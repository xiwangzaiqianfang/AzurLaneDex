using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System;

namespace AzurLaneDex.Views
{
    public sealed partial class FirstRunDialog : ContentDialog
    {
        private string _avatarPath = "";

        public FirstRunDialog()
        {
            this.InitializeComponent();
            this.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        }

        public (string Name, string Password, string Avatar, bool IsDeveloper, bool SetDefault, string SecurityQuestion, string SecurityAnswer) GetAccountInfo()
        {
            return (
                            NameBox.Text.Trim(),
                            PasswordBox.Password,
                            _avatarPath,
                            false,   // IsDeveloper 默认为 false（普通用户）
                            SetDefaultCheckBox.IsChecked == true,
                            SecurityQuestionBox.Text.Trim(),
                            SecurityAnswerBox.Password
                        );
        }

        private async void SelectAvatar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                var window = (Application.Current as App)?.GetMainWindow();
                if (window != null)
                    InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".bmp");
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    _avatarPath = file.Path;
                    AvatarPathText.Text = file.Name;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"选择头像失败: {ex.Message}");
            }
        }
    }
}