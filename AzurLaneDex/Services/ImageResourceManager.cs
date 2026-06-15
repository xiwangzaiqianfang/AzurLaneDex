using AzurLaneDex.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzurLaneDex.Services
{
    public class ImageResourceManager
    {
        private readonly string _imageBasePath;
        private readonly string _manifestPath;
        private LocalResourceManifest _localManifest;

        public ImageResourceManager(string dataRoot)
        {
            _imageBasePath = Path.Combine(dataRoot, "images", "ship");
            _manifestPath = Path.Combine(_imageBasePath, "manifest.json");
            Directory.CreateDirectory(_imageBasePath);
            LoadLocalManifest();
        }

        private void LoadLocalManifest()
        {
            if (File.Exists(_manifestPath))
            {
                var json = File.ReadAllText(_manifestPath);
                _localManifest = JsonSerializer.Deserialize<LocalResourceManifest>(json) ?? new LocalResourceManifest();
            }
            else
                _localManifest = new LocalResourceManifest();
        }

        private void SaveLocalManifest()
        {
            var json = JsonSerializer.Serialize(_localManifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_manifestPath, json);
        }

        public bool NeedDownload() => _localManifest.Packages.Count == 0;

        public async Task<RemoteResourceManifest> GetRemoteManifestAsync(string baseUrl, string proxy = "")
        {
            string manifestUrl = baseUrl.TrimEnd('/') + "/resource_manifest.json";
            var client = CreateHttpClient(proxy);
            var json = await client.GetStringAsync(manifestUrl);
            return JsonSerializer.Deserialize<RemoteResourceManifest>(json);
        }

        public async Task<bool> DownloadPackageAsync(RemotePackage pkg, IProgress<double> progress, string proxy = "")
        {
            string tempZip = Path.GetTempFileName();
            try
            {
                using var client = CreateHttpClient(proxy);
                using var response = await client.GetAsync(pkg.url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                using (var fs = new FileStream(tempZip, FileMode.Create))
                using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[8192];
                    long read = 0;
                    int bytes;
                    while ((bytes = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fs.WriteAsync(buffer, 0, bytes);
                        read += bytes;
                        if (total.HasValue)
                            progress?.Report((double)read / total.Value * 100);
                        else
                            progress?.Report(-1);
                    }
                }

                string targetDir = Path.Combine(_imageBasePath, pkg.name);
                Directory.CreateDirectory(targetDir);
                ZipFile.ExtractToDirectory(tempZip, targetDir, true);

                _localManifest.Packages[pkg.name] = pkg.md5;
                SaveLocalManifest();
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
            }
        }

        private HttpClient CreateHttpClient(string proxy)
        {
            if (string.IsNullOrEmpty(proxy)) return new HttpClient();
            var handler = new HttpClientHandler
            {
                Proxy = new System.Net.WebProxy(proxy),
                UseProxy = true
            };
            return new HttpClient(handler);
        }

        public string GetLocalImagePath(int factionId, string imageName)
        {
            string folder = factionId.ToString();
            string path = Path.Combine(_imageBasePath, folder, imageName);
            if (File.Exists(path)) return path;
            string pngPath = Path.ChangeExtension(path, ".png");
            if (File.Exists(pngPath)) return pngPath;
            return null;
        }

        public void MigrateLegacyAvatarFolders()
        {
            var mapping = new Dictionary<string, int>
            {
                ["白鹰"] = (int)Faction.EagleUnion,
                ["皇家"] = (int)Faction.RoyalNavy,
                ["重樱"] = (int)Faction.SakuraEmpire,
                ["铁血"] = (int)Faction.IronBlood,
                ["东煌"] = (int)Faction.DragonEmpery,
                ["撒丁帝国"] = (int)Faction.Sardegna,
                ["北方联合"] = (int)Faction.NorthernUnion,
                ["自由鸢尾"] = (int)Faction.FreeFrench,
                ["维希教廷"] = (int)Faction.Vichya,
                ["郁金王国"] = (int)Faction.Tulip,
                ["飓风"] = (int)Faction.Tempesta,
                ["其他"] = (int)Faction.Other,
                ["超次元游戏海王星"] = (int)Faction.Collab_Nep,
                ["哔哩哔哩"] = (int)Faction.Collab_Bilibili,
            };

            foreach (var kv in mapping)
            {
                string oldPath = Path.Combine(_imageBasePath, kv.Key);
                string newPath = Path.Combine(_imageBasePath, kv.Value.ToString());
                if (Directory.Exists(oldPath) && !Directory.Exists(newPath))
                {
                    try
                    {
                        Directory.Move(oldPath, newPath);
                        LogService.Info($"迁移头像文件夹: {kv.Key} -> {kv.Value}", "ImageResourceManager");
                    }
                    catch (Exception ex)
                    {
                        LogService.Error($"迁移失败 {kv.Key}: {ex.Message}", "ImageResourceManager");
                    }
                }
            }
        }

        public List<string> GetDownloadedPackages() => new List<string>(_localManifest.Packages.Keys);
    }

    public class LocalResourceManifest
    {
        public Dictionary<string, string> Packages { get; set; } = new();
    }

    public class RemoteResourceManifest
    {
        public int Version { get; set; }
        public List<RemotePackage> Packages { get; set; }
    }

    public class RemotePackage
    {
        public string name { get; set; }
        public string url { get; set; }
        public long size { get; set; }
        public string md5 { get; set; }
    }
}