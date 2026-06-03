using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            this.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            _accountManager = accountManager;
            _requirePassword = requirePassword;
            LoadAccounts();

            if (!requirePassword)
            {
                PasswordBox.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadAccounts()
        {
            var accounts = _accountManager.GetAccountList();
            AccountCombo.ItemsSource = accounts;
            if (accounts.Any())
                AccountCombo.SelectedIndex = 0;
        }

        // 创建新账户（保持不变）
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
                    LoadAccounts();
                }
                else
                {
                    ShowInlineError("AccountCreateFailed_Message");
                }
            }
            await this.ShowAsync();
        }

        // 登录按钮逻辑
        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var selected = AccountCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(selected))
            {
                args.Cancel = true;
                ShowInlineError("PleaseSelectAccount_Message");
                return;
            }
            if (!_accountManager.VerifyPassword(selected, PasswordBox.Password))
            {
                args.Cancel = true;
                ShowInlineError("InvalidPassword_Message");
                return;
            }
            _accountManager.SetCurrentAccount(selected);
            if (RememberCheckBox.IsChecked == true)
                _accountManager.Save();
        }

        // 忘记密码：切换到重置密码面板
        private void ForgotPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = AccountCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(selected))
            {
                ShowInlineError("PleaseSelectAccount_Message");
                return;
            }

            var question = _accountManager.GetSecurityQuestion(selected);
            if (string.IsNullOrEmpty(question))
            {
                ShowInlineError("NoSecurityQuestion_Message");
                return;
            }

            // 加载本地化文本
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            SecurityQuestionText.Text = $"{loader.GetString("SecurityQuestion_Label")}{question}";
            ResetAnswerBox.Password = "";
            ResetNewPasswordBox.Password = "";
            ResetConfirmBox.Password = "";
            ResetErrorText.Text = "";

            // 切换面板
            LoginPanel.Visibility = Visibility.Collapsed;
            ResetPasswordPanel.Visibility = Visibility.Visible;

            // 修改对话框标题和按钮（可选）
            this.Title = loader.GetString("ResetPasswordDialog_Title");
            this.PrimaryButtonText = "";      // 隐藏默认主按钮
            this.CloseButtonText = loader.GetString("Common_Close");
        }

        // 重置密码确认按钮
        private async void ResetConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = AccountCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(selected)) return;

            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            string answer = ResetAnswerBox.Password;
            string newPwd = ResetNewPasswordBox.Password;
            string confirmPwd = ResetConfirmBox.Password;

            if (newPwd != confirmPwd)
            {
                ResetErrorText.Text = loader.GetString("PasswordMismatch_Message");
                return;
            }
            if (string.IsNullOrEmpty(newPwd))
            {
                ResetErrorText.Text = loader.GetString("PasswordEmpty_Message");
                return;
            }

            if (_accountManager.ResetPasswordBySecurity(selected, answer, newPwd))
            {
                // 重置成功：切回登录面板并显示成功信息
                SwitchToLoginPanel();
                var successLoader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
                ErrorInfoBar.Title = successLoader.GetString("Dialog_Success_Title");
                ErrorInfoBar.Message = successLoader.GetString("PasswordResetSuccess_Message");
                ErrorInfoBar.Severity = InfoBarSeverity.Success;
                ErrorInfoBar.IsOpen = true;
            }
            else
            {
                ResetErrorText.Text = loader.GetString("SecurityAnswerWrong_Message");
            }
        }

        // 取消重置：切回登录面板
        private void ResetCancelButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchToLoginPanel();
        }

        // 切换回登录面板并重置对话框状态
        private void SwitchToLoginPanel()
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            LoginPanel.Visibility = Visibility.Visible;
            ResetPasswordPanel.Visibility = Visibility.Collapsed;
            this.Title = loader.GetString("LoginDialog_Title");
            this.PrimaryButtonText = loader.GetString("LoginDialog_LoginButton");
            this.CloseButtonText = loader.GetString("Common_Cancel");
        }

        private void ShowInlineError(string resourceKey)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            ErrorInfoBar.Message = loader.GetString(resourceKey);
            ErrorInfoBar.Severity = InfoBarSeverity.Error;
            ErrorInfoBar.IsOpen = true;
        }

        private void ErrorInfoBar_CloseButtonClick(InfoBar sender, object args)
        {
            ErrorInfoBar.IsOpen = false;
        }
    }
}