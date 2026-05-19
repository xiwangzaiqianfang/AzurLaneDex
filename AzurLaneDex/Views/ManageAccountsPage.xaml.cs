using AzurLaneDex.Models;
using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AzurLaneDex.Views
{
    public sealed partial class ManageAccountsPage : Page
    {
        private AccountManager _accountManager;
        private bool _isUpdating = false;

        public ManageAccountsPage()
        {
            this.InitializeComponent();
            Loaded += ManageAccountsPage_Loaded;
        }

        private void ManageAccountsPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            _accountManager = app.AccountManager;
            System.Diagnostics.Debug.WriteLine($"Current Account: {_accountManager.CurrentAccount}");
            var current = _accountManager.Accounts.FirstOrDefault(a => a.Name == _accountManager.CurrentAccount);
            if (current != null)
            {
                System.Diagnostics.Debug.WriteLine($"IsDeveloper: {current.IsDeveloper}, IsSystem: {current.IsSystem}");
            }
            LoadAccounts();
        }

        private void LoadAccounts()
        {
            // 只显示非系统账户
            var regularAccounts = _accountManager.Accounts.Where(a => !a.IsSystem).ToList();
            AccountsRepeater.ItemsSource = regularAccounts;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private async void AddAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FirstRunDialog();
            dialog.XamlRoot = this.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var info = dialog.GetAccountInfo();
                if (_accountManager.AddAccount(info.Name, info.Password, info.Avatar, info.IsDeveloper))
                {
                    _accountManager.SetSecurityQuestion(info.Name, info.SecurityQuestion, info.SecurityAnswer);
                    LoadAccounts(); // 刷新列表
                }
                else
                {
                    await ShowError("账户名已存在");
                }
            }
        }

        private async void DeleteAccountButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var account = button?.Tag as Account;
            if (account == null) return;

            var dialog = new ContentDialog
            {
                Title = "确认删除",
                Content = $"确定要删除账户“{account.Name}”吗？此操作不可恢复。",
                PrimaryButtonText = "删除",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            // 不能删除当前登录账户（除非有备用账户）
            if (account.Name == _accountManager.CurrentAccount)
            {
                // 删除前先切换到另一个可用账户
                var other = _accountManager.Accounts.FirstOrDefault(a => a.Name != account.Name && !a.IsSystem);
                if (other != null)
                    _accountManager.SetCurrentAccount(other.Name);
                else
                {
                    await ShowError("无法删除当前账户，因为没有其他可用账户。");
                    return;
                }

            }

            _accountManager.DeleteAccount(account.Name);
            LoadAccounts();

            // 如果全部账户被删除，可能需要重新创建或退出应用，但这里简单处理
        }

        private async Task ShowError(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "错误",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
        }
        private async void AdminToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;
            var toggle = sender as ToggleSwitch;
            var account = toggle?.Tag as Account;
            if (account == null) return;

            _isUpdating = true;
            try
            {
                // 不能修改当前登录账户的管理员状态
                if (account.Name == _accountManager.CurrentAccount)
                {
                    await ShowError("不能修改当前登录账户的权限。");
                    toggle.IsOn = !toggle.IsOn; // 恢复原值
                    return;
                }

                var current = _accountManager.Accounts.FirstOrDefault(a => a.Name == _accountManager.CurrentAccount);
                if (current == null || !current.IsDeveloper)
                {
                    await ShowError("只有系统账户或管理员才能修改其他账户的权限。");
                    toggle.IsOn = !toggle.IsOn;
                    return;
                }

                // 应用更改
                account.IsDeveloper = toggle.IsOn;
                _accountManager.Save();
                // 手动刷新界面中该账户的显示文本（如角色文本），但不需要整体刷新列表
                var container = toggle.FindAscendant<Expander>()?.FindDescendant<TextBlock>(tb => tb.Name == "RoleText");
                if (container != null)
                    container.Text = account.IsDeveloper ? "管理员" : "普通用户";
            }
            finally
            {
                _isUpdating = false;
                LoadAccounts();
            }
        }
        private async void RequestAdmin_Click(object sender, RoutedEventArgs e)
        {
            // 弹出密码输入框
            var passwordBox = new PasswordBox { PlaceholderText = "请输入系统管理员密码" };
            var panel = new StackPanel();
            panel.Children.Add(passwordBox);

            var dialog = new ContentDialog
            {
                Title = "提升权限",
                Content = panel,
                PrimaryButtonText = "确认",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string password = passwordBox.Password;
                if (_accountManager.VerifyPassword("developer", password))
                {
                    // 将当前账户设为管理员
                    var current = _accountManager.Accounts.FirstOrDefault(a => a.Name == _accountManager.CurrentAccount);
                    if (current != null)
                    {
                        current.IsDeveloper = true;
                        _accountManager.Save();
                        await ShowError("您已获得管理员权限，请重新打开相关页面以启用新建/编辑功能。");
                    }
                }
                else
                {
                    await ShowError("密码错误，无法提升权限。");
                }
            }
        }
        private async void ChangePasswordButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var account = button?.Tag as Account;
            if (account == null) return;

            var app = (App)Application.Current;
            var currentAccount = app.AccountManager.GetCurrentAccount();

            // 权限检查：如果是修改自己的密码，需验证旧密码；如果是管理员修改他人密码，无需旧密码
            bool isSelf = account.Name == app.AccountManager.CurrentAccount;
            bool isAdmin = currentAccount?.IsDeveloper == true || currentAccount?.IsSystem == true;

            if (!isSelf && !isAdmin)
            {
                await ShowError("只有管理员才能修改其他用户的密码。");
                return;
            }

            var passwordBox = new PasswordBox { PlaceholderText = isSelf ? "旧密码" : "新密码（留空则不修改）" };
            var newPasswordBox = new PasswordBox { PlaceholderText = "新密码" };
            var confirmBox = new PasswordBox { PlaceholderText = "确认新密码" };

            var panel = new StackPanel { Spacing = 12 };
            if (isSelf)
                panel.Children.Add(passwordBox);
            panel.Children.Add(newPasswordBox);
            panel.Children.Add(confirmBox);

            var dialog = new ContentDialog
            {
                Title = $"修改密码 - {account.Name}",
                Content = panel,
                PrimaryButtonText = "确认",
                CloseButtonText = "取消",
                XamlRoot = this.XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            string newPwd = newPasswordBox.Password;
            string confirmPwd = confirmBox.Password;

            if (newPwd != confirmPwd)
            {
                await ShowError("两次输入的新密码不一致");
                return;
            }

            bool success;
            if (isSelf)
            {
                string oldPwd = passwordBox.Password;
                success = app.AccountManager.ChangePassword(account.Name, oldPwd, newPwd);
                if (!success)
                {
                    await ShowError("旧密码错误，修改失败");
                    return;
                }
            }
            else
            {
                // 管理员直接设置新密码（无需旧密码验证）
                // 需要增加 AccountManager 中的方法，或者直接修改
                success = app.AccountManager.AdminSetPassword(account.Name, newPwd);
                if (!success)
                {
                    await ShowError("修改失败");
                    return;
                }
            }

            await ShowError("密码修改成功");
            LogService.Info($"用户 {app.AccountManager.CurrentAccount} 修改了账户 {account.Name} 的密码");
        }
    }
    public static class UIHelper
    {
        public static T FindAscendant<T>(this DependencyObject obj) where T : DependencyObject
        {
            while (obj != null && !(obj is T))
                obj = VisualTreeHelper.GetParent(obj);
            return obj as T;
        }

        public static T FindDescendant<T>(this DependencyObject obj, Func<T, bool> predicate) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t && predicate(t))
                    return t;
                var result = FindDescendant(child, predicate);
                if (result != null) return result;
            }
            return null;
        }
    }
}