using AzurLaneDex.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIEx;  // 如果未安装 WinUIEx，请注释并改用原生方法

namespace AzurLaneDex.Views
{
    public sealed partial class SettingsPage : Page
    {
        private App _app;

        public SettingsPage()
        {
            this.InitializeComponent();
            _app = Application.Current as App;
            LoadSettings();
        }

        private void LoadSettings()
        {
            // 加载日志记录设置
            if (_app.ShipManager?.Config != null)
            {
                var logEdits = _app.ShipManager.Config.GetValueOrDefault("log_edits", true);
                LogEditsCheckBox.IsChecked = logEdits is bool b ? b : true;
            }

            // 窗口大小显示
            UpdateWindowSizeLabel();
        }

        private void UpdateWindowSizeLabel()
        {
            var window = _app.GetMainWindow();
            if (window != null)
            {
                var bounds = window.Bounds;
                WindowSizeLabel.Text = $"当前窗口大小: {bounds.Width} x {bounds.Height}";
            }
        }

        private async void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            // 实现密码修改逻辑（可暂不实现，提示开发中）
            await ShowDialog("编辑密码", "功能开发中");
        }

        private async void UpdateData_Click(object sender, RoutedEventArgs e)
        {
            await ShowDialog("网络更新", "功能开发中");
        }

        private async void MigrateOldData_Click(object sender, RoutedEventArgs e)
        {
            // 调用之前实现的迁移逻辑
            // 为了简洁，此处调用一个公共方法，您需要将之前写的迁移代码移入
            await PerformDataMigration();
        }

        private async Task PerformDataMigration()
        {
            var picker = new FileOpenPicker();
            var window = _app.GetMainWindow();
            if (window == null) return;
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
            picker.FileTypeFilter.Add(".json");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            string oldJson;
            try
            {
                oldJson = await FileIO.ReadTextAsync(file);
            }
            catch
            {
                await ShowDialog("错误", "无法读取文件");
                return;
            }

            // 迁移逻辑（请复用之前完整的 MigrateOldStaticJson 方法）
            var newStatic = MigrateOldStaticJson(oldJson);
            if (newStatic == null)
            {
                await ShowDialog("错误", "文件格式不正确");
                return;
            }

            string targetPath = System.IO.Path.Combine(App.DataRoot, "static", "ships_static.json");
            try
            {
                var newJson = JsonSerializer.Serialize(newStatic, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(targetPath, newJson);
                _app.ShipManager?.Load();
                await ShowDialog("成功", "旧数据已转换并覆盖。请重新启动应用或切换页面以查看变更。");
            }
            catch (Exception ex)
            {
                await ShowDialog("失败", ex.Message);
            }
        }

        private StaticData MigrateOldStaticJson(string oldJson)
        {
            // 请复制之前写的完整迁移方法，此处略（避免重复）
            // 在实际项目中，您可以从之前的代码中复制 MigrateOldStaticJson 和 MigrateSingleShip 方法
            // 为了编译通过，暂时返回 null
            return null;
        }

        private async void ResetWindowClick(object sender, RoutedEventArgs e)
        {
            var window = _app.GetMainWindow();
            if (window == null) return;
            var manager = WinUIEx.WindowManager.Get(window);
            manager.PersistenceId = "MainWindow";
            manager.Width = 1310;
            manager.Height = 750;
            UpdateWindowSizeLabel();
            var dialog = new ContentDialog
            {
                Title = "重置窗口",
                Content = "窗口大小已重置",
                CloseButtonText = "确定",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void NavigateToUpdatePage(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(UpdatePage));
        }

        private async Task ShowDialog(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void OnLogEditsToggled(object sender, RoutedEventArgs e)
        {
            if (_app.ShipManager?.Config != null)
            {
                _app.ShipManager.Config["log_edits"] = LogEditsCheckBox.IsChecked ?? true;
                _app.ShipManager.SaveConfig();
            }
        }
    }

    // 辅助类（如果迁移方法需要）
    public class StaticData
    {
        public string Version { get; set; }
        public List<ShipStatic> Ships { get; set; }
    }
}