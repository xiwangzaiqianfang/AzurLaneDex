using Microsoft.UI.Dispatching;
using System;
using System.Net.Http;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AzurLaneDex.Services;

public enum AvatarUpdateResult
{
    Success,          // 所有检查完成，无更新或更新成功
    NoUpdate,         // 没有需要更新的
    UpdateAvailable,  // 发现有可用的更新（但尚未下载）
    NetworkError,     // 网络连接失败
    ServerError,      // 服务器响应错误
    OtherError        // 其他错误
}

public class AvatarUpdateResultInfo
{
    public AvatarUpdateResult Result { get; set; }
    public string Message { get; set; } = "";
    public List<string> AvailableFactions { get; set; } = new();
}

public class FactionAvatarManager
{
    private readonly AssetDownloadService _downloadService;
    private readonly AssetExtractService _extractService;
    private readonly DispatcherQueue _dispatcherQueue;
    public event Action<string, string>? FactionAvatarUpdated;

    public FactionAvatarManager()
    {
        _downloadService = new AssetDownloadService();
        _extractService = new AssetExtractService();
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    /// <summary>
    /// 从本地或远程获取资源版本清单，决定需要更新哪些阵营。
    /// 具体实现取决于版本存储方式。
    /// </summary>
    private async Task<Dictionary<string, string>> GetLocalAssetVersions()
    {
        // 示例: 从本地配置文件读取已有阵营的版本号
        // 你可以选择在 App.DataRoot 下存储一个 versions.json
        return await Task.FromResult(new Dictionary<string, string>());
    }

    /// <summary>
    /// 核心方法：启动更新检查，下载所有需要更新的阵营资源
    /// </summary>
    /// <param name="progressCallback">用于汇报单个阵营下载进度的回调</param>
    public async Task<AvatarUpdateResultInfo> UpdateAllFactionAvatarsAsync(Action<string, double>? progressCallback = null)
    {
        var info = new AvatarUpdateResultInfo();
        try
        {
            var remoteManifest = await _downloadService.GetRemoteManifestAsync();
            if (remoteManifest == null)
            {
                info.Result = AvatarUpdateResult.ServerError;
                info.Message = "无法获取远程资源清单，请检查网络或服务器状态。";
                return info;
            }

            var localVersions = await GetLocalAssetVersions();
            var needUpdateFactions = new List<string>();

            foreach (var kvp in remoteManifest.FactionVersions)
            {
                string factionId = kvp.Key;
                string remoteVersion = kvp.Value;
                bool needUpdate = !_extractService.IsFactionAvatarsExist(factionId) ||
                                  !localVersions.TryGetValue(factionId, out var localVersion) ||
                                  localVersion != remoteVersion;

                if (needUpdate)
                {
                    await DownloadAndExtractFactionAsync(factionId, remoteVersion, progressCallback);
                }
            }
            if (needUpdateFactions.Count == 0)
            {
                info.Result = AvatarUpdateResult.NoUpdate;
                info.Message = "所有头像已是最新。";
                return info;
            }

            info.Result = AvatarUpdateResult.UpdateAvailable;
            info.AvailableFactions = needUpdateFactions;
            info.Message = $"发现 {needUpdateFactions.Count} 个阵营的头像更新。";
            return info;
        }
        catch (HttpRequestException ex) when (ex.InnerException is System.Net.Sockets.SocketException)
        {
            info.Result = AvatarUpdateResult.NetworkError;
            info.Message = "网络连接失败，请检查网络设置后重试。";
            LogService.Error($"头像更新网络错误: {ex.Message}", "FactionAvatarManager", ex);
            return info;
        }
        catch (Exception ex)
        {
            info.Result = AvatarUpdateResult.OtherError;
            info.Message = $"头像更新失败：{ex.Message}";
            LogService.Error($"头像更新未知错误", "FactionAvatarManager", ex);
            return info;
        }
    }

    private async Task DownloadAndExtractFactionAsync(string factionId, string newVersion, Action<string, double>? progressCallback = null)
    {
        string tempZipPath = Path.GetTempFileName();
        try
        {
            var progress = new Progress<double>(p => progressCallback?.Invoke(factionId, p));
            bool downloadSuccess = await _downloadService.DownloadAssetPackageAsync(factionId, tempZipPath, progress);
            if (!downloadSuccess) return;

            bool extractSuccess = await _extractService.ExtractToFactionDirectoryAsync(tempZipPath, factionId);
            if (extractSuccess)
            {
                // 下载并解压成功后，保存新版本号，并通知UI更新
                await SaveLocalVersionAsync(factionId, newVersion);
                _dispatcherQueue.TryEnqueue(() =>
                {
                    FactionAvatarUpdated?.Invoke(factionId, newVersion);
                });
            }
        }
        finally
        {
            if (File.Exists(tempZipPath))
                File.Delete(tempZipPath);
        }
    }

    private Task SaveLocalVersionAsync(string factionId, string version)
    {
        // 同样，将版本号写入本地
        return Task.CompletedTask;
    }

    /// <summary>
    /// 供外部调用的手动更新方法
    /// </summary>
    public async Task<bool> UpdateSpecificFactionAvatarsAsync(string factionId, IProgress<double>? progress = null)
    {
        var remoteManifest = await _downloadService.GetRemoteManifestAsync();
        if (remoteManifest == null || !remoteManifest.FactionVersions.ContainsKey(factionId))
            return false;

        string tempZipPath = Path.GetTempFileName();
        try
        {
            bool downloadSuccess = await _downloadService.DownloadAssetPackageAsync(factionId, tempZipPath, progress);
            if (!downloadSuccess) return false;

            return await _extractService.ExtractToFactionDirectoryAsync(tempZipPath, factionId);
        }
        finally
        {
            if (File.Exists(tempZipPath))
                File.Delete(tempZipPath);
        }
    }
    public async Task<bool> DownloadFactionUpdatesAsync(List<string> factionIds, Action<string, double>? progressCallback = null)
    {
        var remoteManifest = await _downloadService.GetRemoteManifestAsync();
        if (remoteManifest == null) return false;

        foreach (var factionId in factionIds)
        {
            if (remoteManifest.FactionVersions.TryGetValue(factionId, out var version))
            {
                await DownloadAndExtractFactionAsync(factionId, version, progressCallback);
            }
        }
        return true;
    }
}