using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AzurLaneDex.Views
{
    public sealed partial class StatsPage : Page
    {
        private ShipManager _manager;

        public StatsPage()
        {
            this.InitializeComponent();
            Loaded += StatsPage_Loaded;
        }

        private void StatsPage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = Application.Current as App;
            _manager = app?.ShipManager;
            if (_manager != null)
            {
                LoadStats();
                // 监听数据变化
                _manager.data_changed += () => LoadStats();
            }
        }

        private void LoadStats()
        {
            var stats = _manager.stats();

            var cards = new List<StatCardData>
            {
                new StatCardData { Title = "总计舰船", Value = stats.Total },
                new StatCardData { Title = "已获得", Value = stats.Owned },
                new StatCardData { Title = "未获得", Value = stats.NotOwned },
                new StatCardData { Title = "已满破", Value = stats.MaxBreakthrough },
                new StatCardData { Title = "未满破", Value = stats.NotMaxBreakthrough },
                new StatCardData { Title = "已誓约", Value = stats.Oath },
                new StatCardData { Title = "已改造", Value = stats.Remodeled },
                new StatCardData { Title = "可改造未改造", Value = stats.CanRemodelNot },
                new StatCardData { Title = "120级", Value = stats.Level120 },
                new StatCardData { Title = "已获得特殊兵装", Value = stats.SpecialGearObtained },
                new StatCardData { Title = "未获得特殊兵装", Value = stats.SpecialGearNotObtained },
            };

            StatsRepeater.ItemsSource = cards;

            // 收集进度（获得数 + 改造数，分母为总舰船 + 可改造总数）
            int numerator = stats.Owned + stats.Remodeled;
            int denominator = stats.Total + stats.CanRemodelTotal;
            int percent = denominator == 0 ? 0 : numerator * 100 / denominator;
            CollectionProgressText.Text = $"收集进度: {percent}% ({numerator}/{denominator})";
            CollectionProgressBar.Minimum = 0;
            CollectionProgressBar.Maximum = denominator;
            CollectionProgressBar.Value = numerator;
        }

        private async void ExportToImage(object sender, RoutedEventArgs e)
        {
            try
            {
                var renderTarget = new RenderTargetBitmap();
                await renderTarget.RenderAsync(this);

                var picker = new FileSavePicker();
                var window = (Application.Current as App)?.GetMainWindow();
                if (window != null)
                    InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
                picker.FileTypeChoices.Add("PNG Image", new List<string> { ".png" });
                picker.SuggestedFileName = "Statistics";

                var file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    var pixels = await renderTarget.GetPixelsAsync();
                    using (var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                    {
                        var encoder = await Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(
                            Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
                        encoder.SetPixelData(Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8,
                                             Windows.Graphics.Imaging.BitmapAlphaMode.Straight,
                                             (uint)renderTarget.PixelWidth,
                                             (uint)renderTarget.PixelHeight,
                                             96, 96,
                                             pixels.ToArray());
                        await encoder.FlushAsync();
                    }
                    await ShowDialog("导出成功", $"图片已保存至 {file.Path}");
                }
            }
            catch (Exception ex)
            {
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
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    public class StatCardData
    {
        public string Title { get; set; }
        public int Value { get; set; }
    }
}