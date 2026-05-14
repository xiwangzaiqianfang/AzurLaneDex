using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Models;

public class ShipStatic
{
    public static class ShipIdRanges
    {
        public const int NormalStart = 1;
        public const int NormalEnd = 9999;
        public const int MetaStart = 10001;
        public const int CollabStart = 20001;
        public const int ResearchStart = 30001;
    }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("alt_name")]
    public string AltName { get; set; } = "";

    [JsonPropertyName("faction")]
    public string Faction { get; set; } = "";

    [JsonPropertyName("ship_class")]
    public string ShipClass { get; set; } = "";

    [JsonPropertyName("rarity")]
    public string Rarity { get; set; } = "";

    [JsonPropertyName("game_order")]
    public int GameOrder { get; set; }
    public enum ShipCategory
    {
        Normal = 0,
        Collab = 1,
        Research = 2,
        META = 3
    }

    [JsonPropertyName("category")]
    public ShipCategory Category { get; set; } = ShipCategory.Normal;

    [JsonPropertyName("category_order")]
    public int CategoryOrder { get; set; }

    // 获取方式
    [JsonPropertyName("acquire_main")]
    public string AcquireMain { get; set; } = "";

    [JsonPropertyName("acquire_detail")]
    public string AcquireDetail { get; set; } = "";

    [JsonPropertyName("build_time")]
    public string BuildTime { get; set; } = "";

    [JsonPropertyName("drop_locations")]
    public List<string> DropLocations { get; set; } = new();

    [JsonPropertyName("shop_exchange")]
    public string ShopExchange { get; set; } = "";

    [JsonPropertyName("is_permanent")]
    public bool IsPermanent { get; set; }

    // 实装活动
    [JsonPropertyName("debut_event")]
    public string DebutEvent { get; set; } = "";

    [JsonPropertyName("release_date")]
    public string ReleaseDate { get; set; } = "";

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = "";

    // 改造
    [JsonPropertyName("can_remodel")]
    public bool CanRemodel { get; set; }

    [JsonPropertyName("remodel_date")]
    public string RemodelDate { get; set; } = "";

    // 特殊兵装
    [JsonPropertyName("can_special_gear")]
    public bool CanSpecialGear { get; set; }

    [JsonPropertyName("special_gear_name")]
    public string SpecialGearName { get; set; } = "";

    [JsonPropertyName("special_gear_date")]
    public string SpecialGearDate { get; set; } = "";

    [JsonPropertyName("special_gear_acquire")]
    public string SpecialGearAcquire { get; set; } = "";

    [JsonPropertyName("image_path")]
    public string ImagePath { get; set; } = "";

    // 属性加成（新格式）
    [JsonPropertyName("obtain_bonus_attr")]
    public string ObtainBonusAttr { get; set; } = "";

    [JsonPropertyName("obtain_bonus_value")]
    public int ObtainBonusValue { get; set; }

    [JsonPropertyName("obtain_affects")]
    public List<string> ObtainAffects { get; set; } = new();

    [JsonPropertyName("level120_bonus_attr")]
    public string Level120BonusAttr { get; set; } = "";

    [JsonPropertyName("level120_bonus_value")]
    public int Level120BonusValue { get; set; }

    [JsonPropertyName("level120_affects")]
    public List<string> Level120Affects { get; set; } = new();

    // 科技点
    [JsonPropertyName("tech_points_obtain")]
    public int TechPointsObtain { get; set; }

    [JsonPropertyName("tech_points_max")]
    public int TechPointsMax { get; set; }

    [JsonPropertyName("tech_points_120")]
    public int TechPoints120 { get; set; }
}