using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Models
{
    public class OldShipStatic
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("alt_name")] public string AltName { get; set; } = "";
        [JsonPropertyName("faction")] public string Faction { get; set; } = "";
        [JsonPropertyName("ship_class")] public string ShipClass { get; set; } = "";
        [JsonPropertyName("rarity")] public string Rarity { get; set; } = "";
        [JsonPropertyName("game_order")] public int GameOrder { get; set; }
        [JsonPropertyName("category")] public ShipCategory Category { get; set; }
        [JsonPropertyName("category_order")] public int CategoryOrder { get; set; }
        [JsonPropertyName("acquire_main")] public string AcquireMain { get; set; } = "";
        [JsonPropertyName("acquire_detail")] public string AcquireDetail { get; set; } = "";
        [JsonPropertyName("build_time")] public string BuildTime { get; set; } = "";
        [JsonPropertyName("drop_locations")] public List<string> DropLocations { get; set; } = new();
        [JsonPropertyName("shop_exchange")] public string ShopExchange { get; set; } = "";
        [JsonPropertyName("is_permanent")] public bool IsPermanent { get; set; }
        [JsonPropertyName("debut_event")] public string DebutEvent { get; set; } = "";
        [JsonPropertyName("release_date")] public string ReleaseDate { get; set; } = "";
        [JsonPropertyName("notes")] public string Notes { get; set; } = "";
        [JsonPropertyName("can_remodel")] public bool CanRemodel { get; set; }
        [JsonPropertyName("remodel_date")] public string RemodelDate { get; set; } = "";
        [JsonPropertyName("can_special_gear")] public bool CanSpecialGear { get; set; }
        [JsonPropertyName("special_gear_name")] public string SpecialGearName { get; set; } = "";
        [JsonPropertyName("special_gear_date")] public string SpecialGearDate { get; set; } = "";
        [JsonPropertyName("special_gear_acquire")] public string SpecialGearAcquire { get; set; } = "";
        [JsonPropertyName("obtain_bonus_attr")] public string ObtainBonusAttr { get; set; } = "";
        [JsonPropertyName("obtain_bonus_value")] public int ObtainBonusValue { get; set; }
        [JsonPropertyName("obtain_affects")] public List<string> ObtainAffects { get; set; } = new();
        [JsonPropertyName("level120_bonus_attr")] public string Level120BonusAttr { get; set; } = "";
        [JsonPropertyName("level120_bonus_value")] public int Level120BonusValue { get; set; }
        [JsonPropertyName("level120_affects")] public List<string> Level120Affects { get; set; } = new();
        [JsonPropertyName("tech_points_obtain")] public int TechPointsObtain { get; set; }
        [JsonPropertyName("tech_points_max")] public int TechPointsMax { get; set; }
        [JsonPropertyName("tech_points_120")] public int TechPoints120 { get; set; }
    }

    public class OldStaticData
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("ships")] public List<OldShipStatic> Ships { get; set; } = new();
    }
}