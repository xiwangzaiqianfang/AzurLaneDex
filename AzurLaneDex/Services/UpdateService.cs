using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AzurLaneDex.Models;
using AzurLaneDex.Services.Interfaces;

namespace AzurLaneDex.Services
{
    public class UpdateService : IUpdateService
    {
        private readonly ShipManager _shipManager;
        private readonly HttpClient _httpClient;

        public UpdateService(ShipManager shipManager)
        {
            _shipManager = shipManager ?? throw new ArgumentNullException(nameof(shipManager));
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public UpdateChannel CurrentChannel
        {
            get
            {
                if (_shipManager?.Config == null)
                    return UpdateChannel.Stable;

                if (_shipManager.Config.TryGetValue("update_channel", out var obj) &&
                    obj is string channelStr &&
                    Enum.TryParse<UpdateChannel>(channelStr, out var channel))
                {
                    return channel;
                }
                return UpdateChannel.Stable;
            }
            set
            {
                if (_shipManager?.Config != null)
                {
                    _shipManager.Config["update_channel"] = value.ToString();
                    _shipManager.SaveConfig();
                }
            }
        }

        public UpdateSource CurrentSource
        {
            get
            {
                if (_shipManager?.Config == null)
                    return UpdateSource.GitHub;

                if (_shipManager.Config.TryGetValue("update_source", out var obj) &&
                    obj is string srcStr &&
                    Enum.TryParse<UpdateSource>(srcStr, out var source))
                {
                    return source;
                }
                return UpdateSource.GitHub;
            }
            set
            {
                if (_shipManager?.Config != null)
                {
                    _shipManager.Config["update_source"] = value.ToString();
                    _shipManager.SaveConfig();
                }
            }
        }

        public string CurrentAppVersion => _shipManager?.GetCurrentAppVersion() ?? "0.0.0.0";

        public async Task<UpdateInfo> CheckForUpdateAsync()
        {
            var channel = CurrentChannel;
            // 测试版强制使用 GitHub
            var source = (channel == UpdateChannel.Stable) ? CurrentSource : UpdateSource.GitHub;
            string versionUrl = GetVersionUrl(channel, source);

            try
            {
                var json = await _httpClient.GetStringAsync(versionUrl);
                var data = JsonSerializer.Deserialize<VersionInfo>(json);
                if (data == null || string.IsNullOrEmpty(data.Version))
                {
                    return new UpdateInfo { HasUpdate = false, Error = "无法解析版本信息" };
                }

                bool hasUpdate = CompareVersion(data.Version, CurrentAppVersion) > 0;
                string downloadUrl = string.IsNullOrEmpty(data.DownloadUrl)
                    ? GetDownloadUrl(channel, source, data.Version)
                    : data.DownloadUrl;

                return new UpdateInfo
                {
                    HasUpdate = hasUpdate,
                    LatestVersion = data.Version,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = data.ReleaseNotes ?? "",
                    Channel = channel
                };
            }
            catch (HttpRequestException ex)
            {
                LogService.Error($"检查更新网络请求失败: {ex.Message}", nameof(UpdateService), ex);
                return new UpdateInfo { HasUpdate = false, Error = $"网络错误: {ex.Message}" };
            }
            catch (Exception ex)
            {
                LogService.Error($"检查更新失败: {ex.Message}", nameof(UpdateService), ex);
                return new UpdateInfo { HasUpdate = false, Error = $"检查更新失败: {ex.Message}" };
            }
        }

        public async Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo update, IProgress<double> progress)
        {
            if (update == null || string.IsNullOrEmpty(update.DownloadUrl))
                return false;

            try
            {
                string tempDir = System.IO.Path.GetTempPath();
                string fileName = System.IO.Path.GetFileName(new Uri(update.DownloadUrl).AbsolutePath);
                if (string.IsNullOrEmpty(fileName))
                    fileName = "AzurLaneDex.msixbundle";
                string downloadPath = System.IO.Path.Combine(tempDir, fileName);

                using var response = await _httpClient.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new System.IO.FileStream(downloadPath, System.IO.FileMode.Create, System.IO.FileAccess.Write);

                byte[] buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        double percent = (double)totalRead / totalBytes.Value * 100;
                        progress?.Report(percent);
                    }
                    else
                    {
                        progress?.Report(-1);
                    }
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = downloadPath,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                await Task.Delay(2000);
                Windows.ApplicationModel.Core.CoreApplication.Exit();
                return true;
            }
            catch (Exception ex)
            {
                LogService.Error($"下载并安装更新失败: {ex.Message}", nameof(UpdateService), ex);
                return false;
            }
        }

        private string GetVersionUrl(UpdateChannel channel, UpdateSource source)
        {
            if (channel != UpdateChannel.Stable)
                source = UpdateSource.GitHub;

            return source switch
            {
                UpdateSource.GitHub => "https://raw.githubusercontent.com/xiwangzaiqianfang/AzurLaneDex/main/version.json",
                UpdateSource.Gitee => "https://gitee.com/fmlg/AzurLaneDex/raw/main/version.json",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private string GetDownloadUrl(UpdateChannel channel, UpdateSource source, string version)
        {
            if (channel != UpdateChannel.Stable)
                source = UpdateSource.GitHub;

            return source switch
            {
                UpdateSource.GitHub => $"https://github.com/xiwangzaiqianfang/AzurLaneDex/releases/download/{version}/AzurLaneDex.msixbundle",
                UpdateSource.Gitee => $"https://gitee.com/fmlg/AzurLaneDex/releases/download/{version}/AzurLaneDex_{version}.msixbundle",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private int CompareVersion(string a, string b)
        {
            if (Version.TryParse(a, out var va) && Version.TryParse(b, out var vb))
                return va.CompareTo(vb);
            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private class VersionInfo
        {
            public string Version { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public string ReleaseNotes { get; set; } = "";
        }
    }
}