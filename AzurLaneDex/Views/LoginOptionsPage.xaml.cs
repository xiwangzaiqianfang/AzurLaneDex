using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Resources;
using System.Linq;

namespace AzurLaneDex.Views
{
    public sealed partial class LoginOptionsPage : Page
    {
        private Dictionary<string, object>? _config;
        private ShipManager? _shipManager;
        /// <summary>程序化回填控件时为 true：避免把"打开页面"误当成用户修改并写回配置。</summary>
        private bool _initializing;

        /// <summary>资源键取词（缺失回退中文兜底）。</summary>
        private static string L(string key, string fallback) {
                string text = ResourceLoader.GetForViewIndependentUse().GetString(key);
                return string.IsNullOrEmpty(text) ? fallback : text;
        }

        public LoginOptionsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var app = (App)Application.Current;

            if (e.Parameter is not Dictionary<string, object> config)
            {
                // 未收到配置（例如 ShipManager 尚未就绪）：禁用交互，避免"看起来能改其实无效"
                _config = null;
                AskAccountToggleSwitch.IsEnabled = false;
                DefaultAccountComboBox.IsEnabled = false;
                StatusText.Text = L("Status_1", "未就绪");
                return;
            }

            _config = config;
            _shipManager = app.ShipManager;

            _initializing = true;
            try
            {
                AskAccountToggleSwitch.IsOn = _config.GetValueOrDefault("ask_account_on_startup", true) is bool b && b;
                UpdateStatusText();

                // 填充默认账号下拉框
                var regularAccounts = app.AccountManager.Accounts.Where(a => !a.IsSystem).Select(a => a.Name).ToList();
                DefaultAccountComboBox.ItemsSource = regularAccounts;
                string defaultAccount = _config.GetValueOrDefault("default_account", "")?.ToString() ?? "";
                if (!string.IsNullOrEmpty(defaultAccount) && regularAccounts.Contains(defaultAccount))
                    DefaultAccountComboBox.SelectedItem = defaultAccount;
                else if (regularAccounts.Count > 0)
                    DefaultAccountComboBox.SelectedIndex = 0;   // 仅界面预选，不再顺带改写配置
            }
            finally
            {
                _initializing = false;
            }
        }

        /// <summary>状态文本跟随开关（并走本地化资源，原先硬编码且首次进入不刷新）。</summary>
        private void UpdateStatusText()
        {
            bool on = AskAccountToggleSwitch.IsOn;
            StatusText.Text = L(on ? "Toggle_On" : "Toggle_Off", on ? "开" : "关");
        }

        // 实时保存启动时询问账号的设置
        private void AskAccountToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_initializing || _config == null) return;
            _config["ask_account_on_startup"] = AskAccountToggleSwitch.IsOn;
            UpdateStatusText();
            _shipManager?.SaveConfig();
            System.Diagnostics.Debug.WriteLine($"Saved ask_account_on_startup = {AskAccountToggleSwitch.IsOn}");
        }

        // 实时保存默认账号的设置
        private void DefaultAccountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing || _config == null || DefaultAccountComboBox.SelectedItem == null) return;
            _config["default_account"] = DefaultAccountComboBox.SelectedItem.ToString() ?? "";
            _shipManager?.SaveConfig();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }
    }
}