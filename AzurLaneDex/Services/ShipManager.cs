using AzurLaneDex.Models;
using AzurLaneDex.ViewModels;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using static AzurLaneDex.Models.ShipStatic;

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
        if (staticData?.Ships != null)
        {
            var testShip = staticData.Ships.FirstOrDefault(s => s.Id == 20001);
            if (testShip != null)
            {
                System.Diagnostics.Debug.WriteLine($"Test Ship: {testShip.Name}, Category = {testShip.Category}");
            }
        }
        _staticShips = staticData?.Ships ?? new List<ShipStatic>();
        Version = staticData?.Version ?? "0.0";

        // 检测是否需要填充 CategoryOrder（如果第一条船的 CategoryOrder == 0 且 Category == Normal，说明可能未迁移）
        bool needCategoryMigration = false;
        if (_staticShips.Count > 0)
        {
            var first = _staticShips[0];
            // 判断是否需要迁移：CategoryOrder 为 0 且 Category 为 Normal（普通船），或者任意船的 Category 为 Normal 但 GameOrder 可能与 CategoryOrder 不同
            // 更简单的判断：如果存在任何船 CategoryOrder == 0 且 (Category == Normal 且 GameOrder != 0) 或 (Category != Normal)
            needCategoryMigration = _staticShips.Any(s => s.CategoryOrder == 0) ||
                                    _staticShips.Any(s => s.Category == ShipCategory.Normal && s.CategoryOrder == 0 && s.GameOrder != 0) ||
                                    _staticShips.Any(s => s.Category != ShipCategory.Normal && s.CategoryOrder == 0);
        }

        if (needCategoryMigration)
        {
            if (MigrateCategoriesAndOrders())
            {
                SaveStatic();  // 保存更新后的静态文件
                               // 重新读取更新后的数据（确保 _staticShips 已更新）
                var updatedJson = File.ReadAllText(_staticPath);
                staticData = JsonSerializer.Deserialize<StaticData>(updatedJson, options);
                _staticShips = staticData?.Ships ?? new List<ShipStatic>();
                Version = staticData?.Version ?? "0.0";
            }
        }

        // 2. 读取用户状态        
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
        }

        // 添加 ID 迁移（一次性）
        MigrateSpecialShipIds();

        // 3. 生成 ViewModel
        Ships.Clear();
        foreach (var staticShip in _staticShips)
        {
            if (!_userStates.TryGetValue(staticShip.Id, out var state))
                state = new ShipState { Id = staticShip.Id };
            Ships.Add(new ShipViewModel(staticShip, state));
        }
        foreach (var ship in Ships)
        {
            if (ship.Name == "泛用型布里" || ship.Name == "试作型布里MKII" || ship.Name == "特装型布里MKIII")
            {
                ship.Breakthrough = 3;
            }
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

    private void MigrateSpecialShipIds()
    {
        // 检查是否已经迁移过：如果存在任何特殊船的 ID 在对应范围内，则跳过
        if (_staticShips.Any(s => s.Category == ShipCategory.META && s.Id >= ShipIdRanges.MetaStart && s.Id < ShipIdRanges.CollabStart))
            return;
        if (_staticShips.Any(s => s.Category == ShipCategory.Collab && s.Id >= ShipIdRanges.CollabStart && s.Id < ShipIdRanges.ResearchStart))
            return;
        if (_staticShips.Any(s => s.Category == ShipCategory.Research && s.Id >= ShipIdRanges.ResearchStart))
            return;
        // 记录每个类别的下一个可用 ID
        int nextMetaId = ShipIdRanges.MetaStart;
        int nextCollabId = ShipIdRanges.CollabStart;
        int nextResearchId = ShipIdRanges.ResearchStart;

        bool changed = false;
        var idMapping = new Dictionary<int, int>(); // 旧ID -> 新ID

        foreach (var ship in _staticShips)
        {
            int newId = ship.Id;
            switch (ship.Category)
            {
                case ShipCategory.META:
                    if (ship.Id < ShipIdRanges.MetaStart || ship.Id >= ShipIdRanges.CollabStart)
                    {
                        newId = nextMetaId++;
                        idMapping[ship.Id] = newId;
                        ship.Id = newId;
                        changed = true;
                    }
                    else
                    {
                        nextMetaId = Math.Max(nextMetaId, ship.Id + 1);
                    }
                    break;
                case ShipCategory.Collab:
                    if (ship.Id < ShipIdRanges.CollabStart || ship.Id >= ShipIdRanges.ResearchStart)
                    {
                        newId = nextCollabId++;
                        idMapping[ship.Id] = newId;
                        ship.Id = newId;
                        changed = true;
                    }
                    else
                    {
                        nextCollabId = Math.Max(nextCollabId, ship.Id + 1);
                    }
                    break;
                case ShipCategory.Research:
                    if (ship.Id < ShipIdRanges.ResearchStart)
                    {
                        newId = nextResearchId++;
                        idMapping[ship.Id] = newId;
                        ship.Id = newId;
                        changed = true;
                    }
                    else
                    {
                        nextResearchId = Math.Max(nextResearchId, ship.Id + 1);
                    }
                    break;
                default:
                    // 普通船如果误入特殊段，也修正（可选）
                    if (ship.Id >= ShipIdRanges.MetaStart)
                    {
                        // 重新分配一个普通段内的ID，简单处理：找最大普通ID+1
                        int maxNormalId = _staticShips.Where(s => s.Category == ShipCategory.Normal).Max(s => s.Id);
                        newId = maxNormalId + 1;
                        idMapping[ship.Id] = newId;
                        ship.Id = newId;
                        changed = true;
                    }
                    break;
            }
        }

        if (changed)
        {
            // 更新用户状态中的 ID
            var newUserStates = new Dictionary<int, ShipState>();
            foreach (var kv in _userStates)
            {
                if (idMapping.TryGetValue(kv.Key, out int newId))
                    newUserStates[newId] = kv.Value;
                else
                    newUserStates[kv.Key] = kv.Value;
            }
            _userStates = newUserStates;

            // 重新生成 Ships 集合中的 ViewModel（因为 ID 变了）
            RebuildShips();

            SaveStatic();
            Save();
            DataStructureChanged?.Invoke();
        }
    }
    private void RebuildShips()
    {
        Ships.Clear();
        foreach (var staticShip in _staticShips)
        {
            if (!_userStates.TryGetValue(staticShip.Id, out var state))
                state = new ShipState { Id = staticShip.Id };
            var vm = new ShipViewModel(staticShip, state);
            Ships.Add(vm);
        }
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
        LogService.Operation("用户状态变更", "数据存储");
    }

    public void SwitchAccount(string accountName)
    {
        _accountManager.SetCurrentAccount(accountName); // 如果调用者已经设置了，此行可省
        _userStatePath = Path.Combine(App.DataRoot, "users", accountName, "ships_state.json");
        Load(); // 重新加载数据
        LogService.Operation("用户登录", $"{accountName}");
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
        var shipsToCount = Ships.Where(s => s.Category != ShipCategory.Collab).ToList();
        var stats = new StatsData();
        stats.Total = shipsToCount.Count;
        stats.Owned = shipsToCount.Count(s => s.Owned);
        stats.NotOwned = stats.Total - stats.Owned;
        stats.MaxBreakthrough = shipsToCount.Count(s => s.IsMaxBreakthrough);
        stats.NotMaxBreakthrough = shipsToCount.Count(s => s.Owned && !s.IsMaxBreakthrough);
        stats.Oath = shipsToCount.Count(s => s.Oath);
        stats.Remodeled = shipsToCount.Count(s => s.Remodeled);
        stats.CanRemodelNot = shipsToCount.Count(s => s.CanRemodel && !s.Remodeled);
        stats.Level120 = shipsToCount.Count(s => s.Level120);
        stats.SpecialGearObtained = shipsToCount.Count(s => s.SpecialGearObtained);
        stats.SpecialGearNotObtained = shipsToCount.Count(s => s.CanSpecialGear && !s.SpecialGearObtained);
        stats.CanRemodelTotal = shipsToCount.Count(s => s.CanRemodel);
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
            int newId = GetNextIdForCategory(newShip.Category);
            newShip.Id = newId;
        }
        else
        {
            if (!IsIdValidForCategory(newShip.Id, newShip.Category))
            {
                // ID 冲突，自动重新分配
                newShip.Id = GetNextIdForCategory(newShip.Category);
                // 可选：弹出提示告知用户 ID 被重新分配
            }
            else if (_staticShips.Any(s => s.Id == newShip.Id))
            {
                newShip.Id = GetNextIdForCategory(newShip.Category);
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

        // 处理 CategoryOrder 自动分配
        if (newShip.CategoryOrder == 0)
        {
            // 获取当前类别下最大的 CategoryOrder
            int maxOrder = _staticShips
                .Where(s => s.Category == newShip.Category)
                .Select(s => s.CategoryOrder)
                .DefaultIfEmpty(0)
                .Max();
            newShip.CategoryOrder = maxOrder + 1;
        }
        else
        {
            // 如果指定的 order 已被占用，将该 order 及之后的船向后顺移（仅限同一类别）
            var conflict = _staticShips.FirstOrDefault(s => s.Category == newShip.Category && s.CategoryOrder == newShip.CategoryOrder);
            if (conflict != null)
            {
                foreach (var ship in _staticShips.Where(s => s.Category == newShip.Category && s.CategoryOrder >= newShip.CategoryOrder))
                    ship.CategoryOrder++;
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
        LogService.Operation("新增舰船", $"{newShip.Name} (ID: {newShip.Id})");
        return true;
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
        {
            if (!existingIds.Contains(id))
                return id;
        }
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

        var oldShip = _staticShips[index];

        // 1. 处理 ID 变更
        if (newShip.Id != oldId && _staticShips.Any(s => s.Id == newShip.Id))
        {
            // ID 冲突，自动分配新 ID
            int maxId = _staticShips.Max(s => s.Id);
            newShip.Id = maxId + 1;
        }

        // 2. 处理 Category 和 CategoryOrder 变更
        // 注意：如果类别或顺序发生变化，需要调整其他舰船的顺序
        bool categoryChanged = oldShip.Category != newShip.Category;
        bool orderChanged = oldShip.CategoryOrder != newShip.CategoryOrder;

        if (categoryChanged || orderChanged)
        {
            // 2.1 先从旧类别中移除旧船的顺序（将大于旧顺序的船前移）
            if (oldShip.CategoryOrder > 0)
            {
                foreach (var ship in _staticShips.Where(s => s.Category == oldShip.Category && s.CategoryOrder > oldShip.CategoryOrder))
                {
                    ship.CategoryOrder--;
                }
            }

            // 2.2 处理新船的顺序值
            if (newShip.CategoryOrder == 0)
            {
                // 自动分配：取当前类别中最大的 CategoryOrder + 1
                int maxOrder = _staticShips
                    .Where(s => s.Category == newShip.Category)
                    .Select(s => s.CategoryOrder)
                    .DefaultIfEmpty(0)
                    .Max();
                newShip.CategoryOrder = maxOrder + 1;
            }
            else
            {
                // 检查新顺序是否与同一类别下的其他船冲突（排除自身）
                var conflict = _staticShips.FirstOrDefault(s => s.Id != oldId && s.Category == newShip.Category && s.CategoryOrder == newShip.CategoryOrder);
                if (conflict != null)
                {
                    // 冲突：将该顺序及之后的船向后顺移
                    foreach (var ship in _staticShips.Where(s => s.Id != oldId && s.Category == newShip.Category && s.CategoryOrder >= newShip.CategoryOrder))
                    {
                        ship.CategoryOrder++;
                    }
                }
            }
        }
        else
        {
            // 类别和顺序都没变，但仍需确保 CategoryOrder 不为 0（如果是普通船且 GameOrder 可能为 0，此处可保留）
            if (newShip.CategoryOrder == 0 && newShip.Category == ShipCategory.Normal)
            {
                newShip.CategoryOrder = newShip.GameOrder;
            }
        }

        // 3. 替换原数据
        _staticShips[index] = newShip;

        // 4. 重新排序（按 GameOrder 和 CategoryOrder 混合排序？实际上内部顺序只需按 CategoryOrder 全局排序即可，因为不同类别不会混合）
        // 但为了保持整体一致性，我们直接按 Id 重新排序（或者保留原有顺序）。这里只保存即可，不重新排序 List。
        // 如果需要确保顺序正确，可以按 CategoryOrder 排序：
        _staticShips = _staticShips.OrderBy(s => s.Category).ThenBy(s => s.CategoryOrder).ToList();

        // 5. 保存静态文件
        SaveStatic();

        // 6. 更新用户状态映射（如果 ID 变化）
        if (newShip.Id != oldId)
        {
            if (_userStates.TryGetValue(oldId, out var state))
            {
                _userStates.Remove(oldId);
                _userStates[newShip.Id] = state;
            }
        }

        // 7. 更新 Ships 集合中的 ViewModel
        var oldVm = Ships.FirstOrDefault(vm => vm.Id == oldId);
        if (oldVm != null)
        {
            var newVm = new ShipViewModel(newShip, oldVm.GetState());
            int vmIndex = Ships.IndexOf(oldVm);
            Ships[vmIndex] = newVm;
        }

        // 8. 保存用户状态（确保 ID 映射正确）
        Save();
        LogService.Operation("编辑舰船", $"{newShip.Name} (ID: {newShip.Id})");

        // 9. 触发数据变更事件
        DataStructureChanged?.Invoke();
    }
    private bool MigrateCategoriesAndOrders()
    {
        bool changed = false;
        foreach (var ship in _staticShips)
        {
            // 跳过已经填充过的船（已有非零的 CategoryOrder 或者 Category 不为 Normal）
            if (ship.Category != ShipCategory.Normal) continue;

            ShipCategory? detectedCategory = null;
            int order = 0;

            // 1. 从 Name 或 AltName 中检测类别和编号
            string nameToCheck = ship.Name ?? "";
            string altNameToCheck = ship.AltName ?? "";

            // 匹配 META: NO.METAxxx 或 META_xxx 或类似
            var metaMatch = Regex.Match(nameToCheck, @"NO\.META(\d+)", RegexOptions.IgnoreCase);
            if (!metaMatch.Success) metaMatch = Regex.Match(altNameToCheck, @"NO\.META(\d+)", RegexOptions.IgnoreCase);
            if (metaMatch.Success)
            {
                detectedCategory = ShipCategory.META;
                order = int.Parse(metaMatch.Groups[1].Value);
            }

            // 匹配科研: NO.Planxxx 或 科研-xxx 等
            if (!detectedCategory.HasValue)
            {
                var planMatch = Regex.Match(nameToCheck, @"NO\.Plan(\d+)", RegexOptions.IgnoreCase);
                if (!planMatch.Success) planMatch = Regex.Match(altNameToCheck, @"NO\.Plan(\d+)", RegexOptions.IgnoreCase);
                if (planMatch.Success)
                {
                    detectedCategory = ShipCategory.Research;
                    order = int.Parse(planMatch.Groups[1].Value);
                }
            }

            // 匹配联动: NO.Collabxxx 或 联动_xxx
            if (!detectedCategory.HasValue)
            {
                var collabMatch = Regex.Match(nameToCheck, @"NO\.Collab(\d+)", RegexOptions.IgnoreCase);
                if (!collabMatch.Success) collabMatch = Regex.Match(altNameToCheck, @"NO\.Collab(\d+)", RegexOptions.IgnoreCase);
                if (collabMatch.Success)
                {
                    detectedCategory = ShipCategory.Collab;
                    order = int.Parse(collabMatch.Groups[1].Value);
                }
            }

            // 若仍未检测到，根据阵营 Faction == "META" 判定为 META
            if (!detectedCategory.HasValue && ship.Faction == "META")
            {
                detectedCategory = ShipCategory.META;
                // 尝试从名称中提取数字，否则使用 Id 作为顺序（临时）
                var anyNumber = Regex.Match(nameToCheck, @"\d+");
                order = anyNumber.Success ? int.Parse(anyNumber.Value) : ship.Id;
            }

            // 剩余归类为普通
            if (!detectedCategory.HasValue)
            {
                detectedCategory = ShipCategory.Normal;
                order = ship.GameOrder;
            }

            // 赋值
            if (ship.Category != detectedCategory.Value)
            {
                ship.Category = detectedCategory.Value;
                changed = true;
            }
            if (ship.CategoryOrder != order)
            {
                ship.CategoryOrder = order;
                changed = true;
            }
        }

        // 特别处理：如果科研船有特定的顺序（例如按开发顺序），可以手动调整个别船的 order
        // 例如手动维护一个字典，覆盖科研船的顺序（如果需要）
        // 这里留作扩展点

        return changed;
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
        LogService.Operation("删除舰船", $"ID: {shipId}");
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
        LogService.Operation("数据更新", "结束");
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