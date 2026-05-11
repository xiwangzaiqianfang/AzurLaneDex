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
        }

        public (string Name, string Password, string Avatar, bool IsDeveloper, bool SetDefault, string SecurityQuestion, string SecurityAnswer) GetAccountInfo()
        {
            return (NameBox.Text.Trim(), PasswordBox.Password, _avatarPath,
                    IsDeveloperCheckBox.IsChecked == false, SetDefaultCheckBox.IsChecked == true,
                    SecurityQuestionBox.Text.Trim(), SecurityAnswerBox.Password);
        }

        private async void SelectAvatar_Click(object sender, RoutedEventArgs e)
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
    }
}