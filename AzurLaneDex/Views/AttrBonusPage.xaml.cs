using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AzurLaneDex.Views
{
    public sealed partial class AttrBonusPage : Page
    {
        private ShipManager _manager;
        private Dictionary<string, int> _currentAttrTotals = new(); // 属性名称 -> 总值

        private readonly List<string> _allShipClasses = new()
        {
            "全舰种", "驱逐", "轻巡", "重巡", "超巡", "战巡", "战列", "航战",
            "航母", "轻航", "维修", "潜艇", "潜母", "运输", "风帆", "重炮", "其他"
        };

        private readonly List<string> _attrNames = new()
        {
            "耐久", "炮击", "雷击", "防空", "航空",
            "命中", "装填", "机动", "反潜"
        };

        public AttrBonusPage()
        {
            this.InitializeComponent();
            Loaded += AttrBonusPage_Loaded;
        }

        private void AttrBonusPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = Application.Current as App;
            _manager = app?.ShipManager;
            if (_manager != null)
            {
                // 初始化下拉框
                ShipClassFilter.ItemsSource = _allShipClasses;
                ShipClassFilter.SelectedIndex = 0; // "全舰种"

                LoadData();

                // 监听数据变化
                _manager.data_changed += () => LoadData();
            }
        }

        private void LoadData()
        {
            // 计算全局加成
            var globalBonuses = _manager.CalculateGlobalBonuses();

            // 根据当前选择的舰种计算总计
            string selectedClass = ShipClassFilter.SelectedItem as string;
            _currentAttrTotals.Clear();
            foreach (var attr in _attrNames)
            {
                _currentAttrTotals[attr] = 0;
            }

            if (selectedClass == "全舰种")
            {
                // 对所有舰种求和
                foreach (var kvp in globalBonuses)
                {
                    string shipClass = kvp.Key.ShipClass;
                    string attr = kvp.Key.Attr;
                    int value = kvp.Value;
                    if (_currentAttrTotals.ContainsKey(attr))
                        _currentAttrTotals[attr] += value;
                }
            }
            else
            {
                // 只取指定舰种
                foreach (var attr in _attrNames)
                {
                    int total = 0;
                    if (globalBonuses.TryGetValue((selectedClass, attr), out int val))
                        total = val;
                    _currentAttrTotals[attr] = total;
                }
            }

            // 生成卡片数据
            var cards = _attrNames.Select(attr => new AttrCardData
            {
                AttrName = attr,
                Value = _currentAttrTotals[attr]
            }).ToList();

            AttrRepeater.ItemsSource = cards;
        }

        private void ShipClassFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadData();
        }

        private async void ExportToImage(object sender, RoutedEventArgs e)
        {
            try
            {
                // 截取当前页面内容（不包括滚动条外的部分）
                var renderTarget = new RenderTargetBitmap();
                await renderTarget.RenderAsync(this);

                // 保存为图片
                var picker = new FileSavePicker();
                var window = (Application.Current as App)?.GetMainWindow();
                if (window != null)
                    InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
                picker.FileTypeChoices.Add("PNG Image", new List<string> { ".png" });
                picker.SuggestedFileName = "CampTech";

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    var pixels = await renderTarget.GetPixelsAsync();
                    using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                    {
                        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                        encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                            Windows.Graphics.Imaging.BitmapAlphaMode.Straight,
                            (uint)renderTarget.PixelWidth,
                            (uint)renderTarget.PixelHeight,
                            96,
                            96,
                            System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.ToArray(pixels));
                        await encoder.FlushAsync();
                    }
                    var dialog = new ContentDialog
                    {
                        Title = "导出成功",
                        Content = $"图片已保存至 {file.Path}",
                        CloseButtonText = "确定",
                        XamlRoot = this.XamlRoot,
                        Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "导出失败",
                    Content = ex.Message,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };
                await errorDialog.ShowAsync();
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
    }

    public class AttrCardData
    {
        public string AttrName { get; set; }
        public int Value { get; set; }
    }
}