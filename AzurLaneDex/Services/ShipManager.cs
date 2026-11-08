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

/// <summary>
/// 舰船数据中枢：装载（SQLite 数据源 ships_static.db → 内存目录 + 状态）／保存用户状态／统计转发／配置。
/// 静态数据唯一来源是数据库（SqliteDataSourceLoader）；应用内不写回静态数据，
/// 舰船目录的增删改一律在数据库侧完成，客户端只读。
/// </summary>
public class ShipManager
{
    private readonly AccountManager _accountManager;
    private readonly IShipDataStore _dataStore;
    private readonly IShipStatsCalculator _statsCalculator;

    private List<ShipStatic> _staticShips = new();        // 数据源装载后的内存目录
    private Dictionary<int, ShipState> _userStates = new();
    private string _currentAccount;

    public ObservableCollection<ShipViewModel> Ships { get; private set; } = new();
    public ObservableCollection<Team> Teams { get; private set; } = new();
    public string DataVersion { get; private set; } = "0.0.0.0.0";

    /// <summary>当前已加载的用户状态/配队所属账户（供启动流程判断账户是否变化）。</summary>
    public string CurrentAccount => _currentAccount;

    public event Action? DataStructureChanged;
    public event Action? StateChanged;
    public event Action? data_changed;
    /// <summary>配队集合变化事件（增删改后触发）。</summary>
    public event Action? TeamsChanged;

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

    /// <summary>只读访问当前静态目录（统计用）。</summary>
    public IReadOnlyList<ShipStatic> StaticShips => _staticShips;

    public async Task LoadAsync()
    {
        LogService.Info("开始加载舰船数据", nameof(ShipManager));
        try
        {
            // 1. 静态目录：优先 SQLite 数据源（ships_static.db）；失败/不可用时回退本地缓存
            string dbPath = SqliteDataSourceLoader.ResolveDbPath();
            var source = await SqliteDataSourceLoader.LoadAsync(dbPath);
            List<ShipStatic> staticShips;
            if (source.SourceAvailable)
            {
                DataVersion = source.DataVersion;
                foreach (var w in source.Warnings)
                    LogService.Warning(w, nameof(SqliteDataSourceLoader));

                staticShips = source.Ships;
            }
            else
            {
                LogService.Warning($"SQLite 数据源不可用（{dbPath}），回退本地缓存", nameof(ShipManager));
                var cached = await _dataStore.LoadStaticAsync();
                staticShips = cached?.Ships ?? new List<ShipStatic>();
                DataVersion = cached?.VersionInfo?.DataVersion ?? "0.0.0.0.0";
            }
            _staticShips = staticShips;
            App.StartupTrace("LoadAsync:数据库", $"路径={dbPath} 可用={source.SourceAvailable} 舰船数={staticShips.Count} 警告={source.Warnings.Count}");
            foreach (var w in source.Warnings.Take(3))
                App.StartupTrace("LoadAsync:警告", w);

            // 2. 加载用户状态（数据源切换不迁移旧存档，id 以新数据源为准）
            _currentAccount = _accountManager.CurrentAccount;
            var stateList = await _dataStore.LoadStateAsync(_currentAccount);
            _userStates = stateList?.States?.ToDictionary(s => s.ShipId, s => s) ?? new Dictionary<int, ShipState>();

            // 2.5 加载配队（与账户绑定）
            var teamList = await _dataStore.LoadTeamsAsync(_currentAccount);
            Teams.Clear();
            foreach (var t in teamList?.Teams ?? new List<Team>())
                Teams.Add(t);

            // 2.8 写离线缓存（下次数据源不可用时仍可展示）
            //     仅当数据版本/舰船数变化时才重写：原先每次启动都序列化约 50MB JSON，纯浪费 I/O
            if (source.SourceAvailable)
            {
                if (await _dataStore.IsStaticCacheCurrentAsync(DataVersion, staticShips.Count))
                {
                    App.StartupTrace("LoadAsync:缓存", $"已是最新（{DataVersion} / {staticShips.Count} 艘），跳过写入");
                }
                else
                {
                    await _dataStore.SaveStaticAsync(new StaticData
                    {
                        VersionInfo = new DataVersionInfo { DataVersion = DataVersion },
                        Ships = staticShips
                    });
                    App.StartupTrace("LoadAsync:缓存", $"已更新（{DataVersion} / {staticShips.Count} 艘）");
                }
            }

            // 3. 构建 ViewModel 集合
            Ships.Clear();
            foreach (var staticShip in _staticShips)
            {
                if (!_userStates.TryGetValue(staticShip.Id, out var state))
                    state = new ShipState { ShipId = staticShip.Id };
                Ships.Add(new ShipViewModel(staticShip, state));
            }

            LogService.Info($"舰船数据加载完成，共 {Ships.Count} 艘舰船 (version {DataVersion})", nameof(ShipManager));
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

    // ========== 配队 CRUD（账户绑定，users/{account}/teams.json） ==========

    private async Task PersistTeamsAsync()
    {
        await _dataStore.SaveTeamsAsync(_currentAccount, new TeamList
        {
            AccountName = _currentAccount,
            Teams = Teams.ToList()
        });
        TeamsChanged?.Invoke();
    }

    /// <summary>新增配队（自动分配 id）。</summary>
    public async Task<Team> AddTeamAsync(Team team)
    {
        if (team == null) throw new ArgumentNullException(nameof(team));
        team.Id = Teams.Count > 0 ? Teams.Max(t => t.Id) + 1 : 1;
        Teams.Add(team);
        await PersistTeamsAsync();
        LogService.Operation("配队管理", $"新增配队: {team.Name}");
        return team;
    }

    /// <summary>更新配队（按 Id 替换）。</summary>
    public async Task UpdateTeamAsync(Team team)
    {
        if (team == null) return;
        int index = -1;
        for (int i = 0; i < Teams.Count; i++)
        {
            if (Teams[i].Id == team.Id) { index = i; break; }
        }
        if (index < 0) return;
        Teams[index] = team;
        await PersistTeamsAsync();
        LogService.Operation("配队管理", $"更新配队: {team.Name}");
    }

    /// <summary>删除配队。</summary>
    public async Task DeleteTeamAsync(int teamId)
    {
        var team = Teams.FirstOrDefault(t => t.Id == teamId);
        if (team == null) return;
        Teams.Remove(team);
        await PersistTeamsAsync();
        LogService.Operation("配队管理", $"删除配队: {team.Name}");
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
        try
        {
            string? dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch (Exception ex)
        {
            LogService.Warning($"配置目录不可用：{ex.Message}", nameof(ShipManager));
        }

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
        if (!App.DataRootWritable)
        {
            LogService.Warning("数据目录不可写：配置仅保存在内存", nameof(ShipManager));
            return;
        }

        try
        {
            string? dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            LogService.Warning($"配置保存失败：{ex.Message}", nameof(ShipManager));
        }
    }

    private void SetDefaultConfig()
    {
        Config = new Dictionary<string, object>
        {
            ["edit_password"] = "",
            ["log_edits"] = true,
            ["ask_account_on_startup"] = true,
            ["default_account"] = "",
            ["data_source_path"] = "", // 空 = 使用 DataSourceLoader.DefaultDataSourceDir（装备等离线 JSON 资源）
            ["avatar_auto_check"] = true, // 启动时自动检查头像包更新
            ["avatar_last_prompt_ticks"] = "" // 最近一次提示时间（UTC ticks，防骚扰）
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
        // 非打包运行没有包标识：先判断再调用，避免 Package.Current 抛“拒绝访问”
        if (App.IsPackaged)
        {
            try
            {
                var version = Windows.ApplicationModel.Package.Current.Id.Version;
                return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
            }
            catch { /* 继续用程序集版本 */ }
        }
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
    }
}
