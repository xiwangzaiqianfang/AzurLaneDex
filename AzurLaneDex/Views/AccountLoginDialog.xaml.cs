using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AzurLaneDex.Views
{
    public sealed partial class AccountLoginDialog : ContentDialog
    {
        private AccountManager _accountManager;
        private bool _requirePassword;

        public AccountLoginDialog(AccountManager accountManager, bool requirePassword = true)
        {
            this.InitializeComponent();
            this.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            _accountManager = accountManager;
            _requirePassword = requirePassword;
            LoadAccounts();

            if (!requirePassword)
            {
                // 隐藏密码框
                PasswordBox.Visibility = Visibility.Collapsed;
                // 可选：修改提示文字
                // 也可以保留密码框但禁用验证，但更简洁是隐藏
            }
        }

        private void LoadAccounts()
        {
            var accounts = _accountManager.GetAccountList();
            AccountCombo.ItemsSource = accounts;
            if (accounts.Any())
                AccountCombo.SelectedIndex = 0;
        }

        private async void OnCreateAccountClick(object sender, RoutedEventArgs e)
        {
            this.Hide();
            var createDialog = new FirstRunDialog();
            createDialog.XamlRoot = this.XamlRoot;
            createDialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            if (await createDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var (name, password, avatar, isDev, setDefault, securityQuestion, securityAnswer) = createDialog.GetAccountInfo();
                if (_accountManager.AddAccount(name, password, avatar, isDev))
                {
                    _accountManager.SetCurrentAccount(name);
                    if (setDefault)
                        _accountManager.SetDefaultAccount(name);
                    _accountManager.Save();
                    // 重新加载账户列表
                    LoadAccounts();
                }
                else
                {
                    // 账户已存在等错误，显示提示
                    var errorDialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "账户创建失败，可能已存在",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot,
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                    };
                    await errorDialog.ShowAsync();
                }
            }
            // 重新显示登录对话框
            await this.ShowAsync();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var selected = AccountCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(selected))
            {
                args.Cancel = true;
                return;
            }
            if (!_accountManager.VerifyPassword(selected, PasswordBox.Password))
            {
                args.Cancel = true;
                // 显示错误（可以使用 Flyout 或 MessageDialog）
                return;
            }
            else
            {
                _accountManager.SetCurrentAccount(selected);
                if (RememberCheckBox.IsChecked == true)
                    _accountManager.Save();
            }
        }
        private async void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var selected = AccountCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(selected))
            {
                await ShowError("请先选择账户");
                return;
            }

            var question = _accountManager.GetSecurityQuestion(selected);
            if (string.IsNullOrEmpty(question))
            {
                await ShowError("该账户未设置密保问题，无法找回密码。请联系管理员。");
                return;
            }

            // 弹出输入答案和设置新密码的对话框
            var answerBox = new PasswordBox { PlaceholderText = "密保答案" };
            var newPasswordBox = new PasswordBox { PlaceholderText = "新密码" };
            var confirmBox = new PasswordBox { PlaceholderText = "确认新密码" };
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = $"密保问题：{question}" });
            panel.Children.Add(answerBox);
            panel.Children.Add(newPasswordBox);
            panel.Children.Add(confirmBox);

            var dialog = new ContentDialog
            {
                Title = "重置密码",
                Content = panel,
                PrimaryButtonText = "重置",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                string answer = answerBox.Password;
                string newPwd = newPasswordBox.Password;
                string confirmPwd = confirmBox.Password;

                if (newPwd != confirmPwd)
                {
                    await ShowError("两次输入的新密码不一致");
                    return;
                }
                if (string.IsNullOrEmpty(newPwd))
                {
                    await ShowError("新密码不能为空");
                    return;
                }

                if (_accountManager.ResetPasswordBySecurity(selected, answer, newPwd))
                {
                    // 重置成功，可自动填写密码框或提示
                    await ShowSuccess("密码已重置，请使用新密码登录。");
                }
                else
                {
                    await ShowError("密保答案错误，无法重置密码。");
                }
            }
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

        private async Task ShowSuccess(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "成功",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
        }
    }
}