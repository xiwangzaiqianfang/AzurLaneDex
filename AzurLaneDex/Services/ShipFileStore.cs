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
            Directory.CreateDirectory(_dataRoot);
            Directory.CreateDirectory(Path.Combine(_dataRoot, "static"));
            Directory.CreateDirectory(Path.Combine(_dataRoot, "users"));
        }

        private string StaticPath => Path.Combine(_dataRoot, "static", "ships_static.json");

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
            try
            {
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(StaticPath, json);
                LogService.Info($"保存静态文件成功: {StaticPath}, 舰船数: {data?.Ships?.Count ?? 0}", nameof(ShipFileStore));
            }
            catch (Exception ex)
            {
                LogService.Error($"保存静态文件失败: {StaticPath}", nameof(ShipFileStore), ex);
                throw;
            }
        }

        public string GetUserStatePath(string accountName)
        {
            return Path.Combine(_dataRoot, "users", accountName, "ships_state.json");
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