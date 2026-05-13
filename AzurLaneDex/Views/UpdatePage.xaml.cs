using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace AzurLaneDex.Views
{
    public sealed partial class UpdatePage : Page
    {
        private ShipManager _shipManager;
        private string _currentAppVersion;
        private string? _latestAppVersion;
        private string? _latestAppDownloadUrl;
        private string? _latestDataUrl;
        private string? _remoteDataVersion;

        // GitHub Pages MSIX 地址
        private const string GitHubPagesMsixUrl = "https://xiwangzaiqianfang.github.io/AzurLaneDex/Release/AzurLaneDex.msixbundle";

        // version.json 地址 (GitHub)
        private const string AppVersionJsonRawUrl = "https://raw.githubusercontent.com/xiwangzaiqianfang/AzurLaneDex/main/version.json";
        private const string AppVersionJsonCdnUrl = "https://cdn.jsdelivr.net/gh/xiwangzaiqianfang/AzurLaneDex@main/version.json";

        // Gitee Pages 配置（请替换为你的实际 Pages 地址和文件名）\
        private const string GiteeRawBaseUrl = "https://gitee.com/fmlg/AzurLaneDex/raw/main/";
        private const string GiteeVersionJsonUrl = "https://gitee.com/fmlg/AzurLaneDex/raw/main/version.json";
        private const string GiteeInstallerBaseUrl = "https://gitee.com/fmlg/AzurLaneDex/releases/download/{version}/AzurLaneDex_{version}.msixbundle";

        // 舰船数据硬编码地址
        private const string DataGitHubRawUrl = "https://raw.githubusercontent.com/xiwangzaiqianfang/AzurLaneDex/main/AzurLaneDex/Assets/ships_static.json";
        private const string DataGitHubCdnUrl = "https://cdn.jsdelivr.net/gh/xiwangzaiqianfang/AzurLaneDex@main/AzurLaneDex/Assets/ships_static.json";
        private const string DataGiteeRawUrl = "https://gitee.com/fmlg/AzurLaneDex/raw/main/AzurLaneDex/Assets/ships_static.json";

        public UpdatePage()
        {
            this.InitializeComponent();
            Loaded += UpdatePage_Loaded;
        }

        private void UpdatePage_Loaded(object sender, RoutedEventArgs e)
        {
            var app = (App)Application.Current;
            _shipManager = app.ShipManager;
            _currentAppVersion = _shipManager.GetCurrentAppVersion();
            CurrentVersionText.Text = _currentAppVersion;

            // 从配置恢复自定义数据 URL
            var config = _shipManager.Config;
            if (config != null && config.TryGetValue("data_custom_url", out var dc) && dc is string dUrl)
                DataCustomUrlBox.Text = dUrl;

            DownloadAppButton.Visibility = Visibility.Collapsed;
        }

        private void AppDataSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _latestAppVersion = null;
            _latestAppDownloadUrl = null;
            DownloadAppButton.Visibility = Visibility.Collapsed;
        }

        private async void CheckAppUpdate_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "正在检查应用更新...";
            DownloadAppButton.Visibility = Visibility.Collapsed;
            _latestAppVersion = null;
            _latestAppDownloadUrl = null;

            try
            {
                if (AppDataSourceCombo.SelectedIndex == 0) // GitHub Pages
                {
                    string versionUrl = DataDataSourceCombo.SelectedIndex == 1
                        ? AppVersionJsonCdnUrl
                        : AppVersionJsonRawUrl;

                    using var client = CreateHttpClient(ProxyBox.Text.Trim());
                    string json = await client.GetStringAsync(versionUrl);
                    using var doc = JsonDocument.Parse(json);
                    string? remoteVersion = doc.RootElement.GetProperty("version").GetString();

                    if (string.IsNullOrEmpty(remoteVersion))
                    {
                        StatusText.Text = "version.json 格式错误";
                        return;
                    }

                    _latestAppVersion = remoteVersion;
                    _latestAppDownloadUrl = GitHubPagesMsixUrl;
                }
                else if (AppDataSourceCombo.SelectedIndex == 1) // Gitee (Raw + Releases)
                {
                    using var client = CreateHttpClient(ProxyBox.Text.Trim());
                    string json = await client.GetStringAsync(GiteeVersionJsonUrl);
                    using var doc = JsonDocument.Parse(json);

                    string? remoteVersion = doc.RootElement.GetProperty("version").GetString();
                    if (string.IsNullOrEmpty(remoteVersion))
                    {
                        StatusText.Text = "Gitee version.json 格式错误：缺少 version 字段";
                        return;
                    }

                    _latestAppVersion = remoteVersion;
                    // 拼接下载链接，将 {version} 替换为实际版本号
                    _latestAppDownloadUrl = GiteeInstallerBaseUrl.Replace("{version}", _latestAppVersion);
                }

                // 后续版本比较及显示逻辑（与 GitHub 共用，无需修改）
                if (!string.IsNullOrEmpty(_latestAppVersion) && !string.IsNullOrEmpty(_latestAppDownloadUrl))
                {
                    if (CompareVersion(_latestAppVersion, _currentAppVersion) > 0)
                    {
                        StatusText.Text = $"发现新版本 {_latestAppVersion}，点击「下载并安装」更新。";
                        DownloadAppButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        StatusText.Text = "当前已是最新版本。";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"检查失败: {ex.Message}";
            }
        }

        private async void DownloadApp_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_latestAppDownloadUrl))
            {
                StatusText.Text = "下载链接无效";
                return;
            }
            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadProgressBar.IsIndeterminate = false;
            DownloadStatusText.Text = "正在下载...";
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
                    StatusText.Text = "下载失败";
                    return;
                }

                StatusText.Text = "下载完成，正在启动安装...";
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
                StatusText.Text = $"下载失败: {ex.Message}";
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
            DataCustomUrlBox.Visibility = DataDataSourceCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void CheckDataUpdate_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "正在检查数据版本...";
            try
            {
                string url = GetDataUrl();
                if (string.IsNullOrEmpty(url))
                {
                    StatusText.Text = "请填写有效的自定义 URL";
                    return;
                }

                _remoteDataVersion = await _shipManager.GetRemoteDataVersionAsync(url, ProxyBox.Text.Trim());
                _latestDataUrl = url;

                if (string.IsNullOrEmpty(_remoteDataVersion))
                {
                    StatusText.Text = "无法获取远程数据版本";
                    return;
                }

                if (CompareVersion(_remoteDataVersion, _shipManager.Version) > 0)
                {
                    StatusText.Text = $"发现新数据版本 {_remoteDataVersion}，点击「下载并安装」更新。";
                }
                else
                {
                    StatusText.Text = "数据已是最新版本。";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"检查失败: {ex.Message}";
            }
        }

        private async void DownloadData_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_latestDataUrl))
            {
                StatusText.Text = "请先检查更新";
                return;
            }

            StatusText.Text = "正在下载数据...";
            try
            {
                bool success = await _shipManager.UpdateDataFromUrlAsync(_latestDataUrl, ProxyBox.Text.Trim());
                if (success)
                    StatusText.Text = $"数据已更新至版本 {_remoteDataVersion}。";
                else
                    StatusText.Text = "数据更新失败，请检查网络或 URL。";
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

        private string? ExtractVersionFromFileName(string fileName)
        {
            var match = Regex.Match(fileName, @"(\d+\.\d+\.\d+\.\d+)");
            return match.Success ? match.Value : null;
        }

        private int CompareVersion(string versionA, string versionB)
        {
            if (Version.TryParse(versionA, out Version? vA) && Version.TryParse(versionB, out Version? vB))
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