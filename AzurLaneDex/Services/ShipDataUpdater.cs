using AzurLaneDex.Services.Interfaces;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// 静态数据库（ships_static.db）更新器：HEAD 取远端大小/时间；下载到临时文件后
    /// 校验 SQLite 文件头，备份并原子替换数据目录中的库文件，再触发热重载。
    /// </summary>
    public class ShipDataUpdater : IShipDataUpdater
    {
        /// <summary>SQLite 3 文件头（16 字节）。</summary>
        private static readonly byte[] SqliteHeader =
            "SQLite format 3\u0000"u8.ToArray();

        private static HttpClient CreateHttpClient(string proxy)
        {
            if (string.IsNullOrEmpty(proxy)) return new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            var handler = new HttpClientHandler
            {
                Proxy = new System.Net.WebProxy(proxy),
                UseProxy = true
            };
            return new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async Task<RemoteDataInfo?> GetRemoteDataInfoAsync(string url, string proxy = "")
        {
            try
            {
                using var client = CreateHttpClient(proxy);
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) return null;

                long size = response.Content.Headers.ContentLength ?? 0;
                return new RemoteDataInfo
                {
                    Size = size,
                    LastModified = response.Content.Headers.LastModified
                };
            }
            catch (Exception ex)
            {
                LogService.Warning("读取远端数据库信息失败：" + ex.Message, nameof(ShipDataUpdater));
                return null;
            }
        }

        public async Task<bool> DownloadAndApplyDatabaseAsync(string url, string proxy, Func<Task> onApplied)
        {
            string target = Path.Combine(App.DataRoot, "ships_static.db");
            string temp = target + ".download";
            string backup = target + ".bak";

            try
            {
                using (var client = CreateHttpClient(proxy))
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        LogService.Warning($"数据库下载失败：HTTP {(int)response.StatusCode}", nameof(ShipDataUpdater));
                        return false;
                    }

                    await using var source = await response.Content.ReadAsStreamAsync();
                    await using (var dest = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                        await source.CopyToAsync(dest);
                }

                if (!IsSqliteFile(temp))
                {
                    LogService.Warning("下载的数据库文件头不合法（不是 SQLite 数据库）", nameof(ShipDataUpdater));
                    TryDelete(temp);
                    return false;
                }

                // 备份现有库，再原子替换（同目录 Move 覆盖）
                if (File.Exists(target))
                {
                    try { File.Copy(target, backup, overwrite: true); } catch { /* 备份失败不阻断 */ }
                }
                File.Move(temp, target, overwrite: true);
                LogService.Operation("数据更新", $"数据库已更新：{target}（{new FileInfo(target).Length / 1024 / 1024} MB）");

                if (onApplied != null) await onApplied();
                return true;
            }
            catch (Exception ex)
            {
                LogService.Warning("数据库更新失败：" + ex.Message, nameof(ShipDataUpdater));
                TryDelete(temp);
                return false;
            }
        }

        private static bool IsSqliteFile(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var header = new byte[SqliteHeader.Length];
                int read = fs.Read(header, 0, header.Length);
                if (read != SqliteHeader.Length) return false;
                for (int i = 0; i < header.Length; i++)
                    if (header[i] != SqliteHeader[i]) return false;
                return true;
            }
            catch { return false; }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* 忽略 */ }
        }
    }
}
