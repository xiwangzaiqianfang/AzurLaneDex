using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Models
{
    public static class ShipIdRanges
    {
        public const int NormalStart = 1;
        public const int NormalEnd = 9999;
        public const int MetaStart = 10001;
        public const int CollabStart = 20001;
        public const int ResearchStart = 30001;
    }

    public class ShipStatic
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new();
        [JsonPropertyName("alt_name")] public LocalizedString AltName { get; set; } = new();
        [JsonPropertyName("faction_id")] public int FactionId { get; set; }
        [JsonPropertyName("ship_class_id")] public int ShipClassId { get; set; }
        [JsonPropertyName("rarity_id")] public int RarityId { get; set; }
        [JsonPropertyName("game_order")] public int GameOrder { get; set; }
        [JsonPropertyName("category")] public ShipCategory Category { get; set; }
        [JsonPropertyName("category_order")] public int CategoryOrder { get; set; }

        [JsonPropertyName("acquire_entries")]
        public List<AcquireEntry> AcquireEntries { get; set; } = new();
        // 以下字段将在迁移后逐步废弃，保留用于降级兼容
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("acquire_main")]
        public LocalizedString? AcquireMainLegacy { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("acquire_detail")]
        public LocalizedString? AcquireDetailLegacy { get; set; }
        // 降级兼容字段结束
        [JsonPropertyName("build_time")] public string BuildTime { get; set; } = "";
        [JsonPropertyName("drop_locations")] public List<string> DropLocations { get; set; } = new();
        [JsonPropertyName("shop_exchange")] public string ShopExchange { get; set; } = "";
        [JsonPropertyName("is_permanent")] public bool IsPermanent { get; set; }
        [JsonPropertyName("debut_event")] public LocalizedString DebutEvent { get; set; } = new();
        [JsonPropertyName("release_date")] public string ReleaseDate { get; set; } = "";
        [JsonPropertyName("notes")] public LocalizedString Notes { get; set; } = new();
        [JsonPropertyName("can_remodel")] public bool CanRemodel { get; set; }
        [JsonPropertyName("remodel_date")] public string RemodelDate { get; set; } = "";
        [JsonPropertyName("can_special_gear")] public bool CanSpecialGear { get; set; }
        [JsonPropertyName("special_gear_entries")]
        public List<SpecialGearEntry> SpecialGearEntries { get; set; } = new();
        public class SpecialGearEntry
        {
            [JsonPropertyName("tag")]
            public string Tag { get; set; } = "";          // 例如 "gear_1", "gear_2"
            [JsonPropertyName("params")]
            public List<string> Parameters { get; set; } = new();
            [JsonPropertyName("custom_text")]
            public LocalizedString CustomText { get; set; } = new();
        }
        [JsonPropertyName("special_gear_name")]
        public LocalizedString SpecialGearName { get; set; } = new();
        [JsonPropertyName("special_gear_date")] public string SpecialGearDate { get; set; } = "";
        public LocalizedString SpecialGearAcquire { get; set; } = new LocalizedString(); [JsonPropertyName("obtain_bonus_attr_id")] public int ObtainBonusAttrId { get; set; }
        [JsonPropertyName("obtain_bonus_value")] public int ObtainBonusValue { get; set; }
        [JsonPropertyName("obtain_affect_class_ids")] public List<int> ObtainAffectClassIds { get; set; } = new();
        [JsonPropertyName("level120_bonus_attr_id")] public int Level120BonusAttrId { get; set; }
        [JsonPropertyName("level120_bonus_value")] public int Level120BonusValue { get; set; }
        [JsonPropertyName("level120_affect_class_ids")] public List<int> Level120AffectClassIds { get; set; } = new();
        [JsonPropertyName("tech_points_obtain")] public int TechPointsObtain { get; set; }
        [JsonPropertyName("tech_points_max")] public int TechPointsMax { get; set; }
        [JsonPropertyName("tech_points_120")] public int TechPoints120 { get; set; }
    }

    public class StaticData
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("ships")] public List<ShipStatic> Ships { get; set; } = new();
    }
}