using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace AzurLaneDex.Services;

public class AssetExtractService
{
    private readonly string _assetsRootPath;

    public AssetExtractService()
    {
        // 将头像存放在 App.DataRoot 下的 "avatars" 文件夹中
        _assetsRootPath = Path.Combine(App.DataRoot, "avatars");
        if (!Directory.Exists(_assetsRootPath))
            Directory.CreateDirectory(_assetsRootPath);
    }

    /// <summary>
    /// 获取阵营头像的本地存储路径
    /// </summary>
    public string GetAvatarDirectory(string factionId) => Path.Combine(_assetsRootPath, factionId);

    /// <summary>
    /// 检查某个阵营的头像目录是否存在且可能完整（这里只做简单检查）
    /// </summary>
    public bool IsFactionAvatarsExist(string factionId)
    {
        string dirPath = GetAvatarDirectory(factionId);
        return Directory.Exists(dirPath) && Directory.GetFiles(dirPath).Length > 0;
    }

    /// <summary>
    /// 解压下载的Zip包到指定的阵营文件夹
    /// </summary>
    /// <param name="zipFilePath">下载的Zip文件临时路径</param>
    /// <param name="factionId">阵营ID</param>
    /// <returns>是否成功</returns>
    public Task<bool> ExtractToFactionDirectoryAsync(string zipFilePath, string factionId)
    {
        return Task.Run(() =>
        {
            try
            {
                string targetDir = GetAvatarDirectory(factionId);
                if (Directory.Exists(targetDir))
                    Directory.Delete(targetDir, true);
                Directory.CreateDirectory(targetDir);

                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;
                        string destinationPath = Path.Combine(targetDir, entry.Name);
                        entry.ExtractToFile(destinationPath, true);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"解压阵营 {factionId} 资源失败: {ex.Message}");
                return false;
            }
        });
    }
}