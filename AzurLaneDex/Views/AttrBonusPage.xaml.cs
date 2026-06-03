using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.ApplicationModel.Resources;
using WinRT.Interop;

namespace AzurLaneDex.Views
{
    public sealed partial class AttrBonusPage : Page
    {
        private ShipManager _manager;
        private Dictionary<string, int> _currentAttrTotals = new(); // 属性名称 -> 总值
        private readonly ResourceLoader _loader = ResourceLoader.GetForViewIndependentUse();

        private List<string> GetAttrNames()
        {
            return new List<string>
            {
                _loader.GetString("Attr1_HP"),
                _loader.GetString("Attr1_FP"),
                _loader.GetString("Attr1_TRP"),
                _loader.GetString("Attr1_AA"),
                _loader.GetString("Attr1_AVI"),
                _loader.GetString("Attr1_ACC"),
                _loader.GetString("Attr1_RLD"),
                _loader.GetString("Attr1_EVA"),
                _loader.GetString("Attr1_ASW")
            };
        }

        public AttrBonusPage()
        {
            this.InitializeComponent();
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            Loaded += AttrBonusPage_Loaded;
        }

        private void AttrBonusPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = Application.Current as App;
            _manager = app?.ShipManager;
            if (_manager != null)
            {
                // 初始化下拉框
                ShipClassFilter.ItemsSource = new List<string>
                    {
                _loader.GetString("ShipClass_All"),
                _loader.GetString("ShipClass_DD1"),
                _loader.GetString("ShipClass_CL1"),
                _loader.GetString("ShipClass_CA1"),
                _loader.GetString("ShipClass_CB1"),
                _loader.GetString("ShipClass_BC1"),
                _loader.GetString("ShipClass_BB1"),
                _loader.GetString("ShipClass_BBV1"),
                _loader.GetString("ShipClass_CV1"),
                _loader.GetString("ShipClass_CVL1"),
                _loader.GetString("ShipClass_AR1"),
                _loader.GetString("ShipClass_SS1"),
                _loader.GetString("ShipClass_SSV1"),
                _loader.GetString("ShipClass_AE1"),
                _loader.GetString("ShipClass_Sail1"),
                _loader.GetString("ShipClass_BM1")
            };
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
            var attrNames = GetAttrNames();

            _currentAttrTotals.Clear();
            foreach (var attr in attrNames)
            {
                _currentAttrTotals[attr] = 0;
            }

            string allShipsLabel = _loader.GetString("ShipClass_All");
            if (selectedClass == allShipsLabel)
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
                foreach (var attr in attrNames)
                {
                    int total = 0;
                    if (globalBonuses.TryGetValue((selectedClass, attr), out int val))
                        total = val;
                    _currentAttrTotals[attr] = total;
                }
            }

            // 生成卡片数据
            var cards = attrNames.Select(attr => new AttrCardData
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
            var loader = ResourceLoader.GetForViewIndependentUse();
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
                        Title = loader.GetString("Dialog_Success_Title"),
                        Content = string.Format(loader.GetString("Picture_Save_Message"), file.Path),
                        CloseButtonText = loader.GetString("Common_Confirm"),
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
                    Title = loader.GetString("Dialog_Error_Title"),
                    Content = ex.Message,
                    CloseButtonText = loader.GetString("Common_Confirm"),
                    XamlRoot = this.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };
                await errorDialog.ShowAsync();
            }
        }

        private async Task ShowDialog(string title, string content)
        {
            var loader = ResourceLoader.GetForViewIndependentUse();
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = loader.GetString("Common_Confirm"),
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