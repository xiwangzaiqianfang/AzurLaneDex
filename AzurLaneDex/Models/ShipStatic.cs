using AzurLaneDex.Helpers;
using Microsoft.UI.Xaml.Shapes;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Windows.Storage.Search;

namespace AzurLaneDex.Models
{
    public class ShipStatic
    {
        // 基础
        [JsonPropertyName("global_name")] public string GlobalName { get; set; } = ""; // 全局名称
        [JsonPropertyName("id")] public int Id { get; set; } // 内部 ID
        [JsonPropertyName("game_order")] public int GameOrder { get; set; } // 游戏排序
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new(); // 名称
        [JsonPropertyName("alt_name")] public LocalizedString AltName { get; set; } = new(); // 和谐名
        [JsonPropertyName("alias")] public string Alias { get; set; } = ""; // 其他别称
        [JsonPropertyName("class")] public LocalizedString Class { get; set; } = new(); // 舰船级别
        // 静态
        [JsonPropertyName("category")] public ShipCategory Category { get; set; } // 类型
        [JsonPropertyName("ship_type")] public ShipType Type { get; set; } // 舰种枚举
        [JsonPropertyName("rarity")] public Rarity Rarity { get; set; } // 稀有度
        [JsonPropertyName("faction")] public Faction Faction { get; set; } // 阵营
        [JsonPropertyName("release_date")] public string ReleaseDate { get; set; } = ""; // 实装日期
        [JsonPropertyName("is_permanent")] public bool IsPermanent { get; set; } // 常驻
        // 属性
        [JsonPropertyName("stats")] public ShipStats Stats { get; set; } = new(); // 基础属性
        [JsonPropertyName("performance_rating")] public PerformanceRating Performance { get; set; } = new(); // 性能评分
        // 获取方式
        [JsonPropertyName("acquisition")] public AcquisitionData Acquisition { get; set; } = new();
        // 装备
        [JsonPropertyName("equipment_slots")] public List<EquipmentSlot> EquipmentSlots { get; set; } = new(); // 槽位
        [JsonPropertyName("initial_equipment")] public List<InitialEquipment> InitialEquipment { get; set; } = new(); // 初始设备
        [JsonPropertyName("special_gear")] public SpecialGear SpecialGear { get; set; } // 专属兵装
        // 技能
        [JsonPropertyName("skills")] public List<Skill> Skills { get; set; } = new();
        // 改造
        [JsonPropertyName("retrofit")] public RetrofitData Retrofit { get; set; } = new();
        // 科研
        [JsonPropertyName("research")] public ResearchData Research { get; set; } = new();
        // 换装
        [JsonPropertyName("skins")] public List<Skin> Skins { get; set; } = new();
        // 台词
        [JsonPropertyName("lines")] public List<VoiceLine> Lines { get; set; } = new();
        // 其他
        [JsonPropertyName("cv")] public LocalizedString CV { get; set; } = new(); // 声优
        [JsonPropertyName("artist")] public string Artist { get; set; } = ""; //画师
        [JsonPropertyName("gift_preference")] public List<GiftPreference> GiftPreferences { get; set; } = new(); // 礼物偏好
        [JsonPropertyName("remarks")] public string Remarks { get; set; } = "";
        [JsonPropertyName("notes")] public string Notes { get; set; } = "";      // 备注
        [JsonPropertyName("related_event")] public string RelatedEvent { get; set; } = ""; // 实装活动
        [JsonPropertyName("reference_markdown")] public string ReferenceMarkdown { get; set; } = ""; // 相关资料
        // 强化/退役
        [JsonPropertyName("can_be_enhance_material")] public bool CanBeEnhanceMaterial { get; set; } // 能否被用于强化
        [JsonPropertyName("enhance_value")] public int EnhanceValue { get; set; } // 强化价值
        [JsonPropertyName("can_retire")] public bool CanRetire { get; set; } // 能否退役
        [JsonPropertyName("retirement_reward")] public string RetirementReward { get; set; } = ""; // 退役价值
        [JsonPropertyName("enhance_exp")] public EnhanceExp EnhanceExp { get; set; } = new(); // 强化所需经验
        [JsonPropertyName("enhance_items")] public List<int> EnhanceItems { get; set; } = new(); // 所需道具编号
        [JsonPropertyName("extra_enhance")] public string ExtraEnhance { get; set; } = ""; // 额外强化
        // 舰队科技
        [JsonPropertyName("fleet_tech")] public FleetTech FleetTech { get; set; } = new();
        [JsonPropertyName("fleetTechBonus")] public FleetTechBonus FleetTechBonus { get; set; } = new();
    }
    // 属性
    public class ShipStats
    {
        [JsonPropertyName("hp")] public int Hp { get; set; }
        [JsonPropertyName("armor")] public ArmorType Armor { get; set; } // 轻/中/重
        [JsonPropertyName("fp")] public int Fp { get; set; } // 炮击
        [JsonPropertyName("trp")] public int Trp { get; set; } // 雷击
        [JsonPropertyName("aa")] public int Aa { get; set; }  // 防空
        [JsonPropertyName("avi")] public int Avi { get; set; } // 航空
        [JsonPropertyName("hit")] public int Hit { get; set; } // 命中
        [JsonPropertyName("eva")] public int Eva { get; set; } // 机动
        [JsonPropertyName("asw")] public int Asw { get; set; } // 反潜
        [JsonPropertyName("luck")] public int Luck { get; set; }
        [JsonPropertyName("oil")] public int Oil { get; set; } // 油耗
        [JsonPropertyName("speed")] public double Speed { get; set; } // 航速
    }

    public class EnhanceExp
    {
        [JsonPropertyName("fp")] public int Fp { get; set; }   // 炮击
        [JsonPropertyName("trp")] public int Trp { get; set; } // 雷击
        [JsonPropertyName("avi")] public int Avi { get; set; } // 航空
        [JsonPropertyName("rld")] public int Rld { get; set; } // 装填
    }

    public class PerformanceRating
    {
        [JsonPropertyName("hp")] public PerformanceGrade Hp { get; set; } = PerformanceGrade.D;
        [JsonPropertyName("aa")] public PerformanceGrade Aa { get; set; } = PerformanceGrade.D;
        [JsonPropertyName("eva")] public PerformanceGrade Eva { get; set; } = PerformanceGrade.D;
        [JsonPropertyName("avi")] public PerformanceGrade Avi { get; set; } = PerformanceGrade.D;
        [JsonPropertyName("trp")] public PerformanceGrade Trp { get; set; } = PerformanceGrade.D;
        [JsonPropertyName("fp")] public PerformanceGrade Fp { get; set; } = PerformanceGrade.D;
    }
    public class FleetTech
    {
        [JsonPropertyName("collect_points")] public int CollectPoints { get; set; }
        [JsonPropertyName("limit_break_points")] public int LimitBreakPoints { get; set; }
        [JsonPropertyName("level120_points")] public int Level120Points { get; set; }
    }
    public class FleetTechBonus
    {
        [JsonPropertyName("obtain")]
        public TechBonusDetail Obtain { get; set; } = new();   // 获得时加成

        [JsonPropertyName("level120")]
        public TechBonusDetail Level120 { get; set; } = new(); // 120级时加成

        [JsonPropertyName("limitBreak")]
        public TechBonusDetail LimitBreak { get; set; } = new(); // 满突破加成（可选）
    }
    public class TechBonusDetail
    {
        // 属性加成数值（与 ShipStats 字段对应）、
        [JsonPropertyName("hp")] public int Hp { get; set; }
        [JsonPropertyName("fp")] public int Fp { get; set; }
        [JsonPropertyName("trp")] public int Trp { get; set; }
        [JsonPropertyName("avi")] public int Avi { get; set; }
        [JsonPropertyName("aa")] public int Aa { get; set; }
        [JsonPropertyName("hit")] public int Hit { get; set; }
        [JsonPropertyName("eva")] public int Eva { get; set; }
        [JsonPropertyName("asw")] public int Asw { get; set; }
        // 注意：不包含 Luck、Oil、Speed 等（一般不加）

        // 加成目标舰种列表（例如 [ShipType.DD, ShipType.CL]）
        [JsonPropertyName("targetTypes")]
        public List<ShipType> TargetTypes { get; set; } = new();
    }

    // 获取方式
    public class AcquisitionData
    {
        [JsonPropertyName("methods")]
        public List<AcquisitionMethod> Methods { get; set; } = new();
    }

    public class AcquisitionMethod : INotifyPropertyChanged
    {
        private AcquisitionMethodType _type;
        [JsonPropertyName("type")]
        public AcquisitionMethodType Type
        {
            get => _type;
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged();
                }
            }
        }

        // ---- 建造相关 ----
        private ConstructionPool? _pool;
        [JsonPropertyName("pool")]
        public ConstructionPool? Pool
        {
            get => _pool;
            set { if (_pool != value) { _pool = value; OnPropertyChanged(); } }
        }

        private int? _costCube;
        [JsonPropertyName("cost_cube")]
        public int? CostCube
        {
            get => _costCube;
            set { if (_costCube != value) { _costCube = value; OnPropertyChanged(); } }
        }

        private int? _costGold;
        [JsonPropertyName("cost_gold")]
        public int? CostGold
        {
            get => _costGold;
            set { if (_costGold != value) { _costGold = value; OnPropertyChanged(); } }
        }

        private string? _buildTime;
        [JsonPropertyName("build_time")]
        public string? BuildTime
        {
            get => _buildTime;
            set { if (_buildTime != value) { _buildTime = value; OnPropertyChanged(); } }
        }

        private double? _probability;
        [JsonPropertyName("probability")]
        public double? Probability
        {
            get => _probability;
            set { if (_probability != value) { _probability = value; OnPropertyChanged(); } }
        }

        private bool _isLimited;
        [JsonPropertyName("is_limited")]
        public bool IsLimited
        {
            get => _isLimited;
            set { if (_isLimited != value) { _isLimited = value; OnPropertyChanged(); } }
        }

        private string? _eventId;
        [JsonPropertyName("event_id")]
        public string? EventId
        {
            get => _eventId;
            set { if (_eventId != value) { _eventId = value; OnPropertyChanged(); } }
        }

        // ---- 掉落相关 ----
        private List<DropLocation>? _locations;
        [JsonPropertyName("locations")]
        public List<DropLocation>? Locations
        {
            get => _locations;
            set { if (_locations != value) { _locations = value; OnPropertyChanged(); } }
        }

        // ---- 兑换相关 ----
        private List<ExchangeEntry>? _shops;
        [JsonPropertyName("shops")]
        public List<ExchangeEntry>? Shops
        {
            get => _shops;
            set { if (_shops != value) { _shops = value; OnPropertyChanged(); } }
        }

        // ---- 科研相关 ----
        private int? _series;
        [JsonPropertyName("series")]
        public int? Series
        {
            get => _series;
            set { if (_series != value) { _series = value; OnPropertyChanged(); } }
        }

        private string? _researchRarity;
        [JsonPropertyName("research_rarity")]
        public string? ResearchRarity
        {
            get => _researchRarity;
            set { if (_researchRarity != value) { _researchRarity = value; OnPropertyChanged(); } }
        }

        private Faction? _researchFaction;
        [JsonPropertyName("research_faction")]
        public Faction? ResearchFaction
        {
            get => _researchFaction;
            set { if (_researchFaction != value) { _researchFaction = value; OnPropertyChanged(); } }
        }

        private int? _techPoints;
        [JsonPropertyName("tech_points")]
        public int? TechPoints
        {
            get => _techPoints;
            set { if (_techPoints != value) { _techPoints = value; OnPropertyChanged(); } }
        }

        // ---- 其他 ----
        private LocalizedString? _methodName;
        [JsonPropertyName("method_name")]
        public LocalizedString? MethodName
        {
            get => _methodName;
            set { if (_methodName != value) { _methodName = value; OnPropertyChanged(); } }
        }

        // ---- 通用 ----
        private LocalizedString? _notes;
        [JsonPropertyName("notes")]
        public LocalizedString? Notes
        {
            get => _notes;
            set { if (_notes != value) { _notes = value; OnPropertyChanged(); } }
        }

        private bool _isPrimary;
        [JsonPropertyName("is_primary")]
        public bool IsPrimary
        {
            get => _isPrimary;
            set { if (_isPrimary != value) { _isPrimary = value; OnPropertyChanged(); } }
        }

        // ---- 辅助属性（用于编辑界面，不序列化） ----
        [JsonIgnore]
        public int BuildHour
        {
            get => int.TryParse(BuildTime?.Split(':')[0], out var h) ? h : 0;
            set
            {
                BuildTime = $"{value:00}:{BuildMinute:00}:{BuildSecond:00}";
                OnPropertyChanged();
            }
        }
        [JsonIgnore]
        public int BuildMinute
        {
            get => int.TryParse(BuildTime?.Split(':')[1], out var m) ? m : 0;
            set
            {
                BuildTime = $"{BuildHour:00}:{value:00}:{BuildSecond:00}";
                OnPropertyChanged();
            }
        }
        [JsonIgnore]
        public int BuildSecond
        {
            get => int.TryParse(BuildTime?.Split(':')[2], out var s) ? s : 0;
            set
            {
                BuildTime = $"{BuildHour:00}:{BuildMinute:00}:{value:00}";
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public string NotesString
        {
            get => Notes?.GetLocalized() ?? "";
            set
            {
                Notes = StringToLocalizedString(value);
                OnPropertyChanged();
            }
        }

        [JsonIgnore]
        public string MethodNameString
        {
            get => MethodName?.GetLocalized() ?? "";
            set
            {
                MethodName = StringToLocalizedString(value);
                OnPropertyChanged();
            }
        }

        private static LocalizedString StringToLocalizedString(string value)
        {
            var result = new LocalizedString();
            if (!string.IsNullOrEmpty(value))
            {
                var parts = value.Split('|');
                if (parts.Length > 0) result["zh-Hans"] = parts[0].Trim();
                if (parts.Length > 1) result["zh-Hant"] = parts[1].Trim();
                if (parts.Length > 2) result["en"] = parts[2].Trim();
                if (parts.Length > 3) result["ja"] = parts[3].Trim();
            }
            return result;
        }

        // ---- INotifyPropertyChanged 实现 ----
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DropLocation
    {
        [JsonPropertyName("map")]
        public string Map { get; set; } = "";
        [JsonPropertyName("is_boss")]
        public bool IsBoss { get; set; }
        [JsonPropertyName("is_elite")]
        public bool IsElite { get; set; }
        [JsonPropertyName("is_war_archives")]
        public bool IsWarArchives { get; set; }
        [JsonPropertyName("archive_name")]
        public LocalizedString? ArchiveName { get; set; }
        [JsonPropertyName("has_60_drops")]
        public bool Has60Drops { get; set; }
    }

    public class ExchangeEntry
    {
        public ExchangeEntry()
        {
            Shop = ExchangeShop.Guild; // 默认值
            Currency = new LocalizedString();
            CustomShopName = new LocalizedString();
        }

        [JsonPropertyName("shop")]
        public ExchangeShop Shop { get; set; }

        [JsonPropertyName("custom_shop_name")]
        public LocalizedString? CustomShopName { get; set; } = new();

        private LocalizedString _currency = new();
        [JsonPropertyName("currency")]
        public LocalizedString Currency
        {
            get => _currency;
            set => _currency = value ?? new LocalizedString();
        }

        [JsonPropertyName("cost")]
        public int Cost { get; set; }

        [JsonPropertyName("event_id")]
        public string? EventId { get; set; }

        [JsonIgnore]
        public string CurrencyString
        {
            get
            {
                var c = Currency;
                return c != null && c.Count > 0 ? string.Join("|", c.Values) : "";
            }
            set
            {
                Currency = new LocalizedString();
                if (!string.IsNullOrEmpty(value))
                {
                    var parts = value.Split('|');
                    for (int i = 0; i < parts.Length && i < 4; i++)
                    {
                        string key = i switch { 0 => "zh-Hans", 1 => "zh-Hant", 2 => "en", 3 => "ja", _ => "" };
                        if (!string.IsNullOrEmpty(key))
                            Currency[key] = parts[i].Trim();
                    }
                }
            }
        }

        [JsonIgnore]
        public string CustomShopNameString
        {
            get
            {
                var c = CustomShopName;
                return c != null && c.Count > 0 ? string.Join("|", c.Values) : "";
            }
            set
            {
                CustomShopName = new LocalizedString();
                if (!string.IsNullOrEmpty(value))
                {
                    var parts = value.Split('|');
                    for (int i = 0; i < parts.Length && i < 4; i++)
                    {
                        string key = i switch { 0 => "zh-Hans", 1 => "zh-Hant", 2 => "en", 3 => "ja", _ => "" };
                        if (!string.IsNullOrEmpty(key))
                            CustomShopName[key] = parts[i].Trim();
                    }
                }
            }
        }
    }

    // 装备
    public class EquipmentSlot
    {
        [JsonPropertyName("slot_index")] public int SlotIndex { get; set; } // 1~6
        [JsonPropertyName("equip_type")] public string EquipType { get; set; } = "";
        [JsonPropertyName("efficiency")] public double Efficiency { get; set; }
        [JsonPropertyName("mounts")] public int Mounts { get; set; }
        [JsonPropertyName("preload_count")] public int PreloadCount { get; set; }
        [JsonPropertyName("special_gear_id")] public int SpecialGearId { get; set; } // 仅6号槽
    }

    public class InitialEquipment
    {
        [JsonPropertyName("slot_index")] public int SlotIndex { get; set; }
        [JsonPropertyName("equipment_id")] public int EquipmentId { get; set; }
        [JsonPropertyName("equipment_name")] public LocalizedString EquipmentName { get; set; } = new();
    }
    // 专属兵装
    public class SpecialGear
    {
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new();
        [JsonPropertyName("release_date")] public string ReleaseDate { get; set; } = "";
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("acquisition_method")] public string AcquisitionMethod { get; set; } = "";
    }
    // 技能
    public class Skill
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new();
        [JsonPropertyName("type")] public SkillType Type { get; set; }
        [JsonPropertyName("description")] public LocalizedString Description { get; set; } = new();
        [JsonPropertyName("level_values")] public List<List<string>> LevelValues { get; set; } = new();
        [JsonPropertyName("is_retrofit_enabled")] public bool IsRetrofitEnabled { get; set; }
        [JsonPropertyName("is_research_enabled")] public bool IsResearchEnabled { get; set; }
        [JsonPropertyName("research_require_level")] public int ResearchRequireLevel { get; set; }
        [JsonPropertyName("is_fate_enabled")] public bool IsFateEnabled { get; set; }
        [JsonPropertyName("fate_require_level")] public int FateRequireLevel { get; set; }
        [JsonIgnore]
        public string NameString
        {
            get => Name != null ? string.Join("|", Name.Values) : "";
            set
            {
                Name = new LocalizedString();
                if (!string.IsNullOrEmpty(value))
                {
                    var parts = value.Split('|');
                    if (parts.Length > 0) Name["zh-Hans"] = parts[0];
                    if (parts.Length > 1) Name["zh-Hant"] = parts[1];
                    if (parts.Length > 2) Name["en"] = parts[2];
                    if (parts.Length > 3) Name["ja"] = parts[3];
                }
            }
        }

        [JsonIgnore]
        public string DescriptionString
        {
            get => Description != null ? string.Join("|", Description.Values) : "";
            set
            {
                Description = new LocalizedString();
                if (!string.IsNullOrEmpty(value))
                {
                    var parts = value.Split('|');
                    if (parts.Length > 0) Description["zh-Hans"] = parts[0];
                    if (parts.Length > 1) Description["zh-Hant"] = parts[1];
                    if (parts.Length > 2) Description["en"] = parts[2];
                    if (parts.Length > 3) Description["ja"] = parts[3];
                }
            }
        }

        [JsonIgnore]
        public string LevelValuesString
        {
            get => LevelValues != null ? string.Join("\n", LevelValues.Select(lv => string.Join(",", lv))) : "";
            set
            {
                LevelValues = new List<List<string>>();
                if (!string.IsNullOrEmpty(value))
                {
                    foreach (var line in value.Split('\n'))
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                            LevelValues.Add(line.Split(',').Select(s => s.Trim()).ToList());
                    }
                }
            }
        }
    }
    // 改造
    public class RetrofitData
    {
        [JsonPropertyName("can_retrofit")] public bool CanRetrofit { get; set; }
        [JsonPropertyName("retrofit_release_date")] public string RetrofitReleaseDate { get; set; }
        [JsonPropertyName("ship_type_changed")] public bool ShipTypeChanged { get; set; }
        [JsonPropertyName("target_ship_type")] public ShipType TargetShipType { get; set; }
        [JsonPropertyName("nodes")] public List<RetrofitNode> Nodes { get; set; } = new();
    }

    public class RetrofitNode
    {
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new();
        [JsonPropertyName("attribute_bonus")] public Dictionary<string, int> AttributeBonus { get; set; } = new();
        [JsonPropertyName("required_items")] public List<int> RequiredItems { get; set; } = new();
        [JsonPropertyName("required_coins")] public int RequiredCoins { get; set; }
        [JsonPropertyName("required_level")] public int RequiredLevel { get; set; }
        [JsonPropertyName("required_stars")] public int RequiredStars { get; set; }
    }
    // 科研
    public class ResearchData
    {
        [JsonPropertyName("pre_requisite_factions")] public List<Faction> PreRequisiteFactions { get; set; } = new();
        [JsonPropertyName("tech_points")] public int TechPoints { get; set; }
        [JsonPropertyName("tasks")] public List<ResearchTask> Tasks { get; set; } = new();
        [JsonPropertyName("blueprint_required")] public int BlueprintRequired { get; set; }
        [JsonPropertyName("develop_bonus")] public Dictionary<int, string> DevelopBonus { get; set; } = new(); // 等级->描述
        [JsonPropertyName("develop_blueprint_required")] public int DevelopBlueprintRequired { get; set; }
        [JsonPropertyName("has_fate_simulation")] public bool HasFateSimulation { get; set; }
        [JsonPropertyName("fate_simulation")] public FateSimulation FateSim { get; set; } = new();
    }

    public class ResearchTask
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("requirement")] public string Requirement { get; set; } = "";
    }

    public class FateSimulation
    {
        [JsonPropertyName("level")] public int Level { get; set; }
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("blueprint_required")] public int BlueprintRequired { get; set; }
    }
    // 换装
    public class Skin
    {
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new();
        [JsonPropertyName("release_date")] public string ReleaseDate { get; set; } = "";
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("type")] public SkinType Type { get; set; }
        [JsonPropertyName("servers")] public List<string> Servers { get; set; } = new();
        [JsonPropertyName("is_oath_enabled")] public bool IsOathEnabled { get; set; }
        [JsonIgnore]
        public string NameText
        {
            get => Name != null ? string.Join("|", Name.Values) : "";
            set
            {
                Name = new LocalizedString();
                if (!string.IsNullOrEmpty(value))
                {
                    var parts = value.Split('|');
                    if (parts.Length > 0) Name["zh-Hans"] = parts[0];
                    if (parts.Length > 1) Name["zh-Hant"] = parts[1];
                    if (parts.Length > 2) Name["en"] = parts[2];
                    if (parts.Length > 3) Name["ja"] = parts[3];
                }
            }
        }
    }
    // 台词
    public class VoiceLine
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("content")] public LocalizedString Content { get; set; } = new();
        [JsonPropertyName("is_oath_enabled")] public bool IsOathEnabled { get; set; }
        [JsonPropertyName("is_skin_enabled")] public bool IsSkinEnabled { get; set; }
        [JsonPropertyName("skin_require_id")] public int SkinRequireId { get; set; }
        [JsonPropertyName("trigger_condition")] public string TriggerCondition { get; set; } = "";
        [JsonIgnore]
        public string ContentText
        {
            get => Content != null ? string.Join("|", Content.Values) : "";
            set
            {
                Content = new LocalizedString();
                if (!string.IsNullOrEmpty(value))
                {
                    var parts = value.Split('|');
                    if (parts.Length > 0) Content["zh-Hans"] = parts[0];
                    if (parts.Length > 1) Content["zh-Hant"] = parts[1];
                    if (parts.Length > 2) Content["en"] = parts[2];
                    if (parts.Length > 3) Content["ja"] = parts[3];
                }
            }
        }
    }
    // 礼物偏好
    public class GiftPreference
    {
        [JsonPropertyName("gift_id")] public int GiftId { get; set; }
        [JsonPropertyName("gift_name")] public LocalizedString GiftName { get; set; } = new();
        [JsonPropertyName("preference")] public GiftPreferenceType Preference { get; set; } // 喜欢/一般/厌恶
    }

    public class DataVersionInfo
    {
        [JsonPropertyName("appVersion")]
        public string AppVersion { get; set; } = "0.0.0";

        [JsonPropertyName("gameVersions")]
        public Dictionary<string, string> GameVersions { get; set; } = new();

        [JsonPropertyName("dataVersion")]
        public string DataVersion { get; set; } = "0.0.0.0.0";
    }

    public class StaticData
    {
        // 版本
        [JsonPropertyName("versionInfo")]
        public DataVersionInfo VersionInfo { get; set; } = new();
        // 舰船
        [JsonPropertyName("ships")]
        public List<ShipStatic> Ships { get; set; } = new();
    }
}