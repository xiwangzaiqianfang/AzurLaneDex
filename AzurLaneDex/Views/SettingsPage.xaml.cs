using AzurLaneDex.Models;
using AzurLaneDex.Services;
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
using WinUIEx;

namespace AzurLaneDex.Views
{
    public sealed partial class SettingsPage : Page
    {
        private App _app;

        public SettingsPage()
        {
            this.InitializeComponent();
            _app = Application.Current as App;
            this.Loaded += (s, e) => UpdateWindowSizeLabel();
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

        private async void MigrateOldData_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var window = _app.GetMainWindow();
            if (window == null) return;
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            string oldJson = await FileIO.ReadTextAsync(file);
            try
            {
                var newStatic = MigrateOldStaticJson(oldJson);
                if (newStatic == null)
                {
                    await ShowDialog("迁移失败", "文件格式不正确");
                    LogService.Operation("数据迁移操作", "文件格式错误");
                    return;
                }
                string targetPath = Path.Combine(App.DataRoot, "static", "ships_static.json");
                using (var src = await file.OpenStreamForReadAsync())
                using (var dest = File.OpenWrite(targetPath))
                {
                    await src.CopyToAsync(dest);
                }
                _app.ShipManager?.Load();
                await ShowDialog("成功", "文件已替换并自动迁移。");
                LogService.Operation("数据迁移操作", "迁移成功");
            }
            catch (Exception ex)
            {
                await ShowDialog("失败", ex.Message);
                LogService.Operation("数据迁移操作", $"迁移失败：{ex.Message}");
            }
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
            catch (Exception ex)
            {
                await ShowDialog("读取失败", ex.Message);
                LogService.Operation("数据迁移操作", $"迁移失败：{ex.Message}");
                return;
            }

            // 迁移逻辑（请复用之前完整的 MigrateOldStaticJson 方法）
            try
            {
                // 调用迁移函数（直接使用 ShipManager 的迁移逻辑）
                var newStatic = MigrateOldStaticJson(oldJson);
                if (newStatic == null) throw new InvalidOperationException("文件格式不正确");

                string targetPath = Path.Combine(App.DataRoot, "static", "ships_static.json");
                var newJson = JsonSerializer.Serialize(newStatic, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(targetPath, newJson);

                // 重新加载 ShipManager
                _app.ShipManager?.Load();
                await ShowDialog("成功", "数据已迁移并覆盖，请返回主界面查看。");
                LogService.Operation("数据迁移操作", "数据已成功迁移并覆盖");
            }
            catch (Exception ex)
            {
                await ShowDialog("迁移失败", ex.Message);
                LogService.Operation("数据迁移操作", $"迁移失败：{ex.Message}");
            }
        }

        private StaticData MigrateOldStaticJson(string oldJson)
        {
            using var doc = JsonDocument.Parse(oldJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("ships", out var shipsArray) && shipsArray.ValueKind == JsonValueKind.Array)
            {
                var version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "0.0" : "0.0";
                var newShips = MigrateShipArray(shipsArray);
                return new StaticData { Version = version, Ships = newShips };
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                var newShips = MigrateShipArray(root);
                return new StaticData { Version = "0.0", Ships = newShips };
            }
            else
            {
                return null;
            }
        }

        private List<ShipStatic> MigrateShipArray(JsonElement array)
        {
            var list = new List<ShipStatic>();
            foreach (var old in array.EnumerateArray())
            {
                var newShip = ShipManager.MigrateSingleShip(old);
                list.Add(newShip);
            }
            return list;
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

        private void NavigateToLogPage(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(LogPage));
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
    }

    // 辅助类（如果迁移方法需要）
    public class StaticData
    {
        public string Version { get; set; }
        public List<ShipStatic> Ships { get; set; }
    }
}