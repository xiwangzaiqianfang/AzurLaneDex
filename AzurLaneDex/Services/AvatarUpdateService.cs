using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// 头像更新服务：检查远端 manifest（GitHub → Gitee 依次尝试）、下载 ZIP 头像包、
    /// 解压到本地 avatars/ 目录并记录本地版本。压缩包内条目（如 squareicon/ 顶层目录）
    /// 会被剥离后映射到 avatars/ 根，与"Common/{Faction}/{GlobalName}{后缀}.png"等
    /// 存储规则一致（详见 docs/data_mapping.md）。
    /// </summary>
    public sealed class AvatarUpdateService
    {
        /// <summary>远端 manifest 默认地址（可用 Config["avatar_manifest_github"/"avatar_manifest_gitee"] 覆盖）。</summary>
        public const string DefaultManifestGitHub =
            "https://github.com/xiwangzaiqianfang/AzurLaneDex-Avatars/releases/latest/download/avatar_manifest.json";

        public const string DefaultManifestGitee =
            "https://gitee.com/xiwangzaiqianfang/AzurLaneDex-Avatars/releases/latest/download/avatar_manifest.json";

        /// <summary>下载/解压完成事件（参数=新版本号；可能在后台线程触发，UI 侧需自行调度）。</summary>
        public event Action<string>? UpdateCompleted;

        private const int RequestTimeoutSeconds = 15;

        // 不使用 static 共享 HttpClient：超时/取消后复用坏连接会抛出
        // "线程退出或应用程序请求，已中止 I/O 操作"(SocketError.OperationAborted)；
        // 每次请求失败后重建，避免坏连接污染后续请求。
        private HttpClient? _client;
        private readonly object _clientLock = new();
        private readonly CancellationTokenSource _lifetimeCts = new();
        private int _abortWarnLogged;

        private HttpClient Client
        {
            get
            {
                lock (_clientLock)
                {
                    if (_client == null)
                    {
                        _client = new HttpClient(new SocketsHttpHandler
                        {
                            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                            ConnectTimeout = TimeSpan.FromSeconds(10)
                        })
                        {
                            Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds)
                        };
                    }
                    return _client;
                }
            }
        }

        /// <summary>丢弃当前 HttpClient（连接可能已损坏），下次请求自动重建。</summary>
        private void ResetClient()
        {
            lock (_clientLock)
            {
                var old = _client;
                _client = null;
                old?.Dispose();
            }
        }

        /// <summary>应用退出时调用：取消所有进行中的网络请求（避免退出期 I/O 中止噪音）。</summary>
        public void CancelAll()
        {
            try { _lifetimeCts.Cancel(); } catch { /* 已取消 */ }
        }

        /// <summary>
        /// 判断异常是否为"连接被中止/取消/超时"类（SocketError.OperationAborted /
        /// ConnectionAborted / ConnectionReset / OperationCanceled / Timeout）。
        /// </summary>
        private static bool IsAbortLike(Exception ex)
        {
            for (Exception? cur = ex; cur != null; cur = cur.InnerException)
            {
                if (cur is OperationCanceledException) return true;
                if (cur is TimeoutException) return true;
                if (cur is System.Net.Sockets.SocketException se)
                {
                    switch (se.SocketErrorCode)
                    {
                        case System.Net.Sockets.SocketError.OperationAborted:
                        case System.Net.Sockets.SocketError.ConnectionAborted:
                        case System.Net.Sockets.SocketError.ConnectionReset:
                        case System.Net.Sockets.SocketError.TimedOut:
                        case System.Net.Sockets.SocketError.HostUnreachable:
                        case System.Net.Sockets.SocketError.NetworkUnreachable:
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>把中止类异常统一转换为取消/网络不可用语义（不抛给调用方）。</summary>
        private bool HandleNetworkError(Exception ex, string context)
        {
            if (_lifetimeCts.IsCancellationRequested)
                return true; // 应用退出/主动取消：静默

            if (IsAbortLike(ex))
            {
                ResetClient();
                if (Interlocked.Exchange(ref _abortWarnLogged, 1) == 0)
                    LogService.Warning($"头像网络请求中止/超时（已自动恢复，后续静默）: {context}", nameof(AvatarUpdateService));
                return true;
            }
            if (ex is HttpRequestException || ex is IOException)
            {
                ResetClient();
            }
            return false;
        }

        /// <summary>本地头像根目录。</summary>
        public string AvatarsRoot => Path.Combine(App.DataRoot, "avatars");

        private string VersionPath => Path.Combine(AvatarsRoot, "versions.json");

        /// <summary>远端 manifest 结构。</summary>
        public sealed class AvatarManifest
        {
            [JsonPropertyName("version")] public string Version { get; set; } = "";
            [JsonPropertyName("download_url")] public string DownloadUrl { get; set; } = "";
            [JsonPropertyName("fallback_url")] public string FallbackUrl { get; set; } = "";
        }

        private sealed class LocalVersionFile
        {
            [JsonPropertyName("version")] public string Version { get; set; } = "";
            [JsonPropertyName("updated_at")] public string UpdatedAt { get; set; } = "";
        }

        /// <summary>读取本地头像包版本；无记录/读取失败返回空串。</summary>
        public string GetLocalVersion()
        {
            try
            {
                if (!File.Exists(VersionPath)) return "";
                var json = File.ReadAllText(VersionPath);
                var data = JsonSerializer.Deserialize<LocalVersionFile>(json);
                return data?.Version ?? "";
            }
            catch { return ""; }
        }

        /// <summary>写入本地版本记录（UTF-8 无 BOM）。</summary>
        public void SaveLocalVersion(string version)
        {
            try
            {
                Directory.CreateDirectory(AvatarsRoot);
                var data = new LocalVersionFile
                {
                    Version = version ?? "",
                    UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
                };
                File.WriteAllText(VersionPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                LogService.Error("保存头像版本记录失败", nameof(AvatarUpdateService), ex);
            }
        }

        /// <summary>尝试从 GitHub/Gitee 获取最新 manifest；均失败返回 null（不抛；中止/超时类静默处理）。</summary>
        public async Task<AvatarManifest?> CheckForUpdateAsync(string? githubUrl = null, string? giteeUrl = null)
        {
            if (_lifetimeCts.IsCancellationRequested) return null;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            var ct = linked.Token;

            foreach (var url in new[] { githubUrl ?? DefaultManifestGitHub, giteeUrl ?? DefaultManifestGitee })
            {
                try
                {
                    using var resp = await Client.GetAsync(url, ct);
                    if (!resp.IsSuccessStatusCode) continue;
                    var json = await resp.Content.ReadAsStringAsync(ct);
                    var manifest = JsonSerializer.Deserialize<AvatarManifest>(json);
                    if (manifest != null && !string.IsNullOrEmpty(manifest.Version) && !string.IsNullOrEmpty(manifest.DownloadUrl))
                        return manifest;
                }
                catch (Exception ex) when (ex is OperationCanceledException || _lifetimeCts.IsCancellationRequested)
                {
                    return null; // 取消/退出：静默
                }
                catch (Exception ex)
                {
                    if (HandleNetworkError(ex, $"manifest {url}"))
                        return null; // 中止/超时类：静默返回（本次会话已记录一次）
                    LogService.Warning($"获取头像 manifest 失败（{url}）: {ex.Message}", nameof(AvatarUpdateService));
                }
            }
            return null;
        }

        /// <summary>
        /// 下载并解压头像包到 avatars/（覆盖已有文件）。
        /// 下载地址优先 manifest.DownloadUrl，失败回退 FallbackUrl；进度回调 0~1。
        /// 取消（含应用退出）时清理临时文件并抛出 OperationCanceledException。
        /// </summary>
        public async Task DownloadAndExtractAsync(AvatarManifest manifest, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token, cancellationToken);
            var ct = linked.Token;

            string tempZip = Path.Combine(Path.GetTempPath(), $"azurlanedex_avatars_{Guid.NewGuid():N}.zip");
            try
            {
                bool downloaded = false;
                foreach (var url in new[] { manifest.DownloadUrl, manifest.FallbackUrl })
                {
                    if (string.IsNullOrEmpty(url)) continue;
                    try
                    {
                        using var resp = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                        resp.EnsureSuccessStatusCode();
                        long total = resp.Content.Headers.ContentLength ?? -1;
                        await using (var source = await resp.Content.ReadAsStreamAsync(ct))
                        await using (var target = File.Create(tempZip))
                        {
                            var buffer = new byte[64 * 1024];
                            long read = 0;
                            int n;
                            while ((n = await source.ReadAsync(buffer, ct)) > 0)
                            {
                                await target.WriteAsync(buffer.AsMemory(0, n), ct);
                                read += n;
                                if (total > 0) progress?.Report(Math.Min(0.95, (double)read / total));
                            }
                        }
                        downloaded = true;
                        break;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        if (HandleNetworkError(ex, $"下载 {url}"))
                            throw new OperationCanceledException("下载被中止/取消", ex);
                        LogService.Warning($"头像包下载失败（{url}）: {ex.Message}", nameof(AvatarUpdateService));
                    }
                }

                if (!downloaded)
                    throw new IOException("头像包下载失败：主/备地址均不可用。");

                // 解压：剥离可选的顶层目录（squareicon/），其余条目原样覆盖进 avatars/
                Directory.CreateDirectory(AvatarsRoot);
                using (var zip = ZipFile.OpenRead(tempZip))
                {
                    int totalEntries = zip.Entries.Count;
                    int done = 0;
                    foreach (var entry in zip.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string rel = entry.FullName.Replace('\\', '/');
                        if (string.IsNullOrEmpty(rel) || rel.EndsWith("/")) continue;
                        var parts = rel.Split('/');
                        if (parts.Length >= 2 && (string.Equals(parts[0], "squareicon", StringComparison.OrdinalIgnoreCase) ||
                                                  string.Equals(parts[0], "icon", StringComparison.OrdinalIgnoreCase) ||
                                                  (string.Equals(parts[0], "square", StringComparison.OrdinalIgnoreCase))))
                        {
                            rel = string.Join("/", parts.Skip(1));
                            // 新结构 icon/square/xxx → 本地 avatars/xxx
                            if (rel.StartsWith("square/", StringComparison.OrdinalIgnoreCase))
                                rel = rel.Substring("square/".Length);
                        }

                        string dest = Path.GetFullPath(Path.Combine(AvatarsRoot, rel));
                        if (!dest.StartsWith(Path.GetFullPath(AvatarsRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            continue; // 防目录穿越

                        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                        await using (var src = entry.Open())
                        await using (var dst = File.Create(dest))
                        {
                            await src.CopyToAsync(dst, 64 * 1024, cancellationToken);
                        }
                        progress?.Report(0.95 + 0.05 * (++done) / Math.Max(1, totalEntries));
                    }
                }

                SaveLocalVersion(manifest.Version);
                LogService.Info($"头像包更新完成: version {manifest.Version}", nameof(AvatarUpdateService));
                UpdateCompleted?.Invoke(manifest.Version);
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* 忽略清理失败 */ }
            }
        }

        /// <summary>统计本地头像文件数（*.png）。</summary>
        public int CountLocalAvatars()
        {
            try
            {
                if (!Directory.Exists(AvatarsRoot)) return 0;
                return Directory.EnumerateFiles(AvatarsRoot, "*.png", SearchOption.AllDirectories).Count();
            }
            catch { return 0; }
        }

        /// <summary>清空本地头像缓存（含版本记录）。</summary>
        public void ClearLocalAvatars()
        {
            try
            {
                if (Directory.Exists(AvatarsRoot))
                    Directory.Delete(AvatarsRoot, true);
                Directory.CreateDirectory(AvatarsRoot);
            }
            catch (Exception ex)
            {
                LogService.Error("清空头像缓存失败", nameof(AvatarUpdateService), ex);
                throw;
            }
        }
    }
}
