using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AzurLaneDex.Models;
using AzurLaneDex.Services.Interfaces;

namespace AzurLaneDex.Services
{
    public class ShipFileStore : IShipDataStore
    {
        private readonly string _dataRoot;
        public ShipFileStore()
        {
            _dataRoot = App.DataRoot;
            if (string.IsNullOrEmpty(_dataRoot))
            {
                _dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzurLaneDex", "data");
            }

            // 数据目录不可写时不做任何目录操作（避免受限环境下逐个目录抛权限异常）
            if (!App.DataRootWritable)
            {
                LogService.Warning($"数据目录不可写：{_dataRoot}（用户状态/队伍/缓存仅存内存，不落盘）", nameof(ShipFileStore));
                return;
            }

            try
            {
                Directory.CreateDirectory(_dataRoot);
                Directory.CreateDirectory(Path.Combine(_dataRoot, "static"));
                Directory.CreateDirectory(Path.Combine(_dataRoot, "users"));
            }
            catch (Exception ex)
            {
                LogService.Warning($"用户数据目录不可用：{_dataRoot}（{ex.GetType().Name}: {ex.Message}）", nameof(ShipFileStore));
            }
        }

        private string StaticPath => Path.Combine(_dataRoot, "static", "ships_static.json");

        /// <summary>离线缓存元数据（结构版本 + 数据版本 + 舰船数），用于判断是否需要重写 JSON。</summary>
        private string StaticMetaPath => Path.Combine(_dataRoot, "static", "ships_static.meta.json");

        /// <summary>
        /// 静态缓存结构版本。给 ShipStatic 加字段时必须 +1，否则旧缓存会继续被复用，
        /// 新字段读出来全是默认值（表现为"改了数据层但界面没变化"）。
        /// </summary>
        private const int StaticCacheSchema = 3;

        private sealed class StaticCacheMeta
        {
            public int Schema { get; set; }
            public string DataVersion { get; set; } = "";
            public int ShipCount { get; set; }
            public DateTimeOffset WrittenAt { get; set; }
        }

        public async Task<StaticData> LoadStaticAsync()
        {
            try
            {
                if (!File.Exists(StaticPath))
                {
                    LogService.Info($"静态文件不存在，将创建空数据文件: {StaticPath}", nameof(ShipFileStore));
                    var emptyData = CreateEmptyStaticData();
                    await SaveStaticAsync(emptyData);
                    return emptyData;
                }

                var json = await File.ReadAllTextAsync(StaticPath);
                LogService.Info($"加载静态文件: {StaticPath}, 文件大小: {json.Length} 字节", nameof(ShipFileStore));
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                try
                {
                    var data = JsonSerializer.Deserialize<StaticData>(json, options);
                    if (data == null)
                    {
                        LogService.Error("反序列化得到 null，将覆盖为空白数据", nameof(ShipFileStore));
                        var emptyData = CreateEmptyStaticData();
                        await SaveStaticAsync(emptyData);
                        return emptyData;
                    }
                    LogService.Info($"加载静态文件成功: {StaticPath}, 舰船数: {data.Ships?.Count ?? 0}", nameof(ShipFileStore));
                    return data;
                }
                catch (JsonException ex)
                {
                    LogService.Error($"JSON反序列化失败: {ex.Message}，将覆盖为空白数据", nameof(ShipFileStore), ex);
                    // 输出前200个字符以便调试
                    string backup = StaticPath + ".backup";
                    if (File.Exists(StaticPath))
                        File.Copy(StaticPath, backup, true);
                    var emptyData = CreateEmptyStaticData();
                    await SaveStaticAsync(emptyData);
                    return emptyData;
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"加载静态文件失败: {StaticPath}", nameof(ShipFileStore), ex);
                throw;
            }
        }

        private StaticData CreateEmptyStaticData()
        {
            return new StaticData
            {
                VersionInfo = new DataVersionInfo
                {
                    AppVersion = "1.0.0",
                    GameVersions = new Dictionary<string, string>(),
                    DataVersion = "1.0.0.0.0"
                },
                Ships = new List<ShipStatic>()
            };
        }

        public async Task SaveStaticAsync(StaticData data)
        {
            if (!App.DataRootWritable) return;   // 只读运行：缓存不落盘

            try
            {
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(StaticPath, json);

                // 同步写元数据，供 IsStaticCacheCurrentAsync 判断下次是否需要重写
                await File.WriteAllTextAsync(StaticMetaPath, JsonSerializer.Serialize(new StaticCacheMeta
                {
                    Schema = StaticCacheSchema,
                    DataVersion = data?.VersionInfo?.DataVersion ?? "",
                    ShipCount = data?.Ships?.Count ?? 0,
                    WrittenAt = DateTimeOffset.Now
                }, new JsonSerializerOptions { WriteIndented = true }));

                LogService.Info($"保存静态文件成功: {StaticPath}, 舰船数: {data?.Ships?.Count ?? 0}", nameof(ShipFileStore));
            }
            catch (Exception ex)
            {
                // 缓存写入失败不影响启动（数据源仍可直接从数据库读取）
                LogService.Warning($"保存静态缓存失败: {StaticPath}（{ex.GetType().Name}）", nameof(ShipFileStore));
            }
        }

        /// <summary>
        /// 离线缓存是否与当前数据源一致。一致 → 调用方应跳过 SaveStaticAsync。
        /// 只读元数据小文件，不读取 50MB 的 JSON 本体。
        /// </summary>
        public async Task<bool> IsStaticCacheCurrentAsync(string dataVersion, int shipCount)
        {
            if (!App.DataRootWritable) return true;          // 只读运行：不写缓存，视为"无需更新"

            try
            {
                if (!File.Exists(StaticPath) || !File.Exists(StaticMetaPath)) return false;

                var meta = JsonSerializer.Deserialize<StaticCacheMeta>(await File.ReadAllTextAsync(StaticMetaPath));
                return meta != null
                       && meta.Schema == StaticCacheSchema
                       && string.Equals(meta.DataVersion, dataVersion ?? "", StringComparison.Ordinal)
                       && meta.ShipCount == shipCount;
            }
            catch
            {
                return false;      // 元数据损坏 → 让它重写一次
            }
        }

        public string GetUserStatePath(string accountName)
        {
            return Path.Combine(_dataRoot, "users", accountName, "ships_state.json");
        }

        private string GetTeamsPath(string accountName)
        {
            return Path.Combine(_dataRoot, "users", accountName, "teams.json");
        }

        /// <summary>加载账户配队列表（缺失/损坏返回空表，不阻断启动）。</summary>
        public async Task<TeamList> LoadTeamsAsync(string accountName)
        {
            try
            {
                var path = GetTeamsPath(accountName);
                if (!File.Exists(path))
                {
                    return new TeamList { AccountName = accountName, Teams = new List<Team>() };
                }
                var json = await File.ReadAllTextAsync(path);
                var data = JsonSerializer.Deserialize<TeamList>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                LogService.Info($"加载配队成功: {path}, 队伍数: {data?.Teams?.Count ?? 0}", nameof(ShipFileStore));
                return data ?? new TeamList { AccountName = accountName, Teams = new List<Team>() };
            }
            catch (Exception ex)
            {
                LogService.Error($"加载配队失败: {accountName}", nameof(ShipFileStore), ex);
                return new TeamList { AccountName = accountName, Teams = new List<Team>() };
            }
        }

        /// <summary>保存账户配队列表。</summary>
        public async Task SaveTeamsAsync(string accountName, TeamList teams)
        {
            try
            {
                var path = GetTeamsPath(accountName);
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(teams, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(path, json);
                LogService.Info($"保存配队成功: {path}, 队伍数: {teams?.Teams?.Count ?? 0}", nameof(ShipFileStore));
            }
            catch (Exception ex)
            {
                LogService.Error($"保存配队失败: {accountName}", nameof(ShipFileStore), ex);
                throw;
            }
        }

        public async Task<StateList> LoadStateAsync(string accountName)
        {
            try
            {
                var path = GetUserStatePath(accountName);
                if (!File.Exists(path))
                {
                    LogService.Info($"用户状态文件不存在: {path}", nameof(ShipFileStore));
                    return new StateList { States = new List<ShipState>() };
                }
                var json = await File.ReadAllTextAsync(path);
                var data = JsonSerializer.Deserialize<StateList>(json);
                LogService.Info($"加载用户状态成功: {path}, 状态数: {data?.States?.Count ?? 0}", nameof(ShipFileStore));
                return data ?? new StateList { States = new List<ShipState>() };
            }
            catch (Exception ex)
            {
                LogService.Error($"加载用户状态失败: {accountName}", nameof(ShipFileStore), ex);
                throw;
            }
        }

        public async Task SaveStateAsync(string accountName, StateList states)
        {
            try
            {
                var path = GetUserStatePath(accountName);
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(path, json);
                LogService.Info($"保存用户状态成功: {path}, 状态数: {states?.States?.Count ?? 0}", nameof(ShipFileStore));
            }
            catch (Exception ex)
            {
                LogService.Error($"保存用户状态失败: {accountName}", nameof(ShipFileStore), ex);
                throw;
            }
        }
    }
}