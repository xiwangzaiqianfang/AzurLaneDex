using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzurLaneDex.Services;

public class AssetDownloadService
{
    // 远程版本清单文件的URL
    private const string RemoteManifestUrl = "https://your-server.com/azurlane-assets/assets.manifest";
    // 远程资源包的URL模板
    private const string RemoteAssetPackageUrlTemplate = "https://your-server.com/azurlane-assets/{faction}.zip";

    private readonly HttpClient _httpClient;
    public AssetDownloadService()
    {
        _httpClient = new HttpClient();
        // 可以在这里设置超时等属性
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 获取远程资源版本清单
    /// </summary>
    public async Task<AssetManifest?> GetRemoteManifestAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync(RemoteManifestUrl);
            return JsonSerializer.Deserialize<AssetManifest>(response);
        }
        catch (HttpRequestException ex)
        {
            // 将异常重新抛出以便上层捕获
            throw new HttpRequestException("获取远程清单失败", ex);
        }
        catch (Exception ex)
        {
            // 其他异常也重新抛出
            throw new Exception("下载清单时发生未知错误", ex);
        }
    }

    /// <summary>
    /// 下载指定阵营的资源包到本地临时文件
    /// </summary>
    /// <param name="factionId">阵营ID</param>
    /// <param name="destinationPath">本地临时文件路径</param>
    /// <param name="progress">下载进度报告器</param>
    /// <returns>下载是否成功</returns>
    public async Task<bool> DownloadAssetPackageAsync(string factionId, string destinationPath, IProgress<double>? progress = null)
    {
        var url = RemoteAssetPackageUrlTemplate.Replace("{faction}", factionId);
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1 && progress != null;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalBytesRead += bytesRead;
                if (canReportProgress)
                {
                    progress?.Report((double)totalBytesRead / totalBytes * 100);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"下载阵营 {factionId} 资源失败: {ex.Message}");
            return false;
        }
    }
}

/// <summary>
/// 资源版本清单数据结构
/// </summary>
public class AssetManifest
{
    public Dictionary<string, string> FactionVersions { get; set; } = new();
}