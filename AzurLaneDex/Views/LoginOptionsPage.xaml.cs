using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AzurLaneDex.Views
{
    public sealed partial class LoginOptionsPage : Page
    {
        private Dictionary<string, object> _config = null;
        private ShipManager _shipManager = null;

        public LoginOptionsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Dictionary<string, object> config)
            {
                _config = config;
                var app = (App)Application.Current;
                _shipManager = app.ShipManager;

                // 加载配置
                AskAccountToggleSwitch.IsOn = _config.GetValueOrDefault("ask_account_on_startup", true) is bool b && b;

                // 填充默认账号下拉框
                var accountManager = app.AccountManager;
                var regularAccounts = accountManager.Accounts.Where(a => !a.IsSystem).Select(a => a.Name).ToList();
                DefaultAccountComboBox.ItemsSource = regularAccounts;
                string defaultAccount = _config.GetValueOrDefault("default_account", "")?.ToString();
                if (!string.IsNullOrEmpty(defaultAccount) && regularAccounts.Contains(defaultAccount))
                    DefaultAccountComboBox.SelectedItem = defaultAccount;
                else
                    DefaultAccountComboBox.SelectedIndex = 0;
            }
        }

        // 实时保存启动时询问账号的设置
        private void AskAccountToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_config != null)
            {
                _config["ask_account_on_startup"] = AskAccountToggleSwitch.IsOn;
                StatusText.Text = AskAccountToggleSwitch.IsOn ? "开" : "关";
                var app = (App)Application.Current;
                if (app.ShipManager != null)
                {
                    app.ShipManager.SaveConfig();
                    System.Diagnostics.Debug.WriteLine($"Saved ask_account_on_startup = {AskAccountToggleSwitch.IsOn}");
                }
            }
        }

        // 实时保存默认账号的设置
        private void DefaultAccountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_config != null && DefaultAccountComboBox.SelectedItem != null)
            {
                _config["default_account"] = DefaultAccountComboBox.SelectedItem.ToString();
                _shipManager?.SaveConfig();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }
        private void LoadConfig()
        {
            AskAccountToggleSwitch.IsOn = _config.GetValueOrDefault("ask_account_on_startup", true) is bool b && b;
            StatusText.Text = AskAccountToggleSwitch.IsOn ? "开" : "关";
        }
    }
}