using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AzurLaneDex.Views
{
    public sealed partial class LogPage : Page
    {
        private App _app;

        public LogPage()
        {
            this.InitializeComponent();
            Loaded += LogPage_Loaded;
        }

        private void LogPage_Loaded(object sender, RoutedEventArgs e)
        {
            _app = (App)Application.Current;
            // 加载当前配置
            var config = _app.ShipManager?.Config;
            if (config != null)
            {
                bool enabled = config.TryGetValue("log_enabled", out var eObj) && eObj is bool b ? b : true;
                EnableLogToggle.IsOn = enabled;
                int days = config.TryGetValue("log_retention_days", out var dObj) && dObj is int d ? d : 30;
                RetentionDaysBox.Value = days;
            }
        }

        private void EnableLogToggle_Toggled(object sender, RoutedEventArgs e)
        {
            bool enabled = EnableLogToggle.IsOn;
            int days = (int)RetentionDaysBox.Value;
            LogService.SetSettings(enabled, days);
            if (_app?.ShipManager?.Config != null)
            {
                _app.ShipManager.Config["log_enabled"] = enabled;
                _app.ShipManager.Config["log_retention_days"] = days;
                _app.ShipManager.SaveConfig();
            }
            StatusText.Text = enabled ? "日志记录已启用" : "日志记录已禁用";
        }

        private void RetentionDaysBox_ValueChanged(object sender, NumberBoxValueChangedEventArgs e)
        {
            if (!this.IsLoaded) return;
            int days = (int)e.NewValue;
            bool enabled = EnableLogToggle.IsOn;
            LogService.SetSettings(enabled, days);
            if (_app?.ShipManager?.Config != null)
            {
                _app.ShipManager.Config["log_retention_days"] = days;
                _app.ShipManager.SaveConfig();
            }
            // 触发一次清理
            LogService.CleanOldLogs();
            StatusText.Text = $"日志保留天数已设为 {days} 天";
        }

        private async void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker();
                var window = _app.GetMainWindow();
                if (window != null)
                    InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
                picker.FileTypeChoices.Add("文本文件", new[] { ".txt" });
                picker.SuggestedFileName = $"logs_{DateTime.Now:yyyyMMddHHmmss}";
                var file = await picker.PickSaveFileAsync();
                if (file == null) return;

                StatusText.Text = "正在导出日志...";
                string path = await LogService.ExportLogsAsync(file.Path);
                StatusText.Text = $"日志已导出至 {path}";
                await ShowDialog("导出成功", $"日志已保存至 {path}");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"导出失败: {ex.Message}";
                await ShowDialog("导出失败", ex.Message);
            }
        }

        private async Task ShowDialog(string title, string content)
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }
    }
}