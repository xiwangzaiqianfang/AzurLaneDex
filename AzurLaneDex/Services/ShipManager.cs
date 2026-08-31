using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using AzurLaneDex.Services.Interfaces;
using AzurLaneDex.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzurLaneDex.Services;

public class ShipManager
{
    private readonly AccountManager _accountManager;
    private readonly IShipDataStore _dataStore;
    private readonly IShipStatsCalculator _statsCalculator;

    private List<ShipStatic> _staticShips = new();
    private Dictionary<int, ShipState> _userStates = new();
    private string _currentAccount;

    public ObservableCollection<ShipViewModel> Ships { get; private set; } = new();
    public string DataVersion { get; private set; } = "0.0.0.0.0";

    public event Action? DataStructureChanged;
    public event Action? StateChanged;
    public event Action? data_changed;

    private readonly string _configPath;
    public Dictionary<string, object> Config { get; private set; }

    public ShipManager(AccountManager accountManager)
    {
        _accountManager = accountManager;
        _currentAccount = accountManager.CurrentAccount;
        _dataStore = new ShipFileStore();
        _statsCalculator = new ShipStatsCalculator();

        _configPath = Path.Combine(App.DataRoot, "config.json");
        LoadConfig();
    }

    public async Task LoadAsync()
    {
        LogService.Info("开始加载舰船数据", nameof(ShipManager));
        try
        {
            // 1. 加载静态数据
            var staticData = await _dataStore.LoadStaticAsync();
            if (staticData == null || staticData.Ships == null || staticData.Ships.Count == 0)
            {
                LogService.Warning("静态数据为空，将使用空列表继续", nameof(ShipManager));
                staticData = new StaticData
                {
                    VersionInfo = new DataVersionInfo { DataVersion = "0.0.0.0.0" },
                    Ships = new List<ShipStatic>()
                };
            }

            DataVersion = staticData.VersionInfo?.DataVersion ?? "0.0.0.0.0";
            _staticShips = staticData.Ships;

            // 2. 加载用户状态
            _currentAccount = _accountManager.CurrentAccount;
            var stateList = await _dataStore.LoadStateAsync(_currentAccount);
            _userStates = stateList?.States?.ToDictionary(s => s.ShipId, s => s) ?? new Dictionary<int, ShipState>();

            // 3. 构建 ViewModel 集合
            Ships.Clear();
            foreach (var staticShip in _staticShips)
            {
                if (!_userStates.TryGetValue(staticShip.Id, out var state))
                    state = new ShipState { ShipId = staticShip.Id };
                Ships.Add(new ShipViewModel(staticShip, state));
            }

            LogService.Info($"舰船数据加载完成，共 {Ships.Count} 艘舰船", nameof(ShipManager));
            DataStructureChanged?.Invoke();
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            LogService.Error("加载舰船数据失败", nameof(ShipManager), ex);
            throw;
        }
    }

    public async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(_currentAccount)) return;
        try
        {
            var stateList = new StateList { States = Ships.Select(vm => vm.GetState()).ToList() };
            await _dataStore.SaveStateAsync(_currentAccount, stateList);
            StateChanged?.Invoke();
            LogService.Operation("用户状态保存", $"账户 {_currentAccount}", _currentAccount);
        }
        catch (Exception ex)
        {
            LogService.Error($"保存用户状态失败: {_currentAccount}", nameof(ShipManager), ex);
            throw;
        }
    }

    public async Task SwitchAccountAsync(string accountName)
    {
        _accountManager.SetCurrentAccount(accountName);
        _currentAccount = accountName;
        await LoadAsync();
        data_changed?.Invoke();
    }

    // ========== 增删改（开发者功能） ==========
    public async Task<bool> AddShip(ShipStatic newShip)
    {
        if (!_accountManager.IsDeveloper())
            throw new InvalidOperationException("只有开发者账户才能新增舰船");

        try
        {
            // 自动分配 ID（简化：取最大ID+1）
            newShip.Id = _staticShips.Any() ? _staticShips.Max(s => s.Id) + 1 : 1;
            _staticShips.Add(newShip);

            var staticData = new StaticData
            {
                VersionInfo = new DataVersionInfo { DataVersion = DataVersion },
                Ships = _staticShips
            };
            await _dataStore.SaveStaticAsync(staticData);

            var newState = new ShipState { ShipId = newShip.Id };
            _userStates[newShip.Id] = newState;
            Ships.Add(new ShipViewModel(newShip, newState));
            await SaveAsync();

            DataStructureChanged?.Invoke();
            LogService.Operation("新增舰船", $"{newShip.Name.GetLocalized()} (ID: {newShip.Id})", _accountManager.CurrentAccount);
            return true;
        }
        catch (Exception ex)
        {
            LogService.Error($"新增舰船失败", nameof(ShipManager), ex);
            throw;
        }
    }

    public async Task UpdateShip(int oldId, ShipStatic newShip)
    {
        if (!_accountManager.IsDeveloper())
            throw new InvalidOperationException("只有开发者账户才能编辑舰船");

        try
        {
            int index = _staticShips.FindIndex(s => s.Id == oldId);
            if (index == -1) return;

            // 如果 ID 改变，且新ID冲突则重新分配
            if (newShip.Id != oldId && _staticShips.Any(s => s.Id == newShip.Id))
                newShip.Id = _staticShips.Max(s => s.Id) + 1;

            _staticShips[index] = newShip;

            var staticData = new StaticData
            {
                VersionInfo = new DataVersionInfo { DataVersion = DataVersion },
                Ships = _staticShips
            };
            await _dataStore.SaveStaticAsync(staticData);

            // 更新用户状态中的 ShipId（如果改变）
            if (newShip.Id != oldId && _userStates.TryGetValue(oldId, out var existingState))
            {
                _userStates.Remove(oldId);
                existingState.ShipId = newShip.Id;
                _userStates[newShip.Id] = existingState;
            }

            // 更新 ViewModel
            var oldVm = Ships.FirstOrDefault(vm => vm.Id == oldId);
            if (oldVm != null)
            {
                var oldState = oldVm.GetState();
                var newVm = new ShipViewModel(newShip, oldState);
                int vmIndex = Ships.IndexOf(oldVm);
                Ships[vmIndex] = newVm;
            }

            await SaveAsync();
            DataStructureChanged?.Invoke();
            LogService.Operation("编辑舰船", $"{newShip.Name.GetLocalized()} (ID: {newShip.Id})", _accountManager.CurrentAccount);
        }
        catch (Exception ex)
        {
            LogService.Error($"编辑舰船失败", nameof(ShipManager), ex);
            throw;
        }
    }

    public async Task DeleteShip(int shipId)
    {
        if (!_accountManager.IsDeveloper())
            throw new InvalidOperationException("只有开发者账户才能删除舰船");

        try
        {
            _staticShips.RemoveAll(s => s.Id == shipId);
            var staticData = new StaticData
            {
                VersionInfo = new DataVersionInfo { DataVersion = DataVersion },
                Ships = _staticShips
            };
            await _dataStore.SaveStaticAsync(staticData);

            _userStates.Remove(shipId);
            var vm = Ships.FirstOrDefault(v => v.Id == shipId);
            if (vm != null) Ships.Remove(vm);
            await SaveAsync();

            DataStructureChanged?.Invoke();
            LogService.Operation("删除舰船", $"ID: {shipId}", _accountManager.CurrentAccount);
        }
        catch (Exception ex)
        {
            LogService.Error($"删除舰船失败: ID: {shipId}", nameof(ShipManager), ex);
            throw;
        }
    }

    // ========== 统计方法（委托给 _statsCalculator） ==========
    public Dictionary<string, CampTechData> CalculateCampTechPoints()
        => _statsCalculator.CalculateCampTech(Ships);

    public int GetTotalTechPoints()
        => _statsCalculator.GetTotalTechPoints(Ships);

    public int GetOwnedTechPoints()
        => _statsCalculator.GetOwnedTechPoints(Ships);

    public StatsData GetStats()
        => _statsCalculator.CalculateStats(Ships);

    public Dictionary<(ShipType ShipType, AttributeType Attr), int> CalculateGlobalBonuses()
        => _statsCalculator.CalculateGlobalBonuses(Ships);

    // ========== 配置管理 ==========
    private void LoadConfig()
    {
        if (!Directory.Exists(Path.GetDirectoryName(_configPath)))
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));

        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                {
                    Config = dict;
                    foreach (var key in Config.Keys.ToList())
                    {
                        if (Config[key] is JsonElement elem)
                        {
                            Config[key] = elem.ValueKind switch
                            {
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Number => elem.GetInt32(),
                                JsonValueKind.String => elem.GetString(),
                                _ => Config[key]
                            };
                        }
                    }
                }
                else SetDefaultConfig();
            }
            catch { SetDefaultConfig(); }
        }
        else SetDefaultConfig();
    }

    public void SaveConfig()
    {
        var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }

    private void SetDefaultConfig()
    {
        Config = new Dictionary<string, object>
        {
            ["edit_password"] = "",
            ["log_edits"] = true,
            ["ask_account_on_startup"] = true,
            ["default_account"] = ""
        };
        SaveConfig();
    }

    public string GetUserStatePath()
    {
        return _dataStore.GetUserStatePath(_currentAccount);
    }

    public void NotifyDataChanged()
    {
        data_changed?.Invoke();
    }

    public string GetCurrentAppVersion()
    {
        try
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        }
    }
}