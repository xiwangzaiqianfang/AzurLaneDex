using AzurLaneDex.Models;
using AzurLaneDex.Services;
using AzurLaneDex.Services.Interfaces;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AzurLaneDex.Views
{
    public sealed partial class UpdatePage : Page
    {
        private IUpdateService _updateService;
        private ShipManager _shipManager;
        private string _currentAppVersion;
        private string? _latestAppVersion;
        private string? _latestAppDownloadUrl;
        private string? _latestDataUrl;
        private string? _remoteDataVersion;

        // 数据更新硬编码地址（不变）
        private const string DataGitHubRawUrl = "https://raw.githubusercontent.com/xiwangzaiqianfang/AzurLaneDex/main/AzurLaneDex/Assets/ships_static.json";
        private const string DataGitHubCdnUrl = "https://cdn.jsdelivr.net/gh/xiwangzaiqianfang/AzurLaneDex@main/AzurLaneDex/Assets/ships_static.json";
        private const string DataGiteeRawUrl = "https://gitee.com/fmlg/AzurLaneDex/raw/main/AzurLaneDex/Assets/ships_static.json";

        public UpdatePage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var app = (App)Application.Current;
            if (app.ShipManager == null)
            {
                StatusText.Text = "ShipManager 未就绪，请返回重试";
                CheckAppUpdateButton.IsEnabled = false;
                CheckDataUpdateButton.IsEnabled = false;
                DownloadAppButton.Visibility = Visibility.Collapsed;
                DownloadDataButton.IsEnabled = false;
                return;
            }

            _shipManager = app.ShipManager;
            _updateService = new UpdateService(_shipManager);

            // 加载版本信息
            _currentAppVersion = _shipManager.GetCurrentAppVersion();
            CurrentVersionText.Text = _currentAppVersion;

            // 加载通道设置（自动检测开发版）
            LoadChannelSetting();

            // 加载源设置
            LoadSourceSetting();

            // 根据当前通道控制源下拉框启用状态
            UpdateAppSourceState();

            // 恢复自定义数据 URL
            if (_shipManager.Config.TryGetValue("data_custom_url", out var dc) && dc is string dUrl)
                DataCustomUrlBox.Text = dUrl;

            // 启用按钮
            CheckAppUpdateButton.IsEnabled = true;
            CheckDataUpdateButton.IsEnabled = true;
            DownloadDataButton.IsEnabled = true;
            DownloadAppButton.Visibility = Visibility.Collapsed;
        }

        private void LoadChannelSetting()
        {
            var channel = _updateService.CurrentChannel;
            // 如果从未保存过通道设置，且当前版本包含 "dev"（不区分大小写），则自动设为 Dev 通道
            if (!_shipManager.Config.ContainsKey("update_channel") &&
                _currentAppVersion?.Contains("dev", StringComparison.OrdinalIgnoreCase) == true)
            {
                channel = UpdateChannel.Dev;
                _updateService.CurrentChannel = channel;
            }

            foreach (ComboBoxItem item in UpdateChannelComboBox.Items)
            {
                if (item.Tag?.ToString() == channel.ToString())
                {
                    UpdateChannelComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void LoadSourceSetting()
        {
            var source = _updateService.CurrentSource;
            foreach (ComboBoxItem item in AppDataSourceCombo.Items)
            {
                if ((source == UpdateSource.GitHub && item.Tag?.ToString() == "GitHub") ||
                    (source == UpdateSource.Gitee && item.Tag?.ToString() == "Gitee"))
                {
                    AppDataSourceCombo.SelectedItem = item;
                    break;
                }
            }
        }

        private void UpdateAppSourceState()
        {
            var channel = _updateService.CurrentChannel;
            bool isStable = channel == UpdateChannel.Stable;
            AppDataSourceCombo.IsEnabled = isStable;
            if (!isStable)
            {
                // 非正式版强制显示 GitHub，但不改变保存的值
                AppDataSourceCombo.SelectedItem = AppDataSourceCombo.Items.FirstOrDefault(i => (i as ComboBoxItem)?.Tag?.ToString() == "GitHub");
            }
            StatusText.Text = $"当前通道: {channel}，更新源: {_updateService.CurrentSource}";
        }

        private void UpdateChannelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UpdateChannelComboBox.SelectedItem is ComboBoxItem selected && selected.Tag is string tag)
            {
                if (Enum.TryParse<UpdateChannel>(tag, out var channel))
                {
                    _updateService.CurrentChannel = channel;
                    LogService.Operation("更新通道变更", $"切换到 {channel}", (Application.Current as App)?.AccountManager?.CurrentAccount);

                    // 更新源状态
                    UpdateAppSourceState();

                    // 重置应用更新状态
                    _latestAppVersion = null;
                    _latestAppDownloadUrl = null;
                    DownloadAppButton.Visibility = Visibility.Collapsed;
                    StatusText.Text = $"已切换到 {channel} 通道";
                }
            }
        }

        private void AppDataSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AppDataSourceCombo.SelectedItem is ComboBoxItem selected && selected.Tag is string tag)
            {
                var source = tag == "GitHub" ? UpdateSource.GitHub : UpdateSource.Gitee;
                _updateService.CurrentSource = source;
                LogService.Operation("应用更新源变更", $"切换到 {source}", (Application.Current as App)?.AccountManager?.CurrentAccount);
                // 重置应用更新状态
                _latestAppVersion = null;
                _latestAppDownloadUrl = null;
                DownloadAppButton.Visibility = Visibility.Collapsed;
                StatusText.Text = $"已切换到 {source} 源";
            }
        }

        private async void CheckAppUpdate_Click(object sender, RoutedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            StatusText.Text = loader.GetString("CheckingAppUpdate");
            DownloadAppButton.Visibility = Visibility.Collapsed;

            try
            {
                var updateInfo = await _updateService.CheckForUpdateAsync();
                if (updateInfo.HasUpdate)
                {
                    StatusText.Text = $"发现新版本 {updateInfo.LatestVersion}（{updateInfo.Channel}）";
                    DownloadAppButton.Visibility = Visibility.Visible;
                    _latestAppVersion = updateInfo.LatestVersion;
                    _latestAppDownloadUrl = updateInfo.DownloadUrl;
                }
                else
                {
                    StatusText.Text = loader.GetString("AlreadyLatestVersion");
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(loader.GetString("CheckUpdateFailed"), ex.Message);
            }
        }

        private async void DownloadApp_Click(object sender, RoutedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            if (string.IsNullOrEmpty(_latestAppDownloadUrl))
            {
                StatusText.Text = loader.GetString("InvalidDownloadUrl");
                return;
            }
            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadProgressBar.IsIndeterminate = false;
            DownloadStatusText.Text = loader.GetString("Downloading");
            StatusText.Text = "";
            try
            {
                string tempDir = Path.GetTempPath();
                string fileName = Path.GetFileName(new Uri(_latestAppDownloadUrl).AbsolutePath);
                string downloadPath = Path.Combine(tempDir, fileName);

                var progress = new Progress<double>(percent =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (percent >= 0)
                        {
                            DownloadProgressBar.Value = percent;
                            DownloadStatusText.Text = $"下载进度: {percent:F1}%";
                        }
                        else
                        {
                            DownloadProgressBar.IsIndeterminate = true;
                            DownloadStatusText.Text = "正在下载... (大小未知)";
                        }
                    });
                });

                bool success = await DownloadWithProgressAsync(_latestAppDownloadUrl, downloadPath, ProxyBox.Text.Trim(), progress);
                if (!success)
                {
                    StatusText.Text = loader.GetString("DownloadFailed");
                    return;
                }

                StatusText.Text = loader.GetString("DownloadCompleteStartingInstall");
                DownloadStatusText.Text = "安装包已就绪，正在启动安装程序...";

                var psi = new ProcessStartInfo
                {
                    FileName = downloadPath,
                    UseShellExecute = true
                };
                Process.Start(psi);

                await Task.Delay(2000);
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(loader.GetString("DownloadFailed1"), ex.Message);
                DownloadStatusText.Text = "";
                DownloadProgressBar.Visibility = Visibility.Collapsed;
            }
            finally
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        DownloadProgressBar.Visibility = Visibility.Collapsed;
                        DownloadStatusText.Text = "";
                    });
                });
            }
        }

        // === 数据更新相关方法（基本不变） ===

        private async Task<bool> DownloadWithProgressAsync(string downloadUrl, string destinationPath, string proxy, IProgress<double> progress)
        {
            using var client = CreateHttpClient(proxy);
            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long? contentLength = response.Content.Headers.ContentLength;
            using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                if (contentLength.HasValue && contentLength.Value > 0)
                {
                    double percent = (double)totalBytesRead / contentLength.Value * 100;
                    progress?.Report(percent);
                }
                else
                {
                    progress?.Report(-1);
                }
            }
            return true;
        }

        private void DataDataSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataCustomUrlBox == null) return;
            DataCustomUrlBox.Visibility = DataDataSourceCombo.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void CheckDataUpdate_Click(object sender, RoutedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            StatusText.Text = loader.GetString("CheckingDataVersion");
            try
            {
                string url = GetDataUrl();
                if (string.IsNullOrEmpty(url))
                {
                    StatusText.Text = loader.GetString("PleaseEnterValidCustomUrl");
                    return;
                }

                _remoteDataVersion = await _shipManager.GetRemoteDataVersionAsync(url, ProxyBox.Text.Trim());
                _latestDataUrl = url;

                if (string.IsNullOrEmpty(_remoteDataVersion))
                {
                    StatusText.Text = loader.GetString("CannotGetRemoteDataVersion");
                    return;
                }

                if (CompareVersion(_remoteDataVersion, _shipManager.Version) > 0)
                {
                    StatusText.Text = string.Format(loader.GetString("NewDataVersionAvailable"), _remoteDataVersion);
                    DownloadDataButton.Visibility = Visibility.Visible;
                }
                else
                {
                    StatusText.Text = loader.GetString("DataAlreadyLatest");
                    DownloadDataButton.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = string.Format(loader.GetString("DataUpdateFailed1"), ex.Message);
            }
        }

        private async void DownloadData_Click(object sender, RoutedEventArgs e)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            if (string.IsNullOrEmpty(_latestDataUrl))
            {
                StatusText.Text = loader.GetString("PleaseCheckUpdateFirst");
                return;
            }

            StatusText.Text = loader.GetString("DownloadingData");
            try
            {
                bool success = await _shipManager.UpdateDataFromUrlAsync(_latestDataUrl, ProxyBox.Text.Trim());
                if (success)
                    StatusText.Text = string.Format(loader.GetString("DataUpdatedToVersion"), _remoteDataVersion);
                else
                    StatusText.Text = loader.GetString("DataUpdateFailed");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"下载失败: {ex.Message}";
            }
        }

        private string GetDataUrl()
        {
            return DataDataSourceCombo.SelectedIndex switch
            {
                0 => DataGitHubRawUrl,
                1 => DataGitHubCdnUrl,
                2 => DataGiteeRawUrl,
                3 => DataCustomUrlBox.Text.Trim(),
                _ => ""
            };
        }

        private int CompareVersion(string versionA, string versionB)
        {
            if (Version.TryParse(versionA, out var vA) && Version.TryParse(versionB, out var vB))
                return vA.CompareTo(vB);
            return string.Compare(versionA, versionB, StringComparison.OrdinalIgnoreCase);
        }

        private HttpClient CreateHttpClient(string proxy)
        {
            if (string.IsNullOrEmpty(proxy))
                return new HttpClient();
            var handler = new HttpClientHandler
            {
                Proxy = new System.Net.WebProxy(proxy),
                UseProxy = true
            };
            return new HttpClient(handler);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }
    }
}