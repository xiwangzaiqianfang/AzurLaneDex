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
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
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
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            var stats = _manager.stats();

            var cards = new List<StatCardData>
            {
                new StatCardData { Title = loader.GetString("Stat_TotalShips"), Value = stats.Total },
                new StatCardData { Title = loader.GetString("Stat_Owned"), Value = stats.Owned },
                new StatCardData { Title = loader.GetString("Stat_NotOwned"), Value = stats.NotOwned },
                new StatCardData { Title = loader.GetString("Stat_MaxBreak"), Value = stats.MaxBreakthrough },
                new StatCardData { Title = loader.GetString("Stat_NotMaxBreak"), Value = stats.NotMaxBreakthrough },
                new StatCardData { Title = loader.GetString("Stat_Oath"), Value = stats.Oath },
                new StatCardData { Title = loader.GetString("Stat_Remodeled"), Value = stats.Remodeled },
                new StatCardData { Title = loader.GetString("Stat_CanRemodelNot"), Value = stats.CanRemodelNot },
                new StatCardData { Title = loader.GetString("Stat_Level120"), Value = stats.Level120 },
                new StatCardData { Title = loader.GetString("Stat_SpecialGearObtained"), Value = stats.SpecialGearObtained },
                new StatCardData { Title = loader.GetString("Stat_SpecialGearNotObtained"), Value = stats.SpecialGearNotObtained },
            };

            StatsRepeater.ItemsSource = cards;

            // 收集进度（获得数 + 改造数，分母为总舰船 + 可改造总数）
            int numerator = stats.Owned + stats.Remodeled;
            int denominator = stats.Total + stats.CanRemodelTotal;
            int percent = denominator == 0 ? 0 : numerator * 100 / denominator;
            CollectionProgressText.Text = string.Format(loader.GetString("CollectionProgress_Format"), percent, numerator, denominator);
            CollectionProgressBar.Minimum = 0;
            CollectionProgressBar.Maximum = denominator;
            CollectionProgressBar.Value = numerator;
        }

        private async void ExportToImage(object sender, RoutedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
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
                    await ShowDialog(loader.GetString("Dialog_Success_Title"), string.Format(loader.GetString("Picture_Save_Message"), file.Path));
                }
            }
            catch (Exception ex)
            {
                await ShowDialog(loader.GetString("Dialog_Error_Title"), ex.Message);
            }
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

    public class StatCardData
    {
        public string Title { get; set; }
        public int Value { get; set; }
    }
}