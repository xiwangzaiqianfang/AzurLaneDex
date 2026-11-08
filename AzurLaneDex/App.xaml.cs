using AzurLaneDex.Helpers;
using AzurLaneDex.Services;
using AzurLaneDex.Services.Interfaces;
using AzurLaneDex.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace AzurLaneDex
{
    /// <summary>
    /// 应用入口。启动流程按四段组织：
    /// 1. 存储位置：<see cref="InitializeDataDirectories"/>（打包 → 包 LocalState；非打包 → %LOCALAPPDATA%）
    /// 2. 日志：<see cref="StartupTrace"/>（启动引导）+ <see cref="LogService"/>（业务日志）+ <see cref="LogCrash"/>（崩溃）
    /// 3. 启动顺序：构造函数（注册服务）→ <see cref="OnLaunched"/>（目录 → 服务 → 语言 → 主窗口）
    /// 4. 异常处理：UI / AppDomain / Task 三处统一记录，权限类写盘失败不中断界面
    /// </summary>
    public partial class App : Application
    {
        // ================================================================
        // 一、静态字段
        // ================================================================

        private Window? _window;

        /// <summary>存储根缓存：构造期算一次，OnLaunched 与崩溃日志复用。</summary>
        private static string? _storageBase;

        private static readonly string CrashLogDesktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AzurLaneDex_Crash.log");


        /// <summary>日志目录（构造函数最早期确定，与旧版一致：<c>%LOCALAPPDATA%\AzurLaneDex\data\log</c>）。</summary>
        public static string LogDirectory { get; private set; } = "";

        /// <summary>数据目录（启动时确定，之后只读访问）。</summary>
        public static string DataRoot { get; private set; } = "";

        /// <summary>数据目录是否可写；false 时进入只读运行（用户数据与日志不落盘，界面照常）。</summary>
        public static bool DataRootWritable { get; private set; }

        /// <summary>是否以 MSIX 打包身份运行（原生探测，VS 的打包调试同样返回 true）。</summary>
        public static bool IsPackaged { get; } = DetectPackaged();

        public ShipManager? ShipManager { get; set; }
        public AccountManager? AccountManager { get; set; }
        public FactionAvatarManager? FactionAvatarManager { get; set; }
        public AvatarUpdateService? AvatarUpdateService { get; set; }

        private ServiceProvider _serviceProvider = null!;

        // ================================================================
        // 二、存储位置
        // ================================================================


        /// <summary>
        /// 存储根目录：按候选实测可写性取第一个可用的（LocalState → %LOCALAPPDATA% → LocalLow → %TEMP%）。
        /// 必须保留 LocalLow 兜底：进程被降权到 Low 完整性时，其余候选都会被 MIC 拒绝写入（0x80070005）。
        /// 排查过程见 docs\技术细节说明.md 第 9 节。
        /// </summary>
        private static string ResolveStorageBase()
        {
            var candidates = new List<(string Label, string? Path)>();

            if (IsPackaged)
            {
                try
                {
                    candidates.Add(("ApplicationData.LocalFolder",
                        Windows.Storage.ApplicationData.Current.LocalFolder.Path));
                }
                catch (Exception ex)
                {
                    StartupTrace("存储探测", $"ApplicationData.LocalFolder 不可用：0x{ex.HResult:X8} {ex.Message}");
                }
            }

            string? localAppData = null;
            try { localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
            catch (Exception ex) { StartupTrace("存储探测", $"LocalApplicationData 读取失败：{ex.Message}"); }

            if (!string.IsNullOrEmpty(localAppData))
            {
                candidates.Add(("LocalApplicationData", localAppData));

                // %LOCALAPPDATA%\..\LocalLow —— 低完整性进程的标准可写区
                string? appDataRoot = Path.GetDirectoryName(localAppData);
                if (!string.IsNullOrEmpty(appDataRoot))
                    candidates.Add(("AppData\\LocalLow", Path.Combine(appDataRoot!, "LocalLow")));
            }

            try { candidates.Add(("%TEMP%", Path.GetTempPath())); } catch { }

            foreach (var (label, path) in candidates)
            {
                if (string.IsNullOrEmpty(path)) continue;

                string probeDir;
                try { probeDir = Path.Combine(path!, "AzurLaneDex"); }
                catch { continue; }

                if (QuickWriteTest(probeDir))
                {
                    StartupTrace("存储探测", $"选用【{label}】：{path}｜完整性={IntegrityLevel}");
                    return path!;
                }
                StartupTrace("存储探测", $"【{label}】不可写，试下一个：{path}");
            }

            StartupTrace("存储探测", $"所有候选都不可写（完整性={IntegrityLevel}），退回 %TEMP%");
            try { return Path.GetTempPath(); }
            catch { return AppContext.BaseDirectory; }
        }

        /// <summary>轻量写测试：实写一个探针文件再删。`CreateDirectory` 对已存在目录永远成功，测不出写权限。</summary>
        private static bool QuickWriteTest(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".w_" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "1");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }


        /// <summary>确定数据目录并创建子目录；失败时只降级为只读运行。</summary>
        private void InitializeDataDirectories()
        {
            string basePath = _storageBase ??= ResolveStorageBase();
            DataRoot = Path.Combine(basePath, "AzurLaneDex", "data");

            // 真实写探测：CreateDirectory 对已存在目录永远成功，测不出写权限
            DataRootWritable = QuickWriteTest(DataRoot);

            if (DataRootWritable)
            {
                foreach (string sub in new[] { "log", "users", "static", "avatars" })
                {
                    try { Directory.CreateDirectory(Path.Combine(DataRoot, sub)); } catch { }
                }
            }

            LogService.Info($"数据目录：{DataRoot}（可写={DataRootWritable}，打包={IsPackaged}，完整性={IntegrityLevel}）", "App");

            // 目录已确定 → 让 LogService 重新绑定日志目录。
            // 构造期的 ApplicationData 可能尚未就绪，首次绑定会落到兜底目录；这里重绑一次并迁移旧日志，
            // 避免「startup.log 在 LocalState、log_*.txt 在包外」两份日志分家。
            LogService.RebindLogDirectory();
        }

        /// <summary>首次运行/包内数据库更新时，把包内 ships_static.db 复制到数据目录（已有且不旧则跳过）。</summary>
        private static void CopyDefaultDatabaseIfNeeded()
        {
            if (!DataRootWritable || string.IsNullOrEmpty(DataRoot)) return;

            string destDb = Path.Combine(DataRoot, "ships_static.db");
            string sourceDb = Path.Combine(AppContext.BaseDirectory, "Assets", "ships_static.db");
            if (!File.Exists(sourceDb)) return;

            try
            {
                bool exists = File.Exists(destDb);
                if (exists)
                {
                    var src = new FileInfo(sourceDb);
                    var dst = new FileInfo(destDb);
                    if (src.Length == dst.Length && src.LastWriteTimeUtc <= dst.LastWriteTimeUtc) return;
                    try { File.Copy(destDb, destDb + ".old", overwrite: true); } catch { }
                }

                string tmp = destDb + ".tmp";
                File.Copy(sourceDb, tmp, overwrite: true);
                File.Move(tmp, destDb, overwrite: true);
                LogService.Info($"数据源{(exists ? "已更新" : "已复制")}：{sourceDb} → {destDb}", "App");
            }
            catch (Exception ex)
            {
                LogService.Warning($"复制数据源失败：{ex.Message}", "App");
            }
        }

        /// <summary>当前进程包族名（仅诊断用；非打包返回 null，纯 P/Invoke 不抛 WinRT 异常）。</summary>
        private static string? GetPackageFamilyNameSafe()
        {
            try
            {
                int length = 0;
                int hr = GetCurrentPackageFamilyName(ref length, null);
                if (hr != ErrorInsufficientBuffer || length <= 1) return null;

                var buffer = new System.Text.StringBuilder(length);
                hr = GetCurrentPackageFamilyName(ref length, buffer);
                return hr == 0 ? buffer.ToString() : null;
            }
            catch { return null; }
        }

        // ================================================================
        // 三、日志
        // ================================================================


        /// <summary>启动链路追踪（写入数据目录 log\startup.log；失败静默）。</summary>
        internal static void StartupTrace(string stage, string detail = "")
        {
            try
            {
                string dir = Path.Combine(DataRoot, "log");
                if (string.IsNullOrEmpty(DataRoot)) return;
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "startup.log"),
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}\t{stage}\t{detail}\r\n");
            }
            catch { }
        }


        private static string? _bootstrapLogPath;

        private static string FallbackStartupLogPath =>
            Path.Combine(Path.GetTempPath(), "AzurLaneDex_startup.log");

        private static string BootstrapLogPath
        {
            get
            {
                if (_bootstrapLogPath != null) return _bootstrapLogPath;
                try
                {
                    string dir = string.IsNullOrEmpty(LogDirectory)
                        ? Path.Combine(ResolveStorageBase(), "AzurLaneDex")
                        : LogDirectory;
                    Directory.CreateDirectory(dir);
                    _bootstrapLogPath = Path.Combine(dir, "startup.log");
                }
                catch
                {
                    _bootstrapLogPath = FallbackStartupLogPath;
                }
                return _bootstrapLogPath;
            }
        }

        private static string? _crashLogAppPath;

        private static readonly string CrashLogFallbackPath =
            Path.Combine(Path.GetTempPath(), "AzurLaneDex_Crash.log");

        /// <summary>记录崩溃：优先数据目录 log，其次 %TEMP%，全部失败静默。</summary>
        private static void LogCrash(Exception ex, string source)
        {
            try
            {
                string content = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}]\n" +
                                 $"Type: {ex.GetType()}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}\n";

                _crashLogAppPath ??= ResolveCrashLogPath();
                if (!string.IsNullOrEmpty(_crashLogAppPath))
                {
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(_crashLogAppPath)!);
                        File.AppendAllText(_crashLogAppPath, content + Environment.NewLine);
                    }
                    catch { }
                }

                try { File.AppendAllText(CrashLogFallbackPath, content + Environment.NewLine); } catch { }

                // 与旧版一致：桌面再放一份，方便直接看到
                try
                {
                    string desktop = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AzurLaneDex_Crash.log");
                    File.AppendAllText(desktop, content + Environment.NewLine);
                }
                catch { }
            }
            catch { }
        }

        private static string ResolveCrashLogPath()
        {
            string name = $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            try
            {
                if (!string.IsNullOrEmpty(DataRoot)) return Path.Combine(DataRoot, "log", name);

                // DataRoot 还没算出来时，也绝不能拼 …\Packages\<PFN>\LocalState：
                // 容器内这个字面路径的写入会被拒绝（0x80070005）。
                string basePath = _storageBase ??= ResolveStorageBase();
                return Path.Combine(basePath, "AzurLaneDex", "data", "log", name);
            }
            catch { }
            return CrashLogFallbackPath;
        }

        // ================================================================
        // 四、启动
        // ================================================================

        public App()
        {
            // 0) 日志目录最早期就建好（与旧版一致），后续 LogService / 崩溃日志 / 启动追踪都用它
            try
            {
                _storageBase = ResolveStorageBase();
                LogDirectory = Path.Combine(_storageBase, "AzurLaneDex", "data", "log");
                Directory.CreateDirectory(LogDirectory);
                _crashLogAppPath = Path.Combine(LogDirectory, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            }
            catch { LogDirectory = ""; }

            // 1) 异常钩子先挂，保证构造期的异常也能记下来
            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            try
            {
                // 2) XAML 资源
                InitializeComponent();
                StartupTrace("启动", "XAML 资源已加载");

                // 3) 依赖注入
                var services = new ServiceCollection();
                services.AddSingleton<IShipDataStore, ShipFileStore>();
                services.AddSingleton<IShipDataUpdater, ShipDataUpdater>();
                services.AddSingleton<IUpdateService, UpdateService>();
                services.AddSingleton<IShipStatsCalculator, ShipStatsCalculator>();
                services.AddSingleton<ShipManager>();
                services.AddSingleton<AccountManager>();
                services.AddSingleton<FactionAvatarManager>();
                services.AddSingleton<AvatarUpdateService>();
                _serviceProvider = services.BuildServiceProvider();
                StartupTrace("启动", "服务容器已构建");

                LogService.Info("应用程序已启动", "App");
            }
            catch (Exception ex)
            {
                LogCrash(ex, "App 构造函数");
            }
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine($"IsPackaged = {App.IsPackaged}");
            try
            {
                // 1) 目录与数据源
                InitializeDataDirectories();
                CopyDefaultDatabaseIfNeeded();
                LogService.Info(
                    $"运行身份：{(IsPackaged ? "打包" : "非打包")}｜包族：{GetPackageFamilyNameSafe() ?? "(无)"}｜数据目录：{DataRoot}｜可写：{DataRootWritable}｜完整性：{IntegrityLevel}",
                    "App");

                // 2) 服务
                AccountManager = _serviceProvider.GetRequiredService<AccountManager>();
                FactionAvatarManager = _serviceProvider.GetRequiredService<FactionAvatarManager>();
                AvatarUpdateService = _serviceProvider.GetRequiredService<AvatarUpdateService>();

                // 3) 语言（必须早于主窗口，界面文本依赖）
                var shipManager = _serviceProvider.GetRequiredService<ShipManager>();
                ShipManager = shipManager;
                ApplySavedLanguage(shipManager);

                // 4) 主窗口
                StartupTrace("启动", "创建主窗口");
                _window = new MainWindow();
                try { _window.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop(); } catch { }
                _window.Activate();
                StartupTrace("启动", "主窗口已激活");

                // 打包运行却写不了自己的数据目录：几乎必然是进程被降权（Low IL）或注册状态未刷新。
                // 只提示、不退出——界面仍可只读使用，但要让用户立刻知道「这次什么都没存下」。
                if (IsPackaged && !DataRootWritable)
                {
                    StartupTrace("启动", $"打包态数据目录不可写｜完整性={IntegrityLevel}");
                    _ = ShowStorageWarningAsync();
                }
            }
            catch (Exception ex)
            {
                LogCrash(ex, "OnLaunched");
                await ShowStartupFailureAsync(ex);
                Application.Current.Exit();
            }
        }

        /// <summary>启动失败提示：优先 ContentDialog，失败退回系统 MessageBox。</summary>
        private async Task ShowStartupFailureAsync(Exception ex)
        {
            try
            {
                if (_window?.Content is FrameworkElement fe && fe.XamlRoot != null)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "启动失败",
                        Content = $"应用启动过程中发生严重错误：\n{ex.Message}\n\n日志：{CrashLogFallbackPath}",
                        CloseButtonText = "退出",
                        XamlRoot = fe.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return;
                }
            }
            catch { }

            try
            {
                MessageBoxW(IntPtr.Zero,
                    $"应用启动失败：\n{ex.Message}\n\n日志：{CrashLogFallbackPath}",
                    "AzurLaneDex", 0x00000010 /* MB_ICONERROR */);
            }
            catch { }
        }

        /// <summary>
        /// 打包运行但数据目录不可写时的提示：说明本次只会读运行，并给出排查方向。
        /// 不退出应用（界面仍可只读使用），但必须让用户知道「这次什么都没存」。
        /// </summary>
        private async Task ShowStorageWarningAsync()
        {
            string detail =
                $"数据目录：{DataRoot}\n" +
                $"进程完整性级别：{IntegrityLevel}\n" +
                $"打包运行：{IsPackaged}\n\n" +
                "应用将以只读方式运行：账户、收藏状态、配队、设置和日志都不会保存。\n\n" +
                "常见原因与处理：\n" +
                "1. 进程被降权到 Low 完整性（此时连 %LOCALAPPDATA% 都写不进）\n" +
                "2. 包注册状态未刷新（dotnet build 只编译、不重新注册）\n\n" +
                "请在 Visual Studio 里执行「生成 → 部署解决方案」后重启应用。";

            try
            {
                if (_window?.Content is FrameworkElement fe && fe.XamlRoot != null)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "数据目录不可写",
                        Content = detail,
                        PrimaryButtonText = "退出",
                        CloseButtonText = "继续（只读）",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = fe.XamlRoot
                    };
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        Application.Current.Exit();
                    return;
                }
            }
            catch { /* 退回系统消息框 */ }

            try
            {
                MessageBoxW(IntPtr.Zero, detail, "AzurLaneDex", 0x00000030 /* MB_ICONWARNING | MB_OK */);
            }
            catch { }
        }

        /// <summary>从 config 读取 app_language 并应用到数据文本；界面框架文本重启后生效。</summary>
        private static void ApplySavedLanguage(ShipManager shipManager)
        {
            try
            {
                string? savedLang = shipManager.Config.TryGetValue("app_language", out var langObj)
                    ? langObj?.ToString()
                    : null;
                if (string.IsNullOrWhiteSpace(savedLang)) return;

                LocalizationHelper.CurrentLanguage = savedLang;
                LocalizationHelper.TrySetSystemLanguageOverride(savedLang);
                LogService.Info($"应用语言：{savedLang}", "App");
            }
            catch (Exception ex)
            {
                LogService.Warning($"应用语言设置失败：{ex.Message}", "App");
            }
        }

        // ================================================================
        // 五、异常处理
        // ================================================================


        private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception, "UI_Unhandled");
            e.Handled = true;
        }

        private static void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex) LogCrash(ex, "AppDomain_Unhandled");
        }

        private static void OnUnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            LogCrash(e.Exception, "Task_Unobserved");
            e.SetObserved();
        }

        // ================================================================
        // 六、窗口状态与服务访问
        // ================================================================

        public Window GetMainWindow() => _window;

        private static Dictionary<string, object>? _windowState;
        private static readonly object _windowStateLock = new();

        public static IDictionary<string, object> WindowState
        {
            get
            {
                lock (_windowStateLock) return _windowState ??= LoadWindowState();
            }
        }

        private static string WindowStatePath =>
            string.IsNullOrEmpty(DataRoot) ? "" : Path.Combine(DataRoot, "window_state.json");

        private static Dictionary<string, object> LoadWindowState()
        {
            var dict = new Dictionary<string, object>();
            try
            {
                string path = WindowStatePath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return dict;

                var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(
                    File.ReadAllText(path));
                if (loaded == null) return dict;

                foreach (var kv in loaded)
                {
                    dict[kv.Key] = kv.Value.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.Number => kv.Value.TryGetInt32(out int i) ? i : kv.Value.GetDouble(),
                        System.Text.Json.JsonValueKind.True => true,
                        System.Text.Json.JsonValueKind.False => false,
                        System.Text.Json.JsonValueKind.String => kv.Value.GetString() ?? "",
                        _ => kv.Value.ToString()
                    };
                }
            }
            catch { }
            return dict;
        }

        public static void SaveWindowState()
        {
            try
            {
                string path = WindowStatePath;
                if (string.IsNullOrEmpty(path)) return;

                Dictionary<string, object> snapshot;
                lock (_windowStateLock) snapshot = new Dictionary<string, object>(_windowState ?? new());

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(
                    snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public async Task<bool> SwitchAccountAsync()
        {
            var dialog = new AccountLoginDialog(AccountManager!);
            if (GetMainWindow() is not MainWindow mainWindow) return false;

            dialog.XamlRoot = mainWindow.Content.XamlRoot;
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;

            if (ShipManager != null)
                await ShipManager.SwitchAccountAsync(AccountManager!.CurrentAccount);

            ShipManager?.NotifyDataChanged();
            mainWindow.NavigateTo(typeof(MainPage));
            mainWindow.SetSelectedNavItem("MainPage");
            return true;
        }

        public T GetService<T>() => _serviceProvider.GetService<T>()!;

        // ================================================================
        // 七、平台互操作
        // ================================================================

        private const int ErrorInsufficientBuffer = 122;
        private const int AppModelNoPackage = 15700;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFamilyName(ref int packageFamilyNameLength, System.Text.StringBuilder? packageFamilyName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, System.Text.StringBuilder? packageFullName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

        // ---- 令牌完整性级别（纯只读诊断）----
        // 目的：区分「被降权到 Low IL」与「普通 ACL 拒绝」。
        // Low IL 下 MIC 检查先于 ACL 生效，任何 Medium 目录（含 %LOCALAPPDATA%、LocalState）都写不进，
        // 而读不受影响——日志里没有这一行就只能靠猜。

        private const int TokenQuery = 0x0008;
        private const int TokenIntegrityLevel = 25;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, int desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass,
            IntPtr tokenInformation, int tokenInformationLength, out int returnLength);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern byte GetSidSubAuthorityCount(IntPtr sid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern IntPtr GetSidSubAuthority(IntPtr sid, int subAuthority);

        private static string? _integrityLevel;

        /// <summary>当前进程令牌的完整性级别文本（如 <c>Medium(0x2000)</c>；查询失败返回「未知」）。</summary>
        internal static string IntegrityLevel => _integrityLevel ??= IntegrityLevelText();

        private static string IntegrityLevelText()
        {
            IntPtr token = IntPtr.Zero;
            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out token)) return "未知";
                if (!GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out int size) && size <= 0)
                    return "未知";

                buffer = Marshal.AllocHGlobal(size);
                if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, size, out _)) return "未知";

                // TOKEN_MANDATORY_LABEL { SID_AND_ATTRIBUTES { PSID Sid; DWORD Attributes; } }
                IntPtr sid = Marshal.ReadIntPtr(buffer);
                byte count = GetSidSubAuthorityCount(sid);
                if (count == 0) return "未知";

                IntPtr ridPtr = GetSidSubAuthority(sid, count - 1);
                if (ridPtr == IntPtr.Zero) return "未知";
                uint rid = (uint)Marshal.ReadInt32(ridPtr);

                string name = rid switch
                {
                    0x0000 => "Untrusted",
                    0x1000 => "Low",
                    0x2000 => "Medium",
                    0x2100 => "MediumPlus",
                    0x3000 => "High",
                    0x4000 => "System",
                    _ => "其他"
                };
                return $"{name}(0x{rid:X})";
            }
            catch (Exception ex)
            {
                return $"未知({ex.GetType().Name})";
            }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                if (token != IntPtr.Zero) CloseHandle(token);
            }
        }

        private static bool DetectPackaged()
        {
            try
            {
                int length = 0;
                return GetCurrentPackageFullName(ref length, null) != AppModelNoPackage;
            }
            catch { return false; }
        }
    }
}
