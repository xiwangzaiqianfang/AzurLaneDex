using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AzurLaneDex.Models;
using AzurLaneDex.Services;
using AzurLaneDex.Services.Interfaces;
using AzurLaneDex.ViewModels;
using Windows.ApplicationModel;

namespace AzurLaneDex.Services;

public static class ShipIdRanges
{
    public const int NormalStart = 1;
    public const int NormalEnd = 9999;
    public const int MetaStart = 10001;
    public const int CollabStart = 20001;
    public const int ResearchStart = 30001;
}

public class ShipManager
{
    private readonly AccountManager _accountManager;
    private readonly IShipDataStore _dataStore;
    private readonly IShipMigrator _migrator;
    private readonly IShipDataUpdater _updater;
    private readonly IShipStatsCalculator _statsCalculator;
    private List<ShipStatic> _staticShips = new();
    private Dictionary<int, ShipState> _userStates = new();
    private string _currentAccount;

    public ObservableCollection<ShipViewModel> Ships { get; private set; } = new();
    public ShipStatic MigrateSingleShip(JsonElement old) => _migrator.MigrateSingleShip(old);

    public string Version { get; private set; } = "0.0";

    public event Action? DataStructureChanged;
    public event Action? StateChanged;
    public event Action? data_changed;

    private readonly string _configPath;
    public Dictionary<string, object> Config { get; private set; }

    public ShipManager(AccountManager accountManager)
    {
        _accountManager = accountManager;
        _currentAccount = accountManager.CurrentAccount;

        // 初始化服务（后续可改为依赖注入）
        _dataStore = new ShipFileStore();
        _migrator = new ShipMigrator(_dataStore);
        _updater = new ShipDataUpdater();
        _statsCalculator = new ShipStatsCalculator();

        // 加载配置
        _configPath = Path.Combine(App.DataRoot, "config.json");
        LoadConfig();
    }

    // ========== 公共 API ==========
    public async Task LoadAsync()
    {
        LogService.Info("开始加载舰船数据", nameof(ShipManager));
        try
        {
            // 1. 加载静态数据
            StaticData staticData = await _dataStore.LoadStaticAsync();
            if (staticData?.Ships == null || staticData.Ships.Count == 0)
            {
                // 如果文件不存在或为空，尝试从内置资源复制
                staticData = await _dataStore.LoadStaticAsync();
            }

            // 2. 检查旧格式并迁移
            if (staticData != null && _migrator.IsOldFormat(JsonSerializer.Serialize(staticData)))
            {
                await _migrator.MigrateAsync();
                staticData = await _dataStore.LoadStaticAsync();
            }

            Version = staticData?.Version ?? "0.0";
            _staticShips = staticData?.Ships ?? new List<ShipStatic>();
            LogService.Info($"舰船数据加载完成，共 {Ships.Count} 艘舰船", nameof(ShipManager));
        }
        catch (Exception ex)
        {
            LogService.Error("加载舰船数据失败", nameof(ShipManager), ex);
            throw;
        }

        // 3. 加载用户状态
        _currentAccount = _accountManager.CurrentAccount;
        StateList stateList = await _dataStore.LoadStateAsync(_currentAccount);
        _userStates = stateList?.States?.ToDictionary(s => s.Id, s => s) ?? new Dictionary<int, ShipState>();

        // 4. 构建 ViewModel 集合
        Ships.Clear();
        foreach (var staticShip in _staticShips)
        {
            if (!_userStates.TryGetValue(staticShip.Id, out var state))
                state = new ShipState { Id = staticShip.Id };
            Ships.Add(new ShipViewModel(staticShip, state));
        }

        // 布里强制满破
        foreach (var ship in Ships)
        {
            if (ship.RawName == "泛用型布里" || ship.RawName == "试作型布里MKII" || ship.RawName == "特装型布里MKIII")
                ship.Breakthrough = 3;
        }

        DataStructureChanged?.Invoke();
        StateChanged?.Invoke();
    }

    public async Task SaveAsync()
    {
        if (string.IsNullOrEmpty(_currentAccount)) return;
        LogService.Info($"开始保存用户状态: {_currentAccount}", nameof(ShipManager));
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
        LogService.Operation("用户登录", accountName);
    }

    // ========== 增删改 ==========
    public async Task<bool> AddShip(ShipStatic newShip)
    {
        if (!_accountManager.IsDeveloper())
        {
            LogService.Warning("非开发者尝试新增舰船", nameof(ShipManager));
            throw new InvalidOperationException("只有开发者账户才能新增舰船");
        }

        try
        {
            if (newShip.Id == 0)
                newShip.Id = GetNextIdForCategory(newShip.Category);
            else if (!IsIdValidForCategory(newShip.Id, newShip.Category) || _staticShips.Any(s => s.Id == newShip.Id))
                newShip.Id = GetNextIdForCategory(newShip.Category);

            if (newShip.GameOrder == 0)
                newShip.GameOrder = _staticShips.Count > 0 ? _staticShips.Max(s => s.GameOrder) + 1 : 1;
            else
            {
                var conflict = _staticShips.FirstOrDefault(s => s.GameOrder == newShip.GameOrder);
                if (conflict != null)
                {
                    foreach (var ship in _staticShips.Where(s => s.GameOrder >= newShip.GameOrder))
                        ship.GameOrder++;
                    _staticShips = _staticShips.OrderBy(s => s.GameOrder).ToList();
                }
            }

            if (newShip.CategoryOrder == 0)
            {
                int maxOrder = _staticShips.Where(s => s.Category == newShip.Category).Select(s => s.CategoryOrder).DefaultIfEmpty(0).Max();
                newShip.CategoryOrder = maxOrder + 1;
            }
            else
            {
                var conflict = _staticShips.FirstOrDefault(s => s.Category == newShip.Category && s.CategoryOrder == newShip.CategoryOrder);
                if (conflict != null)
                {
                    foreach (var ship in _staticShips.Where(s => s.Category == newShip.Category && s.CategoryOrder >= newShip.CategoryOrder))
                        ship.CategoryOrder++;
                }
            }

            _staticShips.Add(newShip);
            _staticShips = _staticShips.OrderBy(s => s.GameOrder).ToList();
            var staticData = new StaticData { Version = Version, Ships = _staticShips };
            await _dataStore.SaveStaticAsync(staticData);

            var newState = new ShipState { Id = newShip.Id };
            _userStates[newShip.Id] = newState;
            var newViewModel = new ShipViewModel(newShip, newState);
            Ships.Add(newViewModel);
            await SaveAsync();

            DataStructureChanged?.Invoke();
            LogService.Operation("新增舰船", $"{newShip.Name.GetValueOrDefault("zh-Hans")} (ID: {newShip.Id})", _accountManager.CurrentAccount); return true;
        }
        catch (Exception ex)
        {
            LogService.Error($"新增舰船失败: {newShip?.Name?.GetValueOrDefault("zh-Hans")}", nameof(ShipManager), ex);
            throw;
        }
    }

    public async Task UpdateShip(int oldId, ShipStatic newShip)
    {
        if (!_accountManager.IsDeveloper())
        {
            LogService.Warning("非开发者尝试编辑舰船", nameof(ShipManager));
            throw new InvalidOperationException("只有开发者账户才能编辑舰船");
        }
        try
        {
            int index = _staticShips.FindIndex(s => s.Id == oldId);
            if (index == -1) return;

            if (newShip.Id != oldId && _staticShips.Any(s => s.Id == newShip.Id))
                newShip.Id = _staticShips.Max(s => s.Id) + 1;

            _staticShips.RemoveAt(index);
            _staticShips.Add(newShip);
            _staticShips = _staticShips.OrderBy(s => s.Category).ThenBy(s => s.CategoryOrder).ToList();
            var staticData = new StaticData { Version = Version, Ships = _staticShips };
            await _dataStore.SaveStaticAsync(staticData);

            if (newShip.Id != oldId && _userStates.TryGetValue(oldId, out var state))
            {
                _userStates.Remove(oldId);
                _userStates[newShip.Id] = state;
            }

            var oldVm = Ships.FirstOrDefault(vm => vm.Id == oldId);
            if (oldVm != null)
            {
                var newVm = new ShipViewModel(newShip, oldVm.GetState());
                int vmIndex = Ships.IndexOf(oldVm);
                Ships[vmIndex] = newVm;
            }

            await SaveAsync();
            LogService.Operation("编辑舰船", $"{newShip.Name.GetValueOrDefault("zh-Hans")} (ID: {newShip.Id})", _accountManager.CurrentAccount);
            DataStructureChanged?.Invoke();
        }
        catch (Exception ex)
        {
            LogService.Error($"编辑舰船失败: {newShip?.Name?.GetValueOrDefault("zh-Hans")}", nameof(ShipManager), ex);
            throw;
        }
    }

    public async Task DeleteShip(int shipId)
    {
        if (!_accountManager.IsDeveloper())
        {
            LogService.Warning("非开发者尝试删除舰船", nameof(ShipManager));
            throw new InvalidOperationException("只有开发者账户才能删除舰船");
        }
        try
        {
            var removed = _staticShips.RemoveAll(s => s.Id == shipId) > 0;
            if (!removed) return;

            var staticData = new StaticData { Version = Version, Ships = _staticShips };
            await _dataStore.SaveStaticAsync(staticData);
            _userStates.Remove(shipId);
            var vm = Ships.FirstOrDefault(v => v.Id == shipId);
            if (vm != null) Ships.Remove(vm);
            await SaveAsync();

            DataStructureChanged?.Invoke();
            LogService.Operation("删除舰船", $"ID: {shipId}");
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

    public StatsData stats()
        => _statsCalculator.CalculateStats(Ships);

    public Dictionary<(string ShipClass, string Attr), int> CalculateGlobalBonuses()
        => _statsCalculator.CalculateGlobalBonuses(Ships);

    // ========== 更新方法（委托给 _updater） ==========
    public async Task<string> GetRemoteDataVersionAsync(string url, string proxy = "")
        => await _updater.GetRemoteVersionAsync(url, proxy);

    public async Task<bool> UpdateDataFromUrlAsync(string url, string proxy = "")
    {
        bool result = await _updater.DownloadAndApplyUpdateAsync(url, proxy, async (newData) =>
        {
            // 保存新数据到文件
            await _dataStore.SaveStaticAsync(newData);
            // 重新加载
            await LoadAsync();
            DataStructureChanged?.Invoke();
            LogService.Operation("数据更新", "结束");
        });
        return result;
    }

    // ========== 其他辅助方法 ==========
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
                        if (Config[key] is JsonElement elem)
                            switch (elem.ValueKind)
                            {
                                case JsonValueKind.True: Config[key] = true; break;
                                case JsonValueKind.False: Config[key] = false; break;
                                case JsonValueKind.Number: Config[key] = elem.GetInt32(); break;
                                case JsonValueKind.String: Config[key] = elem.GetString(); break;
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

    public string GetCurrentAppVersion()
    {
        try
        {
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch
        {
            var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return assemblyVersion?.ToString() ?? "0.0.0.0";
        }
    }
    private int GetNextIdForCategory(ShipCategory category)
    {
        int start, end;
        switch (category)
        {
            case ShipCategory.META:
                start = ShipIdRanges.MetaStart;
                end = ShipIdRanges.CollabStart - 1;
                break;
            case ShipCategory.Collab:
                start = ShipIdRanges.CollabStart;
                end = ShipIdRanges.ResearchStart - 1;
                break;
            case ShipCategory.Research:
                start = ShipIdRanges.ResearchStart;
                end = int.MaxValue;
                break;
            default:
                start = ShipIdRanges.NormalStart;
                end = ShipIdRanges.NormalEnd;
                break;
        }
        var existingIds = _staticShips.Where(s => s.Category == category).Select(s => s.Id).ToHashSet();
        for (int id = start; id <= end; id++)
            if (!existingIds.Contains(id))
                return id;
        throw new InvalidOperationException($"No available ID in range for category {category}");
    }

    private bool IsIdValidForCategory(int id, ShipCategory category)
    {
        return category switch
        {
            ShipCategory.META => id >= ShipIdRanges.MetaStart && id < ShipIdRanges.CollabStart,
            ShipCategory.Collab => id >= ShipIdRanges.CollabStart && id < ShipIdRanges.ResearchStart,
            ShipCategory.Research => id >= ShipIdRanges.ResearchStart,
            _ => id >= ShipIdRanges.NormalStart && id <= ShipIdRanges.NormalEnd
        };
    }
    public string GetUserStatePath() => _dataStore.GetUserStatePath(_currentAccount);
    public void NotifyDataChanged() => data_changed?.Invoke();
}