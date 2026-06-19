using AzurLaneDex.Services;
using AzurLaneDex.Services.Interfaces;
using AzurLaneDex.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using WinUIEx;

namespace AzurLaneDex
{
    public partial class App : Application
    {
        private Window? _window;
        private SimpleSplashScreen? _simpleSplashScreen;

        // 崩溃日志路径：同时写入桌面和日志目录
        private static readonly string CrashLogDesktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AzurLaneDex_Crash.log");
        private static string? _crashLogAppPath;

        // 全局静态属性，供其他类获取数据根目录
        public static string DataRoot { get; private set; } = "";
        public ShipManager? ShipManager { get; set; }
        public AccountManager? AccountManager { get; set; }
        public FactionAvatarManager? FactionAvatarManager { get; set; }
        public Window GetMainWindow() => _window;

        private ServiceProvider _serviceProvider;

        public App()
        {
            try
            {
                // 1. 强制初始化日志目录（用于后续LogService）
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                 "AzurLaneDex", "data", "log");
                Directory.CreateDirectory(logDir);
                _crashLogAppPath = Path.Combine(logDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                // 2. 订阅全局未处理异常（UI线程、后台线程、Task）
                this.UnhandledException += OnUnhandledException;
                AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

                // 3. 初始化组件
                InitializeComponent();

                var services = new ServiceCollection();
                services.AddSingleton<IShipDataStore, ShipFileStore>();
                services.AddSingleton<IShipMigrator, ShipMigrator>();
                services.AddSingleton<IShipDataUpdater, ShipDataUpdater>();
                services.AddSingleton<IShipStatsCalculator, ShipStatsCalculator>();
                services.AddSingleton<ShipManager>();
                _serviceProvider = services.BuildServiceProvider();

                // 4. 记录启动信息
                LogService.Info("应用程序已启动", "App");

                // 显示系统默认启动画面（图片已在 Package.appxmanifest 中配置）
                try
                {
                    _simpleSplashScreen = SimpleSplashScreen.ShowDefaultSplashScreen();
                }
                catch (Exception ex)
                {
                    LogService.Warning($"无法显示启动画面: {ex.Message}", "App");
                    _simpleSplashScreen = null;
                }

                this.UnhandledException += (sender, e) =>
                {
                    var ex = e.Exception;
                    System.Diagnostics.Debug.WriteLine("=== 未处理异常 ===");
                    while (ex != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Type: {ex.GetType()}");
                        System.Diagnostics.Debug.WriteLine($"Message: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                        ex = ex.InnerException;
                    }
                    e.Handled = true; // 避免应用崩溃
                };
            }
            catch (Exception ex)
            {
                // 构造函数中的异常直接记录
                LogCrash(ex, "App Constructor");
                // 此时无法使用LogService，使用静态方法写入文件
            }
        }

        // 全局异常处理事件
        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            LogCrash(e.Exception, "UI_Unhandled");
            e.Handled = true; // 阻止应用崩溃，但可能状态不稳定
        }

        // 修复：明确使用 System.UnhandledExceptionEventArgs 消除歧义
        private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
                LogCrash(ex, "AppDomain_Unhandled");
        }

        private void OnUnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            LogCrash(e.Exception, "Task_Unobserved");
            e.SetObserved();
        }

        // 核心崩溃日志写入（同时写入桌面和日志目录）
        private static void LogCrash(Exception ex, string source)
        {
            try
            {
                string content = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}]\n" +
                                 $"Type: {ex.GetType()}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}\n";
                // 写入桌面（便于用户发现）
                File.AppendAllText(CrashLogDesktopPath, content + Environment.NewLine);
                // 写入应用日志目录（便于开发者收集）
                if (!string.IsNullOrEmpty(_crashLogAppPath))
                    File.AppendAllText(_crashLogAppPath, content + Environment.NewLine);
            }
            catch { /* 日志记录失败则忽略 */ }
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // 初始化数据目录（包含静态文件复制）
                InitializeDataDirectories();

                // 创建主窗口
                _window = new MainWindow();
                try
                {
                    _window.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                }
                catch { /* 降级处理 */ }
                _window.Activate();
            }
            catch (Exception ex)
            {
                LogCrash(ex, "OnLaunched");
                // 显示错误对话框（如果可能）
                try
                {
                    var dialog = new ContentDialog
                    {
                        Title = "启动失败",
                        Content = $"应用启动过程中发生严重错误：\n{ex.Message}\n\n请检查日志文件：{CrashLogDesktopPath}",
                        CloseButtonText = "退出",
                        XamlRoot = _window?.Content?.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
                catch { }
                Application.Current.Exit();
            }
        }

        // 初始化数据目录
        private void InitializeDataDirectories()
        {
            string basePath;
            try
            {
                // 对于打包应用，ApplicationData.Current.LocalFolder 返回包专属目录
                basePath = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
            }
            catch
            {
                // 若无法获取（极少数情况），降级到普通 LocalAppData
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            string oldDataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzurLaneDex", "data");

            string newDataRoot = Path.Combine(basePath, "AzurLaneDex", "data");

            // 如果旧路径存在且新路径不存在，则执行迁移
            if (Directory.Exists(oldDataRoot) && !Directory.Exists(newDataRoot))
            {
                try
                {
                    LogService.Info($"检测到旧数据路径 {oldDataRoot}，开始迁移到新路径 {newDataRoot}", "App");
                    Directory.CreateDirectory(Path.GetDirectoryName(newDataRoot));
                    // 复制整个目录
                    CopyDirectory(oldDataRoot, newDataRoot, overwrite: true);
                    LogService.Info($"数据迁移完成", "App");
                    // 可选：删除旧数据（建议暂不删除，避免用户担心）
                    // Directory.Delete(oldDataRoot, true);
                }
                catch (Exception ex)
                {
                    LogService.Error($"数据迁移失败，将使用旧路径", "App", ex);
                    // 迁移失败则继续使用旧路径
                    DataRoot = oldDataRoot;
                    EnsureDirectories(DataRoot);
                    return;
                }
            }

            if (Directory.Exists(newDataRoot))
            {
                DataRoot = newDataRoot;
                EnsureDirectories(DataRoot);
                return;
            }

            DataRoot = newDataRoot;
            EnsureDirectories(DataRoot);
        }

        private void EnsureDirectories(string dataRoot)
        {
            Directory.CreateDirectory(Path.Combine(dataRoot, "static"));
            Directory.CreateDirectory(Path.Combine(dataRoot, "users"));
            Directory.CreateDirectory(Path.Combine(dataRoot, "log"));
            CopyDefaultStaticIfNeeded();
        }

        private void CopyDirectory(string sourceDir, string destDir, bool overwrite = false)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectory(subDir, destSubDir, overwrite);
            }
        }

        // 复制默认静态文件
        private void CopyDefaultStaticIfNeeded()
        {
            string destStatic = Path.Combine(DataRoot, "static", "ships_static.json"); 
            if (File.Exists(destStatic))
                return;

            // 1. 从 Package 安装位置复制（打包环境）
            try
            {
                string packagePath = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
                string packageStatic = Path.Combine(packagePath, "Assets", "ships_static.json");
                if (File.Exists(packageStatic))
                {
                    File.Copy(packageStatic, destStatic);
                    LogService.Info($"已从包内资源复制静态文件：{packageStatic} -> {destStatic}", "App");
                    return;
                }
            }
            catch { /* 忽略 */ }

            // 2. 从 exe 目录下的 data/static 复制（开发环境）
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string sourceStatic = Path.Combine(exeDir, "data", "static", "ships_static.json");
            if (File.Exists(sourceStatic))
            {
                try
                {
                    File.Copy(sourceStatic, destStatic);
                    LogService.Info($"已从开发目录复制静态文件：{sourceStatic} -> {destStatic}", "App");
                    return;
                }
                catch { }
            }
            LogService.Warning("未能复制默认静态文件，将在 ShipManager 初始化时再次尝试", "App");
        }

        // 切换账户
        public async Task<bool> SwitchAccountAsync()
        {
            var dialog = new AccountLoginDialog(this.AccountManager);
            var mainWindow = GetMainWindow() as MainWindow;
            if (mainWindow == null) return false;
            dialog.XamlRoot = mainWindow.Content.XamlRoot;
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return false;
            System.Diagnostics.Debug.WriteLine($"新账户: {this.AccountManager.CurrentAccount}");
            // 使用现有的 ShipManager 切换账户
            if (this.ShipManager != null)
                await this.ShipManager.SwitchAccountAsync(this.AccountManager.CurrentAccount);
            // 触发数据变更事件，让所有订阅者刷新
            this.ShipManager.NotifyDataChanged();
            // 导航到主页面，清空 Frame 历史
            mainWindow.NavigateTo(typeof(MainPage));
            // 更新侧边栏选中项
            mainWindow.SetSelectedNavItem("MainPage");
            return true;
        }

        // 隐藏启动画面
        public void HideSplashScreen()
        {
            if (_simpleSplashScreen != null)
            {
                _simpleSplashScreen.Hide();
                _simpleSplashScreen.Dispose();
                _simpleSplashScreen = null;
            }
        }

        public T GetService<T>() => _serviceProvider.GetService<T>();
    }
}
