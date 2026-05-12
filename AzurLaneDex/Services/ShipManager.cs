using AzurLaneDex.Models;
using AzurLaneDex.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace AzurLaneDex.Services;

public class ShipManager
{
    private readonly AccountManager _accountManager;
    private readonly string _staticPath;
    private string _userStatePath;
    public string GetUserStatePath() => _userStatePath;
    // ShipManager.cs
    public event Action? DataStructureChanged;   // 增、删、改静态数据时触发
    public event Action? StateChanged;           // 用户状态（收集进度）变化时触发
    private List<ShipStatic> _staticShips = new();
    private Dictionary<int, ShipState> _userStates = new();

    public ObservableCollection<ShipViewModel> Ships { get; private set; } = new();
    public string Version { get; private set; } = "0.0";

    // 配置相关
    private readonly string _configPath;
    public Dictionary<string, object> Config { get; private set; }

    public ShipManager(AccountManager accountManager)
    {
        try
        {
            _accountManager = accountManager;

            string dataRoot = App.DataRoot;
            if (string.IsNullOrEmpty(dataRoot))
            {
                dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AzurLaneDex", "data");
                Directory.CreateDirectory(dataRoot);
            }

            string staticDir = Path.Combine(dataRoot, "static");
            Directory.CreateDirectory(staticDir);
            _staticPath = Path.Combine(staticDir, "ships_static.json");

            EnsureBuiltinStaticExists();

            // 配置文件路径
            _configPath = Path.Combine(App.DataRoot, "config.json");
            LoadConfig();

            EnsureStaticFileExists();
            Load();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShipManager constructor error: {ex}");
            throw;
        }
    }

    private void EnsureBuiltinStaticExists()
    {
        if (File.Exists(_staticPath))
            return;

        string builtinPath = null;

        // 开发环境（未打包）
        string devPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "ships_static.json");
        if (File.Exists(devPath))
            builtinPath = devPath;
        else
        {
            // 打包环境（MSIX）
            try
            {
                var installedPath = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
                string packagedPath = Path.Combine(installedPath, "Assets", "ships_static.json");
                if (File.Exists(packagedPath))
                    builtinPath = packagedPath;
            }
            catch { }
        }

        if (builtinPath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_staticPath)!);
            File.Copy(builtinPath, _staticPath);
            System.Diagnostics.Debug.WriteLine($"已从内置资源复制舰船数据: {builtinPath} -> {_staticPath}");
            return;
        }

        // 最后手段：创建空文件
        var empty = new StaticData { Version = "0.0", Ships = new List<ShipStatic>() };
        var json = JsonSerializer.Serialize(empty, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_staticPath, json);
        System.Diagnostics.Debug.WriteLine("未找到内置舰船数据，已创建空文件");
    }

    private void LoadConfig()
    {
        if (!Directory.Exists(Path.GetDirectoryName(_configPath)))
            Directory.CreateDirectory(Path.GetDirectoryName(_configPath));
        System.Diagnostics.Debug.WriteLine($"Loading config from: {_configPath}");
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                if (dict != null)
                {
                    Config = dict;
                    // 将 JsonElement 转换为基本类型（布尔、数字、字符串）
                    foreach (var key in Config.Keys.ToList())
                    {
                        if (Config[key] is JsonElement elem)
                        {
                            switch (elem.ValueKind)
                            {
                                case JsonValueKind.True:
                                    Config[key] = true;
                                    break;
                                case JsonValueKind.False:
                                    Config[key] = false;
                                    break;
                                case JsonValueKind.Number:
                                    Config[key] = elem.GetInt32();
                                    break;
                                case JsonValueKind.String:
                                    Config[key] = elem.GetString();
                                    break;
                                    // 其他类型可忽略或添加
                            }
                        }
                    }
                }
                else
                {
                    SetDefaultConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadConfig error: {ex.Message}");
                SetDefaultConfig();
            }
        }
        else
        {
            SetDefaultConfig();
        }
        System.Diagnostics.Debug.WriteLine($"Loaded ask_account_on_startup: {Config.GetValueOrDefault("ask_account_on_startup")}");
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

    public void SaveConfig()
    {
        var json = JsonSerializer.Serialize(Config, new JsonSerializerOptions { WriteIndented = true });
        System.Diagnostics.Debug.WriteLine($"Config file path: {_configPath}");
        File.WriteAllText(_configPath, json);
        System.Diagnostics.Debug.WriteLine($"File exists after save: {File.Exists(_configPath)}");
        System.Diagnostics.Debug.WriteLine($"Saved config to {_configPath}");
    }
    private void CopyBuiltinStaticIfNeeded()
    {
        if (File.Exists(_staticPath))
            return;

        // 尝试从应用包内的 BuiltinData 文件夹复制
        string builtinPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BuiltinData", "ships_static.json");
        if (File.Exists(builtinPath))
        {
            File.Copy(builtinPath, _staticPath);
            System.Diagnostics.Debug.WriteLine($"已从内置资源复制默认静态数据到 {_staticPath}");
            return;
        }

        // 如果内置文件也不存在（例如开发环境未包含），则创建空模板
        var emptyData = new StaticData { Version = "0.0", Ships = new List<ShipStatic>() };
        var json = JsonSerializer.Serialize(emptyData, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_staticPath, json);
        System.Diagnostics.Debug.WriteLine($"未找到内置文件，创建空静态文件: {_staticPath}");
    }

    private void EnsureStaticFileExists()
    {
        if (File.Exists(_staticPath)) return;

        string sourceStatic = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "static", "ships_static.json");
        if (File.Exists(sourceStatic))
        {
            try
            {
                File.Copy(sourceStatic, _staticPath);
                return;
            }
            catch { }
        }

        var emptyStatic = new StaticData { Version = "0.1", Ships = new List<ShipStatic>() };
        var json = JsonSerializer.Serialize(emptyStatic, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_staticPath, json);
    }

    public void Load()
    {
        // 1. 读取静态文件
        if (!File.Exists(_staticPath)) return;
        string rawJson = File.ReadAllText(_staticPath);
        System.Diagnostics.Debug.WriteLine($"Load started, static path: {_staticPath}");
        if (!File.Exists(_staticPath))
        {
            System.Diagnostics.Debug.WriteLine("Static file does not exist!");
            return;
        }
        System.Diagnostics.Debug.WriteLine($"JSON length: {rawJson.Length}");
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        Version = root.TryGetProperty("version", out var ver) ? ver.GetString() ?? "0.0" : "0.0";

        // 检查是否需要迁移
        bool needMigration = false;
        if (root.TryGetProperty("ships", out var shipsArray) && shipsArray.GetArrayLength() > 0)
        {
            var firstShip = shipsArray[0];
            needMigration = !firstShip.TryGetProperty("obtain_bonus_attr", out _);
        }

        if (needMigration)
        {
            MigrateStaticFile(rawJson);
            rawJson = File.ReadAllText(_staticPath);
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var staticData = JsonSerializer.Deserialize<StaticData>(rawJson, options);
        _staticShips = staticData?.Ships ?? new List<ShipStatic>();
        Version = staticData?.Version ?? "0.0";

        // 2. 读取用户状态
        foreach (var staticShip in _staticShips)
        {
            Ships.Add(new ShipViewModel(staticShip, new ShipState { Id = staticShip.Id }));
        }
        if (_accountManager != null && !string.IsNullOrEmpty(_accountManager.CurrentAccount))
        {
            string dataRoot = App.DataRoot;
            if (string.IsNullOrEmpty(dataRoot))
                dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzurLaneDex", "data");

            string usersDir = Path.Combine(dataRoot, "users");
            string userFolder = Path.Combine(usersDir, _accountManager.CurrentAccount);
            Directory.CreateDirectory(userFolder);
            _userStatePath = Path.Combine(userFolder, "ships_state.json");

            if (File.Exists(_userStatePath))
            {
                try
                {
                    var stateJson = File.ReadAllText(_userStatePath);
                    var stateList = JsonSerializer.Deserialize<StateList>(stateJson);
                    if (stateList?.States != null)
                    {
                        foreach (var state in stateList.States)
                            _userStates[state.Id] = state;
                    }
                }
                catch { }
            }
            foreach (var ship in Ships)
            {
                if (ship.Name == "泛用型布里" || ship.Name == "试作型布里MKII" || ship.Name == "特装型布里MKIII")
                {
                    ship.Breakthrough = 3;
                }
            }
        }

        // 3. 生成 ViewModel
        Ships.Clear();
        foreach (var staticShip in _staticShips)
        {
            if (!_userStates.TryGetValue(staticShip.Id, out var state))
                state = new ShipState { Id = staticShip.Id };
            Ships.Add(new ShipViewModel(staticShip, state));
        }
        // 检查版本格式是否符合新标准，不符合则刷新一次版本号
        if (ParseRevision(Version) < 0 || !Version.StartsWith("1.0."))
        {
            // 修订次数从0开始，避免误增
            Version = BuildVersion(_staticShips.Count, 0);
            SaveStatic();
        }
    }

    private void MigrateStaticFile(string oldJson)
    {
        using var doc = JsonDocument.Parse(oldJson);
        var root = doc.RootElement;
        if (!root.TryGetProperty("ships", out var oldShips) || oldShips.ValueKind != JsonValueKind.Array)
            return;

        var newShips = new List<ShipStatic>();
        foreach (var old in oldShips.EnumerateArray())
        {
            var newShip = MigrateSingleShip(old);
            newShips.Add(newShip);
        }

        var newStatic = new StaticData
        {
            Version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "0.0" : "0.0",
            Ships = newShips
        };
        var newJson = JsonSerializer.Serialize(newStatic, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_staticPath, newJson);
    }

    public static ShipStatic MigrateSingleShip(JsonElement old)
    {
        var attrMap = new (string Display, string Base)[]
        {
            ("耐久", "durability"), ("炮击", "firepower"), ("雷击", "torpedo"),
            ("防空", "aa"), ("航空", "aviation"), ("命中", "accuracy"),
            ("装填", "reload"), ("机动", "mobility"), ("反潜", "antisub")
        };

        string obtainAttr = "";
        int obtainValue = 0;
        foreach (var (display, baseKey) in attrMap)
        {
            if (old.TryGetProperty($"tech_{baseKey}_obtain", out var val) && val.ValueKind == JsonValueKind.Number && val.GetInt32() != 0)
            {
                obtainAttr = display;
                obtainValue = val.GetInt32();
                break;
            }
        }

        string level120Attr = "";
        int level120Value = 0;
        foreach (var (display, baseKey) in attrMap)
        {
            if (old.TryGetProperty($"tech_{baseKey}_120", out var val) && val.ValueKind == JsonValueKind.Number && val.GetInt32() != 0)
            {
                level120Attr = display;
                level120Value = val.GetInt32();
                break;
            }
        }

        var affects = new List<string>();
        if (old.TryGetProperty("tech_affects", out var affectsElem) && affectsElem.ValueKind == JsonValueKind.Array)
        {
            affects = affectsElem.EnumerateArray().Select(x => x.GetString()).Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        if (affects.Count == 0 && old.TryGetProperty("ship_class", out var scElem) && scElem.ValueKind == JsonValueKind.String)
        {
            string sc = scElem.GetString();
            if (!string.IsNullOrEmpty(sc)) affects.Add(sc);
        }

        var ship = new ShipStatic
        {
            Id = old.GetProperty("id").GetInt32(),
            Name = old.GetProperty("name").GetString() ?? "",
            AltName = old.TryGetProperty("alt_name", out var alt) ? alt.GetString() ?? "" : "",
            Faction = old.GetProperty("faction").GetString() ?? "",
            ShipClass = old.GetProperty("ship_class").GetString() ?? "",
            Rarity = old.GetProperty("rarity").GetString() ?? "",
            GameOrder = old.TryGetProperty("game_order", out var go) ? go.GetInt32() : 0,
            AcquireMain = old.TryGetProperty("acquire_main", out var am) ? am.GetString() ?? "" : "",
            AcquireDetail = old.TryGetProperty("acquire_detail", out var ad) ? ad.GetString() ?? "" : "",
            BuildTime = old.TryGetProperty("build_time", out var bt) ? bt.GetString() ?? "" : "",
            DropLocations = old.TryGetProperty("drop_locations", out var drops) && drops.ValueKind == JsonValueKind.Array
                ? drops.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
                : new List<string>(),
            ShopExchange = old.TryGetProperty("shop_exchange", out var se) ? se.GetString() ?? "" : "",
            IsPermanent = old.TryGetProperty("is_permanent", out var perm) && perm.GetBoolean(),
            DebutEvent = old.TryGetProperty("debut_event", out var de) ? de.GetString() ?? "" : "",
            ReleaseDate = old.TryGetProperty("release_date", out var rd) ? rd.GetString() ?? "" : "",
            Notes = old.TryGetProperty("notes", out var nt) ? nt.GetString() ?? "" : "",
            CanRemodel = old.TryGetProperty("can_remodel", out var cr) && cr.GetBoolean(),
            RemodelDate = old.TryGetProperty("remodel_date", out var rmd) ? rmd.GetString() ?? "" : "",
            CanSpecialGear = old.TryGetProperty("can_special_gear", out var csg) && csg.GetBoolean(),
            SpecialGearName = old.TryGetProperty("special_gear_name", out var sgn) ? sgn.GetString() ?? "" : "",
            SpecialGearDate = old.TryGetProperty("special_gear_date", out var sgd) ? sgd.GetString() ?? "" : "",
            SpecialGearAcquire = old.TryGetProperty("special_gear_acquire", out var sga) ? sga.GetString() ?? "" : "",
            ImagePath = old.TryGetProperty("image_path", out var img) ? img.GetString() ?? "" : "",
            TechPointsObtain = old.TryGetProperty("tech_points_obtain", out var tpO) ? tpO.GetInt32() : 0,
            TechPointsMax = old.TryGetProperty("tech_points_max", out var tpM) ? tpM.GetInt32() : 0,
            TechPoints120 = old.TryGetProperty("tech_points_120", out var tp120) ? tp120.GetInt32() : 0,
            ObtainBonusAttr = obtainAttr,
            ObtainBonusValue = obtainValue,
            ObtainAffects = affects,
            Level120BonusAttr = level120Attr,
            Level120BonusValue = level120Value,
            Level120Affects = affects
        };
        return ship;
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(_userStatePath)) return;

        var stateList = new StateList
        {
            States = Ships.Select(vm => vm.GetState()).ToList()
        };
        var json = JsonSerializer.Serialize(stateList, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_userStatePath, json);
        StateChanged?.Invoke();
    }

    public void SwitchAccount(string accountName)
    {
        _accountManager.SetCurrentAccount(accountName); // 如果调用者已经设置了，此行可省
        _userStatePath = Path.Combine(App.DataRoot, "users", accountName, "ships_state.json");
        Load(); // 重新加载数据
        data_changed?.Invoke(); // 通知所有订阅者刷新
    }

    public void ExportStatic(string filePath)
    {
        var data = new StaticData { Version = Version, Ships = _staticShips };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    public Dictionary<string, CampTechData> CalculateCampTechPoints()
    {
        var result = new Dictionary<string, CampTechData>();
        foreach (var ship in Ships.Where(s => s.Owned))
        {
            string faction = ship.Faction;
            if (!result.ContainsKey(faction))
                result[faction] = new CampTechData();
            var data = result[faction];
            data.Obtain += ship.TechPointsObtain;
            if (ship.IsMaxBreakthrough)
                data.Max += ship.TechPointsMax;
            if (ship.Level120)
                data.Level120 += ship.TechPoints120;
        }
        return result;
    }

    public int GetTotalTechPoints()
    {
        return Ships.Sum(s => s.TechPointsObtain + s.TechPointsMax + s.TechPoints120);
    }

    public class CampTechData
    {
        public int Obtain { get; set; }
        public int Max { get; set; }
        public int Level120 { get; set; }
    }
    public int GetOwnedTechPoints()
    {
        int total = 0;
        foreach (var ship in Ships.Where(s => s.Owned))
        {
            total += ship.TechPointsObtain;
            if (ship.IsMaxBreakthrough)
                total += ship.TechPointsMax;
            if (ship.Level120)
                total += ship.TechPoints120;
        }
        return total;
    }

    public event Action data_changed;

    public StatsData stats()
    {
        var stats = new StatsData();
        stats.Total = Ships.Count;
        stats.Owned = Ships.Count(s => s.Owned);
        stats.NotOwned = stats.Total - stats.Owned;
        stats.MaxBreakthrough = Ships.Count(s => s.IsMaxBreakthrough);
        stats.NotMaxBreakthrough = Ships.Count(s => s.Owned && !s.IsMaxBreakthrough);
        stats.Oath = Ships.Count(s => s.Oath);
        stats.Remodeled = Ships.Count(s => s.Remodeled);
        stats.CanRemodelNot = Ships.Count(s => s.CanRemodel && !s.Remodeled);
        stats.Level120 = Ships.Count(s => s.Level120);
        stats.SpecialGearObtained = Ships.Count(s => s.SpecialGearObtained);
        stats.SpecialGearNotObtained = Ships.Count(s => s.CanSpecialGear && !s.SpecialGearObtained);
        stats.CanRemodelTotal = Ships.Count(s => s.CanRemodel);
        return stats;
    }

    public Dictionary<(string ShipClass, string Attr), int> CalculateGlobalBonuses()
    {
        var bonuses = new Dictionary<(string, string), int>();
        foreach (var ship in Ships.Where(s => s.Owned))
        {
            // 获得时加成
            if (!string.IsNullOrEmpty(ship.ObtainBonusAttr) && ship.ObtainBonusValue != 0 && ship.ObtainAffects.Any())
            {
                foreach (var sc in ship.ObtainAffects)
                {
                    var key = (sc, ship.ObtainBonusAttr);
                    bonuses[key] = bonuses.GetValueOrDefault(key) + ship.ObtainBonusValue;
                }
            }
            // 120级加成
            if (!string.IsNullOrEmpty(ship.Level120BonusAttr) && ship.Level120BonusValue != 0 && ship.Level120Affects.Any())
            {
                foreach (var sc in ship.Level120Affects)
                {
                    var key = (sc, ship.Level120BonusAttr);
                    bonuses[key] = bonuses.GetValueOrDefault(key) + ship.Level120BonusValue;
                }
            }
        }
        return bonuses;
    }
    public class StatsData
    {
        public int Total { get; set; }
        public int Owned { get; set; }
        public int NotOwned { get; set; }
        public int MaxBreakthrough { get; set; }
        public int NotMaxBreakthrough { get; set; }
        public int Oath { get; set; }
        public int Remodeled { get; set; }
        public int CanRemodelNot { get; set; }
        public int Level120 { get; set; }
        public int SpecialGearObtained { get; set; }
        public int SpecialGearNotObtained { get; set; }
        public int CanRemodelTotal { get; set; }
    }
    public bool AddShip(ShipStatic newShip)
    {
        // 1. 权限检查（需要 AccountManager 有 IsDeveloper 方法）
        if (!_accountManager.IsDeveloper())
            throw new InvalidOperationException("只有开发者账户才能新增舰船");

        // 2. 处理 ID
        if (newShip.Id == 0)
        {
            int maxId = _staticShips.Max(s => s.Id);
            newShip.Id = maxId + 1;
        }
        else
        {
            if (_staticShips.Any(s => s.Id == newShip.Id))
            {
                // ID 冲突，自动重新分配
                int maxId = _staticShips.Max(s => s.Id);
                newShip.Id = maxId + 1;
                // 可选：弹出提示告知用户 ID 被重新分配
            }
        }

        // 3. 处理 game_order 冲突
        if (newShip.GameOrder == 0)
        {
            newShip.GameOrder = _staticShips.Count > 0 ? _staticShips.Max(s => s.GameOrder) + 1 : 1;
        }
        else
        {
            // 如果指定的 game_order 已被占用，将该序号及之后的船向后顺移
            var conflict = _staticShips.FirstOrDefault(s => s.GameOrder == newShip.GameOrder);
            if (conflict != null)
            {
                foreach (var ship in _staticShips.Where(s => s.GameOrder >= newShip.GameOrder))
                    ship.GameOrder++;
                // 重新排序
                _staticShips = _staticShips.OrderBy(s => s.GameOrder).ToList();
            }
        }

        // 4. 添加到列表并排序
        _staticShips.Add(newShip);
        _staticShips = _staticShips.OrderBy(s => s.GameOrder).ToList();

        // 5. 保存静态文件
        SaveStatic();

        // 6. 为用户状态添加默认项
        var newState = new ShipState { Id = newShip.Id };
        _userStates[newShip.Id] = newState;
        var newViewModel = new ShipViewModel(newShip, newState);
        Ships.Add(newViewModel);

        // 7. 保存用户状态
        Save();
        DataStructureChanged?.Invoke();
        return true;
    }

    private void SaveStatic()
    {
        UpdateVersionBeforeSave();
        var data = new StaticData { Version = Version, Ships = _staticShips };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_staticPath, json);
    }
    public void UpdateShip(int oldId, ShipStatic newShip)
    {
        if (!_accountManager.IsDeveloper())
            throw new InvalidOperationException("只有开发者账户才能编辑舰船");

        int index = _staticShips.FindIndex(s => s.Id == oldId);
        if (index == -1) return;

        // 处理 ID 变更
        if (newShip.Id != oldId && _staticShips.Any(s => s.Id == newShip.Id))
        {
                // ID 冲突，自动分配新 ID
                int maxId = _staticShips.Max(s => s.Id);
                newShip.Id = maxId + 1;
        }

        // 处理 game_order 冲突（如果更改）
        if (newShip.GameOrder != _staticShips[index].GameOrder)
        {
            var conflict = _staticShips.FirstOrDefault(s => s.Id != oldId && s.GameOrder == newShip.GameOrder);
            if (conflict != null)
            {
                // 向后移动冲突及之后的舰船（排除自身）
                foreach (var ship in _staticShips.Where(s => s.Id != oldId && s.GameOrder >= newShip.GameOrder))
                    ship.GameOrder++;
            }
        }

        // 替换并重新排序
        _staticShips[index] = newShip;
        _staticShips = _staticShips.OrderBy(s => s.GameOrder).ToList();
        SaveStatic();

        // 更新用户状态映射（如果 ID 变化）
        if (newShip.Id != oldId)
        {
            if (_userStates.TryGetValue(oldId, out var state))
            {
                _userStates.Remove(oldId);
                _userStates[newShip.Id] = state;
            }
        }

        // 更新 Ships 集合中的 ViewModel
        var oldVm = Ships.FirstOrDefault(vm => vm.Id == oldId);
        if (oldVm != null)
        {
            var newVm = new ShipViewModel(newShip, oldVm.GetState());
            int vmIndex = Ships.IndexOf(oldVm);
            Ships[vmIndex] = newVm;
        }

        Save();
        DataStructureChanged?.Invoke();
    }
    public void DeleteShip(int shipId)
    {
        if (!_accountManager.IsDeveloper())
            throw new InvalidOperationException("只有开发者账户才能删除舰船");

        // 从静态列表中删除
        var removed = _staticShips.RemoveAll(s => s.Id == shipId) > 0;
        if (!removed) return;

        SaveStatic();

        // 从用户状态中删除
        _userStates.Remove(shipId);

        // 从 Ships 集合中删除 ViewModel
        var vm = Ships.FirstOrDefault(v => v.Id == shipId);
        if (vm != null) Ships.Remove(vm);

        Save();
        DataStructureChanged?.Invoke();
    }
    public void NotifyDataChanged()
    {
        data_changed?.Invoke();
    }
    // 舰船静态数据版本号
    // 生成新版本字符串
    private string BuildVersion(int shipCount, int revision)
    {
        string date = DateTime.Now.ToString("yyyyMMdd");
        return $"1.0.{shipCount}.{revision}.{date}";
    }

    // 从当前版本号尝试提取修订次数，若失败返回 -1
    private int ParseRevision(string version)
    {
        try
        {
            var parts = version.Split('.');
            if (parts.Length == 5 && int.TryParse(parts[2], out _) && int.TryParse(parts[3], out int rev))
                return rev;
        }
        catch { }
        return -1;
    }

    // 更新版本号（在保存静态数据前调用）
    private void UpdateVersionBeforeSave()
    {
        int shipCount = _staticShips.Count;
        int revision = ParseRevision(Version);
        if (revision < 0) revision = 0;   // 无法解析则从0开始
        else revision++;

        Version = BuildVersion(shipCount, revision);
    }
    // 获取程序版本号
    public string GetCurrentAppVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
        }
        catch
        {
            // 非打包运行时的回退
            var assemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return assemblyVersion?.ToString() ?? "0.0.0.0";
        }
    }

    // 获取远程数据版本
    public async Task<string> GetRemoteDataVersionAsync(string url, string proxy = "")
    {
        using var client = CreateHttpClient(proxy);
        var json = await client.GetStringAsync(url);
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
    }

    // 更新数据
    public async Task<bool> UpdateDataFromUrlAsync(string url, string proxy = "")
    {
        using var client = CreateHttpClient(proxy);
        var json = await client.GetStringAsync(url);
        var remoteData = JsonSerializer.Deserialize<StaticData>(json);
        if (remoteData?.Ships == null) return false;
        if (File.Exists(_staticPath))
            File.Copy(_staticPath, _staticPath + ".bak", true);
        File.WriteAllText(_staticPath, json);
        Load();
        DataStructureChanged?.Invoke();
        return true;
    }
    public async Task<string> GetLatestAppVersionAsync(string proxy = "")
    {
        HttpClient client = CreateHttpClient(proxy);
        using (client)
        {
            client.DefaultRequestHeaders.Add("User-Agent", "AzurLaneDex");
            var response = await client.GetStringAsync("https://api.github.com/repos/xiwangzaiqianfang/AzurLane-Dex/releases/latest");
            var json = JsonDocument.Parse(response);
            var tag = json.RootElement.GetProperty("tag_name").GetString();
            return tag?.TrimStart('v') ?? "0.0.0";
        }
    }
    public HttpClient CreateHttpClient(string proxy)
    {
        if (string.IsNullOrEmpty(proxy))
            return new HttpClient();
        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy(proxy),
            UseProxy = true
        };
        return new HttpClient(handler);
    }
    public async Task<string> DownloadStringAsync(string url, string proxy = "")
    {
        using var client = CreateHttpClient(proxy);
        return await client.GetStringAsync(url);
    }
}