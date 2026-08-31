using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Models
{
    /// <summary>
    /// 表示一艘舰船的用户进度数据（动态数据）
    /// </summary>
    public class ShipState
    {
        [JsonPropertyName("ship_id")] public int ShipId { get; set; }

        // 基础状态
        [JsonPropertyName("owned")] public bool Owned { get; set; }
        [JsonPropertyName("breakthrough")] public int Breakthrough { get; set; } // 0 ~ 3

        // 技能等级（技能ID -> 等级，范围1~10）
        [JsonPropertyName("skill_levels")] public Dictionary<int, int> SkillLevels { get; set; } = new();

        // 强化完成状态（炮击、雷击、航空、装填）
        [JsonPropertyName("enhance_completed")] public EnhanceCompleted EnhanceCompleted { get; set; } = new();

        // 好感度与等级
        [JsonPropertyName("affection_max")] public bool AffectionMax { get; set; } // 好感度是否已满
        [JsonPropertyName("oath")] public bool Oath { get; set; }
        [JsonPropertyName("level_120")] public bool Level120 { get; set; }
        [JsonPropertyName("level_125")] public bool Level125 { get; set; }

        // 改造
        [JsonPropertyName("retrofitted")] public bool Retrofitted { get; set; } // 是否已完成全部改造
        [JsonPropertyName("retrofit_nodes")] public Dictionary<string, bool> RetrofitNodes { get; set; } = new(); // 节点字母 -> 是否完成

        // 专属兵装
        [JsonPropertyName("special_gear_obtained")] public bool SpecialGearObtained { get; set; }

        // 科研
        [JsonPropertyName("research_level")] public int ResearchLevel { get; set; } // 开发等级（1~30）
        [JsonPropertyName("fate_level")] public int FateLevel { get; set; } // 天运拟合等级（1~5）

        // 皮肤拥有
        [JsonPropertyName("owned_skins")] public List<int> OwnedSkins { get; set; } = new(); // 皮肤ID列表
    }

    /// <summary>
    /// 强化完成状态（四个属性）
    /// </summary>
    public class EnhanceCompleted
    {
        [JsonPropertyName("fp")] public bool Fp { get; set; }   // 炮击
        [JsonPropertyName("trp")] public bool Trp { get; set; } // 雷击
        [JsonPropertyName("avi")] public bool Avi { get; set; } // 航空
        [JsonPropertyName("rld")] public bool Rld { get; set; } // 装填
    }

    /// <summary>
    /// 用户所有舰船动态数据的容器（用于序列化/反序列化）
    /// </summary>
    public class UserShipStates
    {
        [JsonPropertyName("account")] public string AccountName { get; set; } = "";
        [JsonPropertyName("ships")] public List<ShipState> Ships { get; set; } = new();
    }

    // 舰船状态列表容器（用于序列化/反序列化）
    public class StateList
    {
        [JsonPropertyName("states")]
        public List<ShipState> States { get; set; } = new();
    }
}