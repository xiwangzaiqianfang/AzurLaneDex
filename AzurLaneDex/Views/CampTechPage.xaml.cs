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
    public sealed partial class CampTechPage : Page
    {
        private readonly ResourceLoader _loader = ResourceLoader.GetForViewIndependentUse();

        private ShipManager _manager;

        // 所有阵营列表（按常见顺序）
        private List<string> AllFactions()
        {
            return new List<string>
            {
                _loader.GetString("Faction_EagleUnion"),
                _loader.GetString("Faction_RoyalNavy"),
                _loader.GetString("Faction_SakuraEmpire"),
                _loader.GetString("Faction_IronBlood"),
                _loader.GetString("Faction_DragonEmpery"),
                _loader.GetString("Faction_Sardegna"),
                _loader.GetString("Faction_NorthernUnion"),
                _loader.GetString("Faction_FreeFrench"),
                _loader.GetString("Faction_Vichya"),
                _loader.GetString("Faction_Tulip"),
                _loader.GetString("Faction_Tempesta"),
                _loader.GetString("Faction_Other")
            };
        }

        public class CampCardData
        {
            public string Name { get; set; }
            public int Obtain { get; set; }
            public int Max { get; set; }
            public int Level120 { get; set; }
            public int Total => Obtain + Max + Level120;
        }

        public CampTechPage()
        {
            this.InitializeComponent();
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            Loaded += CampTechPage_Loaded;
        }

        private void CampTechPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = Application.Current as App;
            _manager = app?.ShipManager;
            if (_manager != null)
            {
                LoadData();
                // 监听数据变化，实时更新：订阅 Ships 的 CollectionChanged 事件（替代不存在的 data_changed 事件）
                if (_manager.Ships != null)
                {
                    _manager.Ships.CollectionChanged += (s, ev) => LoadData();
                }
            }
        }

        private void LoadData()
        {
            var loader = ResourceLoader.GetForViewIndependentUse();
            // 计算实际阵营科技点
            var campData = _manager.CalculateCampTechPoints();

            // 构建完整卡片列表（所有阵营）
            var cards = new List<CampCardData>();
            var allFactions = AllFactions();
            foreach (var faction in allFactions)
            {
                if (campData.TryGetValue(faction, out var data))
                {
                    cards.Add(new CampCardData
                    {
                        Name = faction,
                        Obtain = data.Obtain,
                        Max = data.Max,
                        Level120 = data.Level120
                    });
                }
                else
                {
                    cards.Add(new CampCardData
                    {
                        Name = faction,
                        Obtain = 0,
                        Max = 0,
                        Level120 = 0
                    });
                }
            }

            CardsRepeater.ItemsSource = cards;

            // 更新总进度条
            int totalTech = _manager.GetTotalTechPoints();
            int ownedTech = _manager.GetOwnedTechPoints();
            int percent = totalTech == 0 ? 0 : ownedTech * 100 / totalTech;
            TechProgressText.Text = string.Format(loader.GetString("CampTechProgress_Format"), ownedTech, totalTech, percent);
            TechProgressBar.Minimum = 0;
            TechProgressBar.Maximum = totalTech;
            TechProgressBar.Value = ownedTech;
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
    }
}