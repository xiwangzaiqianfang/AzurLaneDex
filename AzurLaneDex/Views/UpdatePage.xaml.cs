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
using System.Xml.Linq;

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
        private LanzouService? _lanzouService;

        // GitHub Pages MSIX 地址
        private const string GitHubPagesMsixUrl = "https://xiwangzaiqianfang.github.io/AzurLaneDex/Release/AzurLaneDex.msixbundle";
        
        // version.json 地址
        private const string AppVersionJsonRawUrl = "https://raw.githubusercontent.com/xiwangzaiqianfang/AzurLaneDex/main/version.json";
        private const string AppVersionJsonCdnUrl = "https://cdn.jsdelivr.net/gh/xiwangzaiqianfang/AzurLaneDex@main/version.json";

        // 蓝奏云文件夹链接与密码（硬编码）
        private const string LanzouFolderUrl = "https://wwaqf.lanzout.com/b0066z4gcb";
        private const string LanzouFolderPwd = "gzjf";

        // 舰船数据硬编码地址
        private const string DataGitHubRawUrl = "https://raw.githubusercontent.com/xiwangzaiqianfang/AzurLaneDex/main/AzurLaneDex/Assets/ships_static.json";
        private const string DataGitHubCdnUrl = "https://cdn.jsdelivr.net/gh/xiwangzaiqianfang/AzurLaneDex@main/AzurLaneDex/Assets/ships_static.json";

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

            // 从配置恢复自定义数据 URL（如果用户设过）
            var config = _shipManager.Config;
            if (config != null)
            {
                if (config.TryGetValue("data_custom_url", out var dc) && dc is string dUrl)
                    DataCustomUrlBox.Text = dUrl;
            }

            DownloadAppButton.Visibility = Visibility.Collapsed;
        }

        // 应用更新源选择变化（无需要操作的控件，但清空状态）
        private void AppDataSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AppDataSourceCombo == null) return;
            _latestAppVersion = null;
            _latestAppDownloadUrl = null;
            DownloadAppButton.Visibility = Visibility.Collapsed;
        }

        // 检查应用更新
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
                else // 蓝奏云（硬编码）
                {
                    _lanzouService = new LanzouService(ProxyBox.Text.Trim());
                    var files = await _lanzouService.GetFileListAsync(LanzouFolderUrl, LanzouFolderPwd);

                    var zipFiles = files
                        .Where(f => f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => f.Name)
                        .ToList();

                    if (zipFiles.Count == 0)
                    {
                        StatusText.Text = "未找到安装包（.zip）文件";
                        return;
                    }

                    var latestFile = zipFiles.First();
                    _latestAppVersion = ExtractVersionFromFileName(latestFile.Name) ?? latestFile.Time;
                    string fileUrl = "https://www.lanzouo.com/" + latestFile.Id;
                    _latestAppDownloadUrl = await _lanzouService.GetDirectLinkAsync(fileUrl, latestFile.Id);
                }

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

        // 下载并安装应用更新
        private async void DownloadApp_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_latestAppDownloadUrl))
            {
                StatusText.Text = "下载链接无效";
                return;
            }

            StatusText.Text = "正在下载更新包...";
            try
            {
                string tempDir = Path.GetTempPath();
                string fileName = Path.GetFileName(new Uri(_latestAppDownloadUrl).AbsolutePath);
                string downloadPath = Path.Combine(tempDir, fileName);

                using var client = CreateHttpClient(ProxyBox.Text.Trim());
                var response = await client.GetAsync(_latestAppDownloadUrl);
                response.EnsureSuccessStatusCode();
                using (var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }

                // 蓝奏云下载的是 .zip，重命名为 .msix
                if (AppDataSourceCombo.SelectedIndex == 1 && downloadPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    string finalPath = Path.ChangeExtension(downloadPath, ".msixbundle");
                    File.Move(downloadPath, finalPath);
                    downloadPath = finalPath;
                }

                StatusText.Text = "下载完成，正在启动安装...";
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
            }
        }

        // 数据更新源切换
        private void DataDataSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataCustomUrlBox == null) return;
            DataCustomUrlBox.Visibility = DataDataSourceCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        // 检查数据更新
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

        // 下载数据更新
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
                2 => DataCustomUrlBox.Text.Trim(),
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