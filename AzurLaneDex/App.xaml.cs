using AzurLaneDex.Services;
using AzurLaneDex.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using WinUIEx;

namespace AzurLaneDex
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
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
        public Window GetMainWindow() => _window;

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

                // 4. 记录启动信息
                LogService.Info("应用程序已启动", "App");

                // 5. 显示启动画面（仅打包环境）
                try
                {
                    _simpleSplashScreen = SimpleSplashScreen.ShowDefaultSplashScreen();
                }
                catch (Exception ex)
                {
                    LogService.Warning($"无法显示启动画面: {ex.Message}", "App");
                    _simpleSplashScreen = null;
                }
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

                // 注意：启动画面隐藏已移到 MainWindow 数据加载完成后（由 MainWindow 调用 App.HideSplashScreen）
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

        // 初始化数据目录（修复问题6：使用备用路径）
        private void InitializeDataDirectories()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                localAppData = Path.Combine(baseDir, "Data");
                LogService.Warning("未能获取 LocalAppData，使用程序目录下的 Data 文件夹作为数据根目录", "App");
            }

            DataRoot = Path.Combine(localAppData, "AzurLaneDex", "data");

            try
            {
                Directory.CreateDirectory(Path.Combine(DataRoot, "static"));
                Directory.CreateDirectory(Path.Combine(DataRoot, "users"));
                Directory.CreateDirectory(Path.Combine(DataRoot, "log"));
                CopyDefaultStaticIfNeeded();
            }
            catch (Exception ex)
            {
                LogService.Error($"无法创建数据目录，将使用临时目录：{ex.Message}", "App", ex);
                // 降级：使用临时目录
                DataRoot = Path.Combine(Path.GetTempPath(), "AzurLaneDex", "data");
                Directory.CreateDirectory(DataRoot);
                Directory.CreateDirectory(Path.Combine(DataRoot, "static"));
                Directory.CreateDirectory(Path.Combine(DataRoot, "users"));
                Directory.CreateDirectory(Path.Combine(DataRoot, "log"));
                // 尝试从包内复制，但可能失败，后续 ShipManager 会再尝试
                CopyDefaultStaticIfNeeded();
            }
        }

        // 复制默认静态文件（问题6增强：优先从包内复制）
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

        // 切换账户（保持不变）
        public async Task<bool> SwitchAccountAsync()
        {
            var dialog = new AccountLoginDialog(this.AccountManager);
            var mainWindow = GetMainWindow() as MainWindow;
            if (mainWindow == null) return false;
            dialog.XamlRoot = mainWindow.Content.XamlRoot;
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return false;
            System.Diagnostics.Debug.WriteLine($"新账户: {this.AccountManager.CurrentAccount}");
            this.ShipManager?.SwitchAccount(this.AccountManager.CurrentAccount);
            this.ShipManager.NotifyDataChanged();
            mainWindow.NavigateTo(typeof(MainPage));
            mainWindow.SetSelectedNavItem("MainPage");
            return true;
        }

        // 隐藏启动画面（由 MainWindow 在数据加载完成后调用，问题12）
        public void HideSplashScreen()
        {
            if (_simpleSplashScreen != null)
            {
                _simpleSplashScreen.Hide();
                _simpleSplashScreen.Dispose();
                _simpleSplashScreen = null;
            }
        }
    }
}