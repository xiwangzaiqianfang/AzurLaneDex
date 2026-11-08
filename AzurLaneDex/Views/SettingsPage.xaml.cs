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
using Windows.ApplicationModel.Core;
using Windows.Globalization;
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
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            _app = Application.Current as App;
            this.Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateWindowSizeLabel();
            LoadLanguageSetting();
        }

        private void UpdateWindowSizeLabel()
        {
            var window = _app.GetMainWindow();
            if (window != null)
            {
                var bounds = window.Bounds;
                var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
                string format = loader.GetString("WindowSizeLabel_Text");
                WindowSizeLabel.Text = string.Format(format, bounds.Width, bounds.Height);
            }
        }
        /// <summary>
        /// 从文件导入数据库：选 ships_static.db → 校验 SQLite 文件头 → 备份现有库 →
        /// 替换数据目录中的 ships_static.db → 立即重新装载（只换文件，不写静态数据）。
        /// </summary>
        private async void ImportDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            var picker = new FileOpenPicker();
            var window = _app.GetMainWindow();
            if (window == null) return;
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".db");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            string target = Path.Combine(App.DataRoot, "ships_static.db");
            string temp = target + ".import";
            string backup = target + ".bak";

            try
            {
                // 1) 校验 SQLite 文件头（16 字节 "SQLite format 3\0"）
                var header = new byte[16];
                using (var read = await file.OpenStreamForReadAsync())
                {
                    int n = await read.ReadAsync(header, 0, header.Length);
                    if (n < header.Length || System.Text.Encoding.ASCII.GetString(header) != "SQLite format 3\0")
                    {
                        await ShowDialog(loader.GetString("Dialog_Error_Title"), loader.GetString("InvalidFileFormat_Message"));
                        LogService.Operation("数据库导入", "文件格式错误（不是 SQLite 数据库）");
                        return;
                    }
                }

                // 2) 复制到临时文件 → 备份旧库 → 原子替换
                using (var src = await file.OpenStreamForReadAsync())
                using (var dest = File.Create(temp))
                    await src.CopyToAsync(dest);

                if (File.Exists(target))
                {
                    try { File.Copy(target, backup, overwrite: true); } catch { /* 备份失败不阻断 */ }
                }
                File.Move(temp, target, overwrite: true);

                // 3) 重新装载
                if (_app.ShipManager != null)
                {
                    await _app.ShipManager.LoadAsync();
                    _app.ShipManager.NotifyDataChanged();
                }
                await ShowDialog(loader.GetString("Dialog_Success_Title"), loader.GetString("MigrationSuccess_Message"));
                LogService.Operation("数据库导入", $"已导入 {file.Name}（{new FileInfo(target).Length / 1024 / 1024} MB）");
            }
            catch (Exception ex)
            {
                await ShowDialog(loader.GetString("Dialog_Error_Title"), ex.Message);
                LogService.Operation("数据库导入", $"导入失败：{ex.Message}");
            }
            finally
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { /* 忽略 */ }
            }
        }


        // 添加私有方法：加载已保存的语言设置
        private void LoadLanguageSetting()
        {
            if (_app?.ShipManager?.Config == null) return;

            LanguageComboBox.SelectionChanged -= LanguageComboBox_SelectionChanged;

            string currentLang = _app.ShipManager.Config.GetValueOrDefault("app_language")?.ToString() ?? "zh-Hans";
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == currentLang)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }
            LanguageComboBox.SelectionChanged += LanguageComboBox_SelectionChanged;
        }

        // 添加语言切换事件处理
        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            if (LanguageComboBox.SelectedItem is ComboBoxItem selected)
            {
                string newLang = selected.Tag?.ToString();
                if (string.IsNullOrEmpty(newLang)) return;

                string currentLang = _app.ShipManager.Config.GetValueOrDefault("app_language")?.ToString();
                if (newLang == currentLang) return;

                // 保存语言设置
                _app.ShipManager.Config["app_language"] = newLang;
                _app.ShipManager.SaveConfig();

                // 即时生效：先切应用内语言（数据文本），系统语言覆盖失败也不影响这一步
                AzurLaneDex.Helpers.LocalizationHelper.CurrentLanguage = newLang;
                bool systemOverrideOk = AzurLaneDex.Helpers.LocalizationHelper.TrySetSystemLanguageOverride(newLang);

                try
                {
                    RefreshLocalizedData();
                }
                catch (Exception ex)
                {
                    LogService.Warning($"语言切换后刷新数据文本失败：{ex.Message}", "SettingsPage");
                }

                // 界面框架文本（XAML 资源）仍需重启后才用新语言——询问是否重启
                string content = loader.GetString("Settings_LanguageDataRefreshed");
                if (string.IsNullOrEmpty(content))
                    content = "舰船数据文本已即时切换语言；界面框架文本将在重启后完全生效。是否现在重启应用？";
                if (!systemOverrideOk)
                {
                    content += "\n\n（提示：当前运行方式不支持设置系统语言覆盖，界面框架文案无法随语言切换；"
                             + "打包安装后运行即可正常切换。舰船/装备等数据文本已按新语言显示。）";
                }

                var dialog = new ContentDialog
                {
                    Title = loader.GetString("RestartApp_Title"),
                    Content = content,
                    PrimaryButtonText = loader.GetString("RestartApp_Message_PrimaryButtonText"),
                    CloseButtonText = loader.GetString("RestartApp_Message_CloseButtonText"),
                    XamlRoot = this.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // 请求重启应用（非打包运行时该 API 可能不可用，兜底提示）
                    try
                    {
                        CoreApplication.RequestRestartAsync(string.Empty);
                    }
                    catch (Exception ex)
                    {
                        LogService.Warning($"请求重启失败：{ex.Message}", "SettingsPage");
                    }
                }
            }
        }

        /// <summary>语言切换后通知全部舰船 VM 刷新本地化显示属性。</summary>
        private void RefreshLocalizedData()
        {
            var ships = _app?.ShipManager?.Ships;
            if (ships == null) return;
            foreach (var vm in ships)
                vm.NotifyLocalizationChanged();
        }

        /*
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
                var newShip = _app.ShipManager.MigrateSingleShip(old);
                list.Add(newShip);
            }
            return list;
        }
        */

        private async void ResetWindowClick(object sender, RoutedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            var window = _app.GetMainWindow();
            if (window == null) return;
            var manager = WinUIEx.WindowManager.Get(window);
            if (!App.IsPackaged)
                WinUIEx.WindowManager.PersistenceStorage = App.WindowState;
            manager.PersistenceId = "MainWindow";
            manager.Width = 1310;
            manager.Height = 750;
            UpdateWindowSizeLabel();
            var dialog = new ContentDialog
            {
                Title = loader.GetString("ResetWindow_Title"),
                Content = loader.GetString("WindowReset_Message"),
                CloseButtonText = loader.GetString("Common_Confirm"),
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
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = loader.GetString("Common_Confirm"),
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