using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AzurLaneDex.Views
{
    public sealed partial class AccountBackupPage : Page
    {
        private ShipManager _shipManager;
        private AccountManager _accountManager;
        private string _currentAccount;

        public AccountBackupPage()
        {
            this.InitializeComponent();
            Loaded += AccountBackupPage_Loaded;
        }

        private void AccountBackupPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            _shipManager = app.ShipManager;
            _accountManager = app.AccountManager;
            _currentAccount = _accountManager.CurrentAccount;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        // 导出用户状态
        private async void ExportData_Click(object sender, RoutedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            try
            {
                var picker = new FileSavePicker();
                var window = (Application.Current as App)?.GetMainWindow();
                if (window != null)
                    InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("JSON文件", new[] { ".json" });
                picker.SuggestedFileName = $"ships_state_{_currentAccount}_{DateTime.Now:yyyyMMddHHmmss}";

                var file = await picker.PickSaveFileAsync();
                if (file == null) return;

                string statePath = _shipManager.GetUserStatePath();
                if (!File.Exists(statePath))
                {
                    await ShowError(loader.GetString("UserStateFileNotFound_Message"));
                    LogService.Operation("用户状态操作操作", "导出功能没有找到用户状态文件");
                    return;
                }

                using (var src = File.OpenRead(statePath))
                using (var dest = await file.OpenStreamForWriteAsync())
                {
                    await src.CopyToAsync(dest);
                }

                await ShowSuccess(loader.GetString("Dialog_Success_Title"), string.Format(loader.GetString("ExportSuccess_Message"), file.Path));
                LogService.Operation("用户状态操作", $"用户状态文件导出至 {file.Path}");
            }
            catch (Exception ex)
            {
                await ShowError(string.Format(loader.GetString("ExportFailed_Message"), ex.Message));
                LogService.Operation("用户状态操作", $"导出失败：{ex.Message}");
            }
        }

        // 导入用户状态（覆盖当前账户）
        private async void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            try
            {
                var picker = new FileOpenPicker();
                var window = (Application.Current as App)?.GetMainWindow();
                if (window != null)
                    InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
                picker.FileTypeFilter.Add(".json");

                var file = await picker.PickSingleFileAsync();
                if (file == null) return;

                var confirm = new ContentDialog
                {
                    Title = loader.GetString("Import_Message_Title"),
                    Content = loader.GetString("Import_Message_Content"),
                    PrimaryButtonText = loader.GetString("Import_Message_Confirm"),
                    CloseButtonText = loader.GetString("Common_Cancel"),
                    XamlRoot = this.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };
                if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

                string targetPath = _shipManager.GetUserStatePath();
                using (var src = await file.OpenStreamForReadAsync())
                using (var dest = File.OpenWrite(targetPath))
                {
                    dest.SetLength(0);
                    await src.CopyToAsync(dest);
                }

                await _shipManager.LoadAsync();
                _shipManager.NotifyDataChanged();

                await ShowSuccess(loader.GetString("Dialog_Success_Title"), loader.GetString("ImportSuccess_Message")); 
                LogService.Operation("用户状态操作", "用户状态导入");
            }
            catch (Exception ex)
            {
                await ShowError(string.Format(loader.GetString("ImportFailed_Message"), ex.Message));
                LogService.Operation("用户状态操作", $"导入失败，{ex.Message}");
            }
        }

        // 创建备份点（带时间戳的副本）
        private async void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            try
            {
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AzurLaneDex", "backups");
                Directory.CreateDirectory(backupDir);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFile = Path.Combine(backupDir, $"ships_state_{_currentAccount}_{timestamp}.json");
                string statePath = _shipManager.GetUserStatePath();
                if (!File.Exists(statePath))
                {
                    await ShowError(loader.GetString("UserStateFileNotFound_Message"));
                    LogService.Operation("用户状态操作", "备份功能没有找到用户状态文件");
                    return;
                }
                File.Copy(statePath, backupFile, true);
                await ShowSuccess(loader.GetString("Dialog_Success_Title"), string.Format(loader.GetString("BackupSuccess_Message"), backupFile));
                LogService.Operation("用户状态操作", $"用户状态文件保存至：\n{backupFile}");
            }
            catch (Exception ex)
            {
                await ShowError(string.Format(loader.GetString("BackupFailed_Message"), ex.Message));
                LogService.Operation("用户状态操作", $"备份失败：{ex.Message}");
            }
        }

        private async Task ShowError(string message)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            var dialog = new ContentDialog
            {
                Title = loader.GetString("Dialog_Error_Title"),
                Content = message,
                CloseButtonText = loader.GetString("Common_Confirm"),
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
        }

        private async Task ShowSuccess(string title, string message)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = loader.GetString("Common_Confirm"),
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
        }
        private void NavigateToSettingsPage(object sender, RoutedEventArgs e)
        {
            var mainWindow = (Application.Current as App)?.GetMainWindow() as MainWindow;
            if (mainWindow != null)
            {
                mainWindow.NavigateTo(typeof(SettingsPage));
                mainWindow.SetSelectedNavItem("SettingsPage");
            }
            else
            {
                // 备用：直接使用当前 Frame 导航（可能不会更新侧边栏）
                Frame.Navigate(typeof(SettingsPage));
            }
        }
    }
}