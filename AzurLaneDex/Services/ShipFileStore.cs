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
                    LogService.Info($"静态文件不存在，返回空数据: {StaticPath}", nameof(ShipFileStore));
                    return new StaticData { Version = "0.0", Ships = new List<ShipStatic>() };
                }
                var json = await File.ReadAllTextAsync(StaticPath);
                var data = JsonSerializer.Deserialize<StaticData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                LogService.Info($"加载静态文件成功: {StaticPath}, 舰船数: {data?.Ships?.Count ?? 0}", nameof(ShipFileStore));
                return data ?? new StaticData { Version = "0.0", Ships = new List<ShipStatic>() };
            }
            catch (Exception ex)
            {
                LogService.Error($"加载静态文件失败: {StaticPath}", nameof(ShipFileStore), ex);
                throw;
            }
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