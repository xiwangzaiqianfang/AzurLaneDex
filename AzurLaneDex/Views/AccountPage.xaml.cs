using AzurLaneDex.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AzurLaneDex.Views
{
    public sealed partial class AccountPage : Page
    {
        private AccountManager _accountManager;
        private ShipManager _shipManager;
        private Frame _mainFrame;

        public AccountPage()
        {
            this.InitializeComponent();
            var accountManager = ((App)Application.Current).AccountManager;
            Loaded += AccountPage_Loaded;
        }

        private void AccountPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            var mainWindow = (Application.Current as App)?.GetMainWindow() as MainWindow;
            if (mainWindow != null)
            {
                _mainFrame = mainWindow.AppContentFrame;
            }
            if (_mainFrame == null)
            {
                _mainFrame = FindFrameInParent(this);
            }

            _accountManager = app.AccountManager;
            _shipManager = app.ShipManager;
                      
            // 显示当前账户信息
            var currentAccount = _accountManager.GetCurrentAccount();
            if (currentAccount != null)
            {
                AccountNameText.Text = currentAccount.Name;
                string role = currentAccount.IsDeveloper ? "管理员" : "普通用户";
                if (currentAccount.IsSystem) role = "系统账户";
                AccountRoleText.Text = role;
                LoadAvatar(currentAccount.AvatarPath);

                // 加载头像
                LoadAvatar(currentAccount.AvatarPath);
            }
        }
        private Frame FindFrameInParent(DependencyObject child)
        {
            while (child != null && !(child is Frame))
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as Frame;
        }

        private void LoadAvatar(string avatarPath)
        {
            if (!string.IsNullOrEmpty(avatarPath) && File.Exists(avatarPath))
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(avatarPath));
                    AvatarImage.Source = bitmap;
                }
                catch { SetDefaultAvatar(); }
            }
            else
            {
                SetDefaultAvatar();
            }
        }

        private void SetDefaultAvatar()
        {
            // 默认头像（可替换为项目中的资源图片）
            AvatarImage.Source = new BitmapImage(new Uri("ms-appx:///Assets/default_avatar.png"));
        }

        // 修改头像
        private async void ChangeAvatarButton_Click(object sender, RoutedEventArgs e)
        {
            // 创建文件选择器
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var window = (Application.Current as App)?.GetMainWindow();
            if (window != null)
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            // 保存头像到应用数据目录
            string avatarFileName = $"{_accountManager.CurrentAccount}_avatar.png";
            string avatarDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzurLaneDex", "avatars");
            Directory.CreateDirectory(avatarDir);
            string avatarPath = Path.Combine(avatarDir, avatarFileName);

            // 复制或转换图片（简单复制，但可能需要缩放，此处简化）
            using (var srcStream = await file.OpenReadAsync())
            using (var destStream = System.IO.File.OpenWrite(avatarPath))
            {
                await srcStream.AsStreamForRead().CopyToAsync(destStream);
            }

            // 更新账户信息
            var currentAccount = _accountManager.Accounts.FirstOrDefault(a => a.Name == _accountManager.CurrentAccount);
            if (currentAccount != null)
            {
                currentAccount.AvatarPath = avatarPath;
                _accountManager.Save();
                // 刷新显示
                LoadAvatar(avatarPath);
            }
        }
        private async void SwitchAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            var regularAccounts = app.AccountManager.GetAccountList();
            if (regularAccounts.Count <= 1)
            {
                await ShowInfo("没有其他可用账户", "当前只有一个账户，无法切换。");
                return;
            }
            if (await app.SwitchAccountAsync())
            {
                await ShowInfo("切换成功", "账户已切换，将返回主界面。");
            }
        }
        
        private async Task ShowInfo(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
        }

        // 登录选项 -> 二级页面
        private void NavigateToLoginOptionsPage(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LoginOptionsPage), _shipManager?.Config);
        }

        // 你的账户 -> 二级页面
        private void NavigateToManageAccountsPage(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ManageAccountsPage));
        }

        // 帐户备份 -> 二级页面
        private void NavigateToBackupPage(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(AccountBackupPage));
        }
    }
}