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
                XamlRoot = this.XamlRoot
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
                XamlRoot = this.XamlRoot
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
            }
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