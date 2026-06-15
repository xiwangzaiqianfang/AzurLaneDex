using AzurLaneDex.Services;
using AzurLaneDex.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace AzurLaneDex
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        private SimpleSplashScreen? _simpleSplashScreen;
        private static readonly string CrashLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AzurLaneDex_Crash.log");


        // 全局静态属性，供其他类获取数据根目录
        public static string DataRoot { get; private set; } = "";
        public ShipManager? ShipManager { get; set; }
        public AccountManager? AccountManager { get; set; }
        public Window GetMainWindow() => _window;
        public App()
        {
            try
            {
                InitializeComponent();
                LogService.Info("应用程序已启动", "App");
                // 显示系统默认启动画面（图片已在 Package.appxmanifest 中配置）
                _simpleSplashScreen = SimpleSplashScreen.ShowDefaultSplashScreen();

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
                LogCrash(ex, "App Constructor");
                throw; // 仍然抛出，但日志已记录
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            try
            {
                // 初始化数据目录（使用完整命名空间避免歧义）
                InitializeDataDirectories();
                _window = new MainWindow();
                try
                {
                    _window.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                }
                catch
                { }
                // _window.Activated += Window_Activated;
                _window.Activate();
            }
            catch (Exception ex)
            {
                LogCrash(ex, "OnLaunched");
                // 可以选择显示一个错误对话框（如果 XamlRoot 可用）或直接退出
                Application.Current.Exit();
            }
        }
        /*
        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            // 取消订阅，避免重复执行
            ((Window)sender).Activated -= Window_Activated;

            // ⭐ 关闭并销毁启动画面
            _simpleSplashScreen?.Hide();
            _simpleSplashScreen?.Dispose();
            _simpleSplashScreen = null;
        }
        */

        private void InitializeDataDirectories()
        {
            // 可靠获取用户 Local AppData 路径
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                localAppData = Path.Combine(baseDir, "Data");
                System.Diagnostics.Debug.WriteLine("警告：未能获取 LocalAppData，使用程序目录下的 Data 文件夹作为数据根目录");
            }

            DataRoot = System.IO.Path.Combine(localAppData, "AzurLaneDex", "data");

            try
            {
                // 创建子目录
                Directory.CreateDirectory(Path.Combine(DataRoot, "static"));
                Directory.CreateDirectory(Path.Combine(DataRoot, "users"));
                Directory.CreateDirectory(Path.Combine(DataRoot, "log"));

                // 尝试从程序目录复制默认静态文件（如果存在）
                CopyDefaultStaticIfNeeded();
            }
            catch (Exception ex)
            {
                // 致命错误：无法创建数据目录，记录日志并退出
                LogService.Error($"无法创建数据目录：{ex.Message}", "App", ex);
                throw new InvalidOperationException("无法初始化应用数据目录，请检查磁盘权限或重新安装应用。", ex);
                DataRoot = Path.Combine(Path.GetTempPath(), "AzurLaneDex", "data");
                Directory.CreateDirectory(DataRoot);
                Directory.CreateDirectory(Path.Combine(DataRoot, "static"));
                Directory.CreateDirectory(Path.Combine(DataRoot, "users"));
                Directory.CreateDirectory(Path.Combine(DataRoot, "log"));
            }
        }

        private void CopyDefaultStaticIfNeeded()
        {
            string destStatic = System.IO.Path.Combine(DataRoot, "static", "ships_static.json");
            if (File.Exists(destStatic))
                return;

            // 源文件可能在 exe 所在目录的 data/static 下
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string sourceStatic = System.IO.Path.Combine(exeDir, "data", "static", "ships_static.json");
            if (File.Exists(sourceStatic))
            {
                try
                {
                    File.Copy(sourceStatic, destStatic);
                }
                catch { }
            }
        }
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
            this.ShipManager?.SwitchAccount(this.AccountManager.CurrentAccount);
            // 触发数据变更事件，让所有订阅者刷新
            this.ShipManager.NotifyDataChanged();
            // 导航到主页面，清空 Frame 历史
            mainWindow.NavigateTo(typeof(MainPage));
            // 更新侧边栏选中项
            mainWindow.SetSelectedNavItem("MainPage");
            return true;
        }
        public void HideSplashScreen()
        {
            if (_simpleSplashScreen != null)
            {
                _simpleSplashScreen.Hide();
                _simpleSplashScreen.Dispose();
                _simpleSplashScreen = null;
            }
        }
        private static void LogCrash(Exception ex, string source)
        {
            try
            {
                string content = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}]\n" +
                                 $"Type: {ex.GetType()}\nMessage: {ex.Message}\nStackTrace: {ex.StackTrace}\n";
                File.AppendAllText(CrashLogPath, content + Environment.NewLine);
            }
            catch { /* 日志记录失败则忽略 */ }
        }
    }
}
