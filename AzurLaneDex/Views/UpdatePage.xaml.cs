using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzurLaneDex.Views
{
    public sealed partial class UpdatePage : Page
    {
        private ShipManager _shipManager;
        private string _currentAppVersion;
        private AppUpdateInfo _latestAppInfo;   // 应用更新信息（自定义源）
        private string _remoteDataVersion;

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

            // 应用更新源切换事件
            AppDataSourceCombo.SelectionChanged += AppDataSourceCombo_SelectionChanged;
            // 数据更新源切换事件
            DataDataSourceCombo.SelectionChanged += DataDataSourceCombo_SelectionChanged;

            // 初始隐藏按钮
            DownloadAppButton.Visibility = Visibility.Collapsed;
        }

        // 应用更新源：显示/隐藏自定义URL输入框
        private void AppDataSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AppCustomUrlBox.Visibility = AppDataSourceCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        // 数据更新源：显示/隐藏自定义URL输入框
        private void DataDataSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataCustomUrlBox.Visibility = DataDataSourceCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        // 获取应用更新使用的URL（根据选择）
        private string GetAppUpdateUrl()
        {
            if (AppDataSourceCombo.SelectedIndex == 0) // GitHub API
                return "https://api.github.com/repos/xiwangzaiqianfang/AzurLane-Dex/releases/latest";
            else
                return AppCustomUrlBox.Text.Trim();
        }

        // 获取代理地址（数据更新和自定义应用源均可共用）
        private string GetProxy() => ProxyBox.Text.Trim();

        // 检查应用更新
        private async void CheckAppUpdate_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "正在检查应用更新...";
            DownloadAppButton.Visibility = Visibility.Collapsed;
            _latestAppInfo = null;

            try
            {
                int selectedSource = AppDataSourceCombo.SelectedIndex;
                if (selectedSource == 0) // GitHub API
                {
                    string latestVersion = await _shipManager.GetLatestAppVersionAsync(GetProxy());
                    if (string.IsNullOrEmpty(latestVersion))
                        throw new Exception("无法获取最新版本信息");

                    if (CompareVersion(latestVersion, _currentAppVersion) > 0)
                    {
                        StatusText.Text = $"发现新版本 {latestVersion}，请点击下载按钮跳转到 GitHub 获取安装包。";
                        // 构造一个伪信息，用于下载按钮打开浏览器
                        _latestAppInfo = new AppUpdateInfo
                        {
                            Version = latestVersion,
                            IsGitHubRelease = true,
                            DownloadUrl = null
                        };
                        DownloadAppButton.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        StatusText.Text = "当前已是最新版本。";
                    }
                }
                else // 自定义 update.json
                {
                    string url = GetAppUpdateUrl();
                    if (string.IsNullOrEmpty(url))
                    {
                        StatusText.Text = "请填写自定义更新源 URL。";
                        return;
                    }

                    string json = await _shipManager.DownloadStringAsync(url, GetProxy());
                    var info = JsonSerializer.Deserialize<AppUpdateInfo>(json);
                    if (info == null || string.IsNullOrEmpty(info.Version))
                        throw new Exception("自定义源返回的数据格式无效");

                    _latestAppInfo = info;
                    if (CompareVersion(info.Version, _currentAppVersion) > 0)
                    {
                        string changelog = string.IsNullOrEmpty(info.Changelog) ? "" : $"\n更新内容：{info.Changelog}";
                        StatusText.Text = $"发现新版本 {info.Version}{changelog}";
                        DownloadAppButton.Visibility = string.IsNullOrEmpty(info.DownloadUrl) && !info.IsGitHubRelease
                            ? Visibility.Collapsed
                            : Visibility.Visible;
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

        // 下载/安装应用更新
        private async void DownloadApp_Click(object sender, RoutedEventArgs e)
        {
            if (_latestAppInfo == null)
            {
                StatusText.Text = "未找到更新信息，请先检查更新。";
                return;
            }

            // GitHub Release 跳转浏览器
            if (_latestAppInfo.IsGitHubRelease)
            {
                var uri = new Uri("https://github.com/xiwangzaiqianfang/AzurLane-Dex/releases/latest");
                await Windows.System.Launcher.LaunchUriAsync(uri);
                StatusText.Text = "已打开浏览器，请手动下载安装包。";
                return;
            }

            // 自定义源：尝试下载 MSIX
            string downloadUrl = _latestAppInfo.DownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                StatusText.Text = "下载链接无效，请检查 update.json 中的 DownloadUrl 字段。";
                return;
            }

            StatusText.Text = "正在下载更新包...";
            try
            {
                string downloadPath = Path.Combine(Path.GetTempPath(), "AzurLaneDex_Update.msix");
                using var client = _shipManager.CreateHttpClient(GetProxy());
                using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using (var fs = new FileStream(downloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }
                }

                StatusText.Text = "下载完成，正在启动安装...";
                var psi = new ProcessStartInfo
                {
                    FileName = downloadPath,
                    UseShellExecute = true
                };
                Process.Start(psi);
                // 可选延迟退出，让安装程序有机会启动
                await Task.Delay(1000);
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                StatusText.Text = $"下载失败: {ex.Message}";
            }
        }

        // 获取数据更新 URL
        private string GetDataUrl()
        {
            return DataDataSourceCombo.SelectedIndex switch
            {
                0 => "https://raw.githubusercontent.com/xiwangzaiqianfang/AzurLane-Dex/main/data/static/ships_static.json",
                1 => "https://cdn.jsdelivr.net/gh/xiwangzaiqianfang/AzurLane-Dex@main/data/static/ships_static.json",
                2 => DataCustomUrlBox.Text.Trim(),
                _ => ""
            };
        }

        // 检查数据更新
        private async void CheckDataUpdate_Click(object sender, RoutedEventArgs e)
        {
            string url = GetDataUrl();
            if (string.IsNullOrEmpty(url))
            {
                StatusText.Text = "请填写有效的数据源 URL。";
                return;
            }

            StatusText.Text = "正在检查数据版本...";
            try
            {
                _remoteDataVersion = await _shipManager.GetRemoteDataVersionAsync(url, GetProxy());
                if (string.IsNullOrEmpty(_remoteDataVersion))
                    throw new Exception("无法获取远程数据版本");

                string currentVersion = _shipManager.Version;
                if (CompareVersion(_remoteDataVersion, currentVersion) > 0)
                {
                    StatusText.Text = $"发现新数据版本 {_remoteDataVersion}，当前版本 {currentVersion}。点击「下载并安装」更新。";
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
            string url = GetDataUrl();
            if (string.IsNullOrEmpty(url))
            {
                StatusText.Text = "请填写有效的数据源 URL。";
                return;
            }

            StatusText.Text = "正在下载数据...";
            try
            {
                bool success = await _shipManager.UpdateDataFromUrlAsync(url, GetProxy());
                if (success)
                {
                    StatusText.Text = "数据更新成功，请重启应用或切换页面以生效。";
                    // 刷新显示当前数据版本
                    var newVersion = _shipManager.Version;
                    if (CompareVersion(_remoteDataVersion, newVersion) == 0)
                        StatusText.Text = $"数据已更新至版本 {newVersion}。";
                    else
                        StatusText.Text = "数据更新完成，但版本号可能未同步。";
                }
                else
                {
                    StatusText.Text = "数据更新失败，请检查 URL 或网络。";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"下载失败: {ex.Message}";
            }
        }

        // 返回按钮
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        // 版本比较（支持 x.y.z 格式，忽略第四段）
        private int CompareVersion(string versionA, string versionB)
        {
            if (Version.TryParse(versionA, out Version vA) && Version.TryParse(versionB, out Version vB))
                return vA.CompareTo(vB);
            // 降级为字符串比较
            return string.Compare(versionA, versionB, StringComparison.OrdinalIgnoreCase);
        }
    }

    // 自定义更新源的数据结构（用于应用更新）
    public class AppUpdateInfo
    {
        public string Version { get; set; }
        public string Changelog { get; set; }
        public string DownloadUrl { get; set; }
        public bool IsGitHubRelease { get; set; }
    }
}