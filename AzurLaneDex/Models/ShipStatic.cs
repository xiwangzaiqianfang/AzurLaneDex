using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Models
{
    /// <summary>
    /// 表示一艘舰船的静态数据（数据驱动模式，源自 AzurLaneData 的 ships.json 及关联文件）。
    /// 装载期由加载器将数据源（ships/skills/skins/ships_words/ship_drops/ship_retro_cost/
    /// wiki_cache 等）按 data_mapping.md 的规则转换为本模型；开发者新增舰船只需更新数据源，
    /// 无需修改 C# 代码。ShipId（= Id）为与 ShipState 关联的主键。
    /// </summary>
    public class ShipStatic
    {
        // ================= 标识与名称 =================

        /// <summary>数据源舰船 id（跨服稳定索引，即 ShipState.ShipId 主键）。</summary>
        [JsonPropertyName("id")] public int Id { get; set; }

        /// <summary>游戏组 id（同船族共享；皮肤编号前缀来源）。</summary>
        [JsonPropertyName("gid")] public int Gid { get; set; }

        /// <summary>图鉴顺序（装载期按 WikiNumber 生成，用于"图鉴"排序）。</summary>
        [JsonPropertyName("game_order")] public int GameOrder { get; set; }

        /// <summary>全服统一英文前缀名（如 "USS Enterprise"），用于头像/皮肤文件命名。</summary>
        [JsonPropertyName("global_name")] public string GlobalName { get; set; } = "";

        /// <summary>多语言名称（en 全量；zh-Hans 全量；zh-Hant 复制 zh-Hans；ja 部分）。</summary>
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new();

        /// <summary>社区别名列表（ships.json aliases + meta/ship_aliases）。</summary>
        [JsonPropertyName("aliases")] public List<string> Aliases { get; set; } = new();

        /// <summary>多语言舰级名称（数据源仅英文；其余语言留空走回退链）。</summary>
        [JsonPropertyName("class")] public LocalizedString Class { get; set; } = new();

        // ================= 分类 =================

        /// <summary>大分类（Normal/Collab/Research/META，装载期按 id 区间推导）。</summary>
        [JsonPropertyName("category")] public ShipCategory Category { get; set; }

        /// <summary>舰种（装载期由 hull 代码映射）。</summary>
        [JsonPropertyName("ship_type")] public ShipType Type { get; set; }

        /// <summary>数据库舰种 id（ship_types.id）：用于区分 DDG 先锋/主力、按舰种过滤装备等场合。</summary>
        [JsonPropertyName("ship_type_id")] public int TypeId { get; set; }

        /// <summary>稀有度（装载期由数据稀有度 2~6 映射）。</summary>
        [JsonPropertyName("rarity")] public Rarity Rarity { get; set; }

        /// <summary>阵营（装载期由 nation 代码映射）。</summary>
        [JsonPropertyName("faction")] public Faction Faction { get; set; }

        /// <summary>位掩码：Bulin=1 / Retro=2 / Research=4 / Fate=8 / Meta=16。</summary>
        [JsonPropertyName("flags")] public ShipFlags Flags { get; set; }

        /// <summary>是否为布里（由 Flags.Bulin 判定，替代旧硬编码中文名单）。</summary>
        [JsonIgnore]
        public bool IsBulin => Flags.HasFlag(ShipFlags.Bulin);

        /// <summary>是否有改造（Flags.Retro）。</summary>
        [JsonIgnore]
        public bool CanRetrofitFlag => Flags.HasFlag(ShipFlags.Retro);

        // ================= 日期与获取概况 =================

        /// <summary>（约）实装日期，文本 yyyy-MM-dd（数据源的毫秒时间戳在装载时转成文本）。</summary>
        [JsonPropertyName("date")] public string DateText { get; set; } = "";

        /// <summary>【旧字段，仅读取兼容】Unix 毫秒时间戳；装载时并入 DateText 后置 null。</summary>
        [JsonPropertyName("date_ms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? LegacyDateMs { get; set; }

        /// <summary>实装日期的 Unix 毫秒（由 <see cref="DateText"/> 现算；无法解析为 0）。</summary>
        [JsonIgnore] public long DateMs => ShipDate.ToUnixMs(DateText);

        /// <summary>是否常驻（装载期由 ship_drops 建造池/限时/非永久标志推导）。</summary>
        [JsonPropertyName("is_permanent")] public bool IsPermanent { get; set; }

        /// <summary>实装活动（多语言名称：en/zh-Hans/zh-Hant/ja；取自 DB `characters.release_event_id`）。</summary>
        [JsonPropertyName("release_event")] public LocalizedString ReleaseEvent { get; set; } = new();

        /// <summary>参与过的活动名（多语言；来自获取方式记录）。</summary>
        [JsonPropertyName("events")] public List<LocalizedString> Events { get; set; } = new();

        /// <summary>
        /// 【旧字段，仅读取兼容】实装活动中文名（wiki_cache.DebutEvent，仅部分船有）。
        /// 装载/编辑时并入 <see cref="Events"/>[0]["zh-Hans"]，随后置 null，因而不再写回文件。
        /// </summary>
        [JsonPropertyName("debut_event_zh")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? LegacyDebutEventZh { get; set; }

        /// <summary>声优（多语言；数据源暂无可靠内容，留空待外挂）。</summary>
        [JsonPropertyName("cv")] public LocalizedString CV { get; set; } = new();

        /// <summary>画师（数据源暂无可靠内容，留空待外挂）。</summary>
        [JsonPropertyName("artist")] public string Artist { get; set; } = "";

        /// <summary>退役奖励中文文本（wiki_cache.RetirementReward，仅部分船有）。</summary>
        [JsonPropertyName("retirement_reward")] public string RetirementReward { get; set; } = "";

        /// <summary>图鉴编号文本（wiki_cache.WikiNumber，如 "NO.077"/"NO.Collab021"）。</summary>
        [JsonPropertyName("wiki_number")] public string WikiNumber { get; set; } = "";

        /// <summary>
        /// 和谐名（DB `name_codes.code`，按中文原名反查；无对照时为空）。
        /// 文案里的 `{namecode:N}` 用的是**原名**，这里给对应的和谐名。
        /// </summary>
        [JsonPropertyName("harmony_name")] public string HarmonyName { get; set; } = "";

        // ================= 属性（按突破档位 + 成长系数） =================

        /// <summary>每档突破的属性（外层下标=突破档，长度与 LimitBreaks 对齐）。</summary>
        [JsonPropertyName("stats_levels")] public List<ShipStatsLevel> StatsLevels { get; set; } = new();

        // ================= 装备 =================

        /// <summary>每档突破的装备槽（外层下标=突破档，内层为该档各槽）。</summary>
        [JsonPropertyName("slot_levels")] public List<List<EquipmentSlotInfo>> SlotLevels { get; set; } = new();

        /// <summary>专属兵装引用（unique_aug → augments.json，装载期补充名称等）。</summary>
        [JsonPropertyName("unique_augment")] public UniqueAugmentInfo? UniqueAugment { get; set; }

        /// <summary>全部专属兵装（DB `spweapons`：一船可能有多件，第一件同时写入 <see cref="UniqueAugment"/>）。</summary>
        [JsonPropertyName("unique_augments")] public List<UniqueAugmentInfo> UniqueAugments { get; set; } = new();

        // ================= 技能（按突破档的 id 引用 + 展开详情） =================

        /// <summary>每档突破解锁的技能 id 列表（外层下标=突破档，引用 Skills 中的 Id）。</summary>
        [JsonPropertyName("skill_ids_by_lb")] public List<List<int>> SkillIdsByLb { get; set; } = new();

        /// <summary>本船全部技能详情（装载期由 skill_ids_by_lb 扁平去重后展开）。</summary>
        [JsonPropertyName("skills")] public List<Skill> Skills { get; set; } = new();

        // ================= 突破档元数据 =================

        /// <summary>每档突破信息（星级/等级范围/油耗区间），与各 Level 数组对齐。</summary>
        [JsonPropertyName("lb_data")] public List<LimitBreakInfo> LimitBreaks { get; set; } = new();

        // ================= 改造 =================

        /// <summary>改造数据（该船可改造时非空）。</summary>
        [JsonPropertyName("retrofit")] public RetrofitData? Retrofit { get; set; }

        // ================= 科研（PR/DR 船） =================

        /// <summary>科研数据（该船为科研船时非空；普通船为 null）。</summary>
        [JsonPropertyName("research")] public ResearchData? Research { get; set; }

        /// <summary>META 舰专属数据（仅 META 分类有值）。</summary>
        [JsonPropertyName("meta")] public MetaData? Meta { get; set; }

        // ================= 获取方式（ship_drops 聚合） =================

        /// <summary>获取方式数据（建造池/掉落/商店/事件）。</summary>
        [JsonPropertyName("acquisition")] public AcquisitionData Acquisition { get; set; } = new();

        // ================= 皮肤与台词 =================

        /// <summary>本船全部皮肤（ships.json skins 展开，含默认皮肤以 id=-1 表达）。</summary>
        [JsonPropertyName("skins")] public List<Skin> Skins { get; set; } = new();

        /// <summary>是否有誓约皮肤（ships.json oath_skin）。</summary>
        [JsonPropertyName("has_oath_skin")] public bool HasOathSkin { get; set; }

        /// <summary>按皮肤分组的台词（ships_words 聚合；默认皮 SkinId=-1）。</summary>
        [JsonPropertyName("words_by_skin")] public List<SkinVoiceGroup> WordsBySkin { get; set; } = new();

        // ================= 礼物与强化 =================

        /// <summary>讨厌的礼物物品 id 列表（→ items.json，type=50）。数据源无"喜欢"清单。</summary>
        [JsonPropertyName("gift_dislike_ids")] public List<int> GiftDislikeIds { get; set; } = new();

        /// <summary>讨厌的礼物名称（与 <see cref="GiftDislikeIds"/> 下标对齐；取不到名称为空串）。</summary>
        [JsonPropertyName("gift_dislike_names")] public List<string> GiftDislikeNames { get; set; } = new();

        /// <summary>讨厌的礼物图标相对路径（与 <see cref="GiftDislikeNames"/> 下标对齐；空=无图）。</summary>
        [JsonPropertyName("gift_dislike_icons")] public List<string> GiftDislikeIcons { get; set; } = new();

        /// <summary>讨厌的礼物品质（与 <see cref="GiftDislikeNames"/> 下标对齐；0=未知，用于图标底框）。</summary>
        [JsonPropertyName("gift_dislike_rarities")] public List<int> GiftDislikeRarities { get; set; } = new();

        /// <summary>作强化材料时的属性经验（fp/trp/avi/aa/rld）；null=不可用于强化。</summary>
        [JsonPropertyName("strengthen_exp")] public EnhanceExp? StrengthenExp { get; set; }

        /// <summary>强化数据（可强化上限 / 等级经验 / 素材经验）；null=数据库未提供。</summary>
        [JsonPropertyName("enhance")] public EnhanceData? Enhance { get; set; }

        /// <summary>退役收益（物资按舰种；勋章规则见 DB `medal_note`）。</summary>
        [JsonPropertyName("retire")] public RetireInfo? Retire { get; set; }

        // ================= 舰队科技 =================

        /// <summary>舰队科技（点数与加成、排序元数据）。</summary>
        [JsonPropertyName("fleet_tech")] public FleetTech? FleetTech { get; set; }

        // ================= 图鉴信息 =================

        /// <summary>图鉴六维评价（DB `character_performance`；null=数据库未提供）。</summary>
        [JsonPropertyName("performance")] public ShipPerformance? Performance { get; set; }

        /// <summary>保留装载时未识别字段，防止未来数据源扩展导致回写丢失。</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }

        // ---- 本地编辑备注（旧版编辑入口遗留字段；数据库装载不产生这些字段，仅供只读浏览展示） ----

        /// <summary>备注文本（旧版编辑器遗留字段，仅本地编辑文件使用）。</summary>
        [JsonPropertyName("remarks")] public string Remarks { get; set; } = "";

        /// <summary>注释文本（旧版编辑器遗留字段，仅本地编辑文件使用）。</summary>
        [JsonPropertyName("notes")] public string Notes { get; set; } = "";

        /// <summary>相关资料文本（旧版编辑器遗留字段，仅本地编辑文件使用）。</summary>
        [JsonPropertyName("reference_markdown")] public string ReferenceMarkdown { get; set; } = "";
    }

    // ================================================================
    // 属性
    // ================================================================

    /// <summary>单档属性快照：基础值 + 成长系数（scaling/strengthen 等）。</summary>
    public class ShipStatsLevel
    {
        [JsonPropertyName("hp")] public int Hp { get; set; }
        /// <summary>装甲（轻/中/重）。</summary>
        [JsonPropertyName("armor")] public ArmorType Armor { get; set; }
        [JsonPropertyName("fp")] public int Fp { get; set; }   // 炮击
        [JsonPropertyName("trp")] public int Trp { get; set; } // 雷击
        [JsonPropertyName("aa")] public int Aa { get; set; }   // 防空
        [JsonPropertyName("avi")] public int Avi { get; set; } // 航空
        [JsonPropertyName("rld")] public int Rld { get; set; } // 装填
        [JsonPropertyName("hit")] public int Hit { get; set; } // 命中
        [JsonPropertyName("eva")] public int Eva { get; set; } // 机动
        [JsonPropertyName("asw")] public int Asw { get; set; } // 反潜
        [JsonPropertyName("luck")] public int Luck { get; set; }
        [JsonPropertyName("spd")] public double Speed { get; set; } // 航速
        [JsonPropertyName("oxygen")] public int Oxygen { get; set; } // 氧气（潜艇）
        [JsonPropertyName("ammo")] public int Ammo { get; set; }     // 弹药（潜艇）

        /// <summary>随等级成长的系数（×1000 制，见数据源公式）。</summary>
        [JsonPropertyName("scaling")] public StatsGrowth Scaling { get; set; } = new();

        /// <summary>100 级后的额外成长系数。</summary>
        [JsonPropertyName("scaling_extra")] public StatsGrowth ScalingExtra { get; set; } = new();

        /// <summary>强化（或科研开发）可达的上限附加值。</summary>
        [JsonPropertyName("strengthen")] public StatsGrowth Strengthen { get; set; } = new();
    }

    /// <summary>成长/强化系数集合（与 ShipStatsLevel 的属性键一致）。</summary>
    public class StatsGrowth
    {
        [JsonPropertyName("hp")] public int Hp { get; set; }
        [JsonPropertyName("fp")] public int Fp { get; set; }
        [JsonPropertyName("trp")] public int Trp { get; set; }
        [JsonPropertyName("aa")] public int Aa { get; set; }
        [JsonPropertyName("avi")] public int Avi { get; set; }
        [JsonPropertyName("rld")] public int Rld { get; set; }
        [JsonPropertyName("hit")] public int Hit { get; set; }
        [JsonPropertyName("eva")] public int Eva { get; set; }
        [JsonPropertyName("asw")] public int Asw { get; set; }
        [JsonPropertyName("spd")] public int Speed { get; set; }
        [JsonPropertyName("luck")] public int Luck { get; set; }
    }

    // ================================================================
    // 突破档 / 装备槽
    // ================================================================

    /// <summary>单档突破信息（lb_data）。</summary>
    public class LimitBreakInfo
    {
        [JsonPropertyName("stars")] public int Stars { get; set; }
        [JsonPropertyName("star_max")] public int StarMax { get; set; }
        [JsonPropertyName("min_level")] public int MinLevel { get; set; }
        [JsonPropertyName("max_level")] public int MaxLevel { get; set; }
        /// <summary>该档突破所需油料区间起点。</summary>
        [JsonPropertyName("oil_start")] public int OilStart { get; set; }
        /// <summary>该档突破所需油料区间终点。</summary>
        [JsonPropertyName("oil_end")] public int OilEnd { get; set; }
    }

    /// <summary>单个装备槽位信息（ships.json slots 条目）。</summary>
    public class EquipmentSlotInfo
    {
        /// <summary>可装备的装备类型代码（EquipmentType 数值，展示名由 UI 映射表提供）。</summary>
        [JsonPropertyName("types")] public List<int> Types { get; set; } = new();
        /// <summary>武器效率（1.0=100%）。</summary>
        [JsonPropertyName("efficiency")] public double Efficiency { get; set; }
        /// <summary>底座数量。</summary>
        [JsonPropertyName("base")] public int Base { get; set; }
        /// <summary>预装填数。</summary>
        [JsonPropertyName("preload")] public int Preload { get; set; }
        /// <summary>并行装填数。</summary>
        [JsonPropertyName("parallel")] public int Parallel { get; set; }
        /// <summary>可存弹幕数（仅主炮/飞机槽；缺省=null）。</summary>
        [JsonPropertyName("capacity")] public int? Capacity { get; set; }
        /// <summary>空槽默认装备 id（-1=无，→ default_equips.json）。</summary>
        [JsonPropertyName("default_equip_id")] public int DefaultEquipId { get; set; } = -1;
    }

    /// <summary>专属兵装引用（DB `spweapons` 按 `unique_character_id` 关联）。</summary>
    public class UniqueAugmentInfo
    {
        /// <summary>兵装 id（= DB `game_id`，同时是图标文件名 `Assets/icon/item/spweapon/{id}.png`）。</summary>
        [JsonPropertyName("id")] public int Id { get; set; }
        /// <summary>名称（多语言；DB i18n `spweapon_{id}_name`）。</summary>
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new();
        /// <summary>描述（多语言；DB i18n `spweapon_{id}_desc`）。</summary>
        [JsonPropertyName("description")] public LocalizedString Description { get; set; } = new();
        /// <summary>稀有度。</summary>
        [JsonPropertyName("rarity")] public Rarity Rarity { get; set; }
        /// <summary>是否可研发合成（augments.json craftable）。</summary>
        [JsonPropertyName("craftable")] public bool Craftable { get; set; }

        /// <summary>适用范围文本（DB `spweapon_types.ship_types` 还原为舰种名）；空=DB 未提供。</summary>
        [JsonPropertyName("type_name")] public string TypeName { get; set; } = "";

        /// <summary>属性加成（DB `attribute_1/2` + `value_1/2`；随机上限见 RandomMax）。</summary>
        [JsonPropertyName("attributes")] public List<AugmentAttribute> Attributes { get; set; } = new();

        /// <summary>兵装效果（技能名/描述，DB `effect_skill_id` → skills）；空=DB 未提供。</summary>
        [JsonPropertyName("effect_skill_name")] public LocalizedString EffectSkillName { get; set; } = new();
        [JsonPropertyName("effect_skill_description")] public LocalizedString EffectSkillDescription { get; set; } = new();

        /// <summary>技能强化（技能名，DB `skill_upgrade_id` → skills）；空=DB 未提供。</summary>
        [JsonPropertyName("upgrade_skill_name")] public LocalizedString UpgradeSkillName { get; set; } = new();

        /// <summary>实装日期文本（本地编辑过渡字段；数据源装载时可留空）。</summary>
        [JsonPropertyName("release_date")] public string ReleaseDate { get; set; } = "";

        /// <summary>获取方式文本（本地编辑过渡字段；数据源装载时可留空）。</summary>
        [JsonPropertyName("acquisition")] public string AcquisitionMethod { get; set; } = "";
    }

    /// <summary>专属兵装的单条属性加成。</summary>
    public class AugmentAttribute
    {
        /// <summary>属性代码（DB `stat_ids.code`，如 cannon/hit/durability）。</summary>
        [JsonPropertyName("code")] public string Code { get; set; } = "";
        /// <summary>属性显示名（DB i18n `stat_{code}`；缺失时为代码本身）。</summary>
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("value")] public int Value { get; set; }
        /// <summary>随机加成上限（DB `value_*_random`；0=固定值）。</summary>
        [JsonPropertyName("random_max")] public int RandomMax { get; set; }

        /// <summary>展示文本：炮击+13（随机上限 10）。</summary>
        [JsonIgnore]
        public string Display => RandomMax > 0
            ? $"{Name}+{Value}（随机上限 {RandomMax}）"
            : $"{Name}+{Value}";
    }

    /// <summary>
    /// 图鉴六维评价（DB `character_performance`）：值 1~7 对应 F/E/D/C/B/A/S。
    /// </summary>
    public class ShipPerformance
    {
        [JsonPropertyName("durability")] public int Durability { get; set; }
        [JsonPropertyName("aa")] public int Aa { get; set; }
        [JsonPropertyName("evasion")] public int Evasion { get; set; }
        [JsonPropertyName("aviation")] public int Aviation { get; set; }
        [JsonPropertyName("torpedo")] public int Torpedo { get; set; }
        [JsonPropertyName("firepower")] public int Firepower { get; set; }

        /// <summary>1~7 → F/E/D/C/B/A/S（0=数据缺失）。</summary>
        public static string Grade(int value) => value switch
        {
            1 => "F", 2 => "E", 3 => "D", 4 => "C", 5 => "B", 6 => "A", 7 => "S",
            _ => "—"
        };

        [JsonIgnore] public string DurabilityGrade => Grade(Durability);
        [JsonIgnore] public string AaGrade => Grade(Aa);
        [JsonIgnore] public string EvasionGrade => Grade(Evasion);
        [JsonIgnore] public string AviationGrade => Grade(Aviation);
        [JsonIgnore] public string TorpedoGrade => Grade(Torpedo);
        [JsonIgnore] public string FirepowerGrade => Grade(Firepower);
    }

    // ================================================================
    // 技能
    // ================================================================

    /// <summary>技能（skills.json 展开）。描述中的 $n 占位符与 {新文本|说明} 勘误标记由渲染层解析。</summary>
    public class Skill
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        /// <summary>多语言名称（en 全量；zh-Hans 部分船有）。</summary>
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new();
        /// <summary>种类（0=装备技能 1=进攻 2=防御 3=支援）。</summary>
        [JsonPropertyName("kind")] public GameSkillKind Kind { get; set; }
        /// <summary>多语言描述。</summary>
        [JsonPropertyName("description")] public LocalizedString Description { get; set; } = new();
        /// <summary>最大等级（10 级技能 Values 每行 10 项）。</summary>
        [JsonPropertyName("max")] public int Max { get; set; }
        /// <summary>数值表：外层=描述中的占位符 $1..$n，内层=1~Max 级数值文本。</summary>
        [JsonPropertyName("values")] public List<List<string>> Values { get; set; } = new();

        /// <summary>
        /// 图标相对路径（相对 Assets，如 icon/skill/12010.png）。取值顺序：
        /// skills.game_id 对应图标 → 降级序号后的同名技能图标（II→I → 基础名）
        /// → 按技能类型的默认图（Attack_Default / Defence_Default / Help_Default，装备技能用 item/unkown.png）。
        /// </summary>
        [JsonPropertyName("icon_rel")] public string IconRel { get; set; } = "";

        /// <summary>取指定等级、指定占位符（1 起）的数值文本；越界返回空串。</summary>
        public string ValueAt(int placeholder, int level)
        {
            int column = placeholder - 1;
            if (column < 0 || column >= Values.Count) return "";
            var row = Values[column];
            int index = level - 1;
            return index >= 0 && index < row.Count ? row[index] : "";
        }
    }

    // ================================================================
    // 改造
    // ================================================================

    /// <summary>改造数据（ships.json retro + ship_retro_cost.json 聚合）。</summary>
    public class RetrofitData
    {
        /// <summary>是否可改造。</summary>
        [JsonPropertyName("can_retrofit")] public bool CanRetrofit { get; set; }
        /// <summary>改造实装日期，文本 yyyy-MM-dd。</summary>
        [JsonPropertyName("date")] public string DateText { get; set; } = "";
        /// <summary>【旧字段，仅读取兼容】Unix 毫秒；装载时并入 DateText 后置 null。</summary>
        [JsonPropertyName("date_ms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? LegacyDateMs { get; set; }
        /// <summary>改造实装日期的 Unix 毫秒（由 <see cref="DateText"/> 现算）。</summary>
        [JsonIgnore] public long DateMs => ShipDate.ToUnixMs(DateText);
        /// <summary>完成整船改造所需最低等级。</summary>
        [JsonPropertyName("min_level")] public int MinLevel { get; set; }
        /// <summary>完成整船改造所需最低突破档。</summary>
        [JsonPropertyName("min_lb")] public int MinLb { get; set; }
        /// <summary>整船改造完成的战力加成。</summary>
        [JsonPropertyName("power_bonus")] public int PowerBonus { get; set; }
        /// <summary>改造后舰种（缺省=不变）。</summary>
        [JsonPropertyName("target_ship_type")] public ShipType? TargetShipType { get; set; }
        /// <summary>改造树节点（字母 id，A 起）。</summary>
        [JsonPropertyName("nodes")] public List<RetrofitNode> Nodes { get; set; } = new();
        /// <summary>节点花费（键=节点字母，来自 ship_retro_cost.json）。</summary>
        [JsonPropertyName("cost_by_letter")] public Dictionary<string, RetrofitCost> CostByLetter { get; set; } = new();

        /// <summary>改造步骤号（DB `character_refit_step.step` 最大值；0=数据库未提供）。</summary>
        [JsonPropertyName("step_count")] public int StepCount { get; set; }
        /// <summary>改造获得技能名（DB `character_refit.refit_skill_id` → skills → i18n；空=无）。</summary>
        [JsonPropertyName("refit_skill_name")] public LocalizedString RefitSkillName { get; set; } = new();
    }

    /// <summary>改造树节点。字母为该节点唯一 id；前置关系由 Requires 字母列表表达。</summary>
    public class RetrofitNode
    {
        [JsonPropertyName("letter")] public string Letter { get; set; } = "";
        /// <summary>节点名（多语言；数据源英文）。</summary>
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new();
        /// <summary>前置节点字母列表。</summary>
        [JsonPropertyName("requires")] public List<string> Requires { get; set; } = new();
        [JsonPropertyName("min_level")] public int MinLevel { get; set; }
        [JsonPropertyName("min_lb")] public int MinLb { get; set; }
        /// <summary>属性加成文本（DB `transform_data_templates.effect` 还原，如「耐久+45」）。</summary>
        [JsonPropertyName("effect")] public string EffectText { get; set; } = "";
        /// <summary>改造习得技能名（DB `transform_data_templates.skill_id` → skills → i18n；空=无）。</summary>
        [JsonPropertyName("skill_name")] public LocalizedString SkillName { get; set; } = new();
        /// <summary>所属第几步（DB `character_refit_step.step`，1 起）。</summary>
        [JsonPropertyName("step")] public int Step { get; set; }
        /// <summary>步内序号（同一 step 下行序，1 起；用于「第 N 步」内的竖排顺序）。</summary>
        [JsonPropertyName("step_index")] public int IndexInStep { get; set; }
        /// <summary>该节点是否需要消耗舰船本体（DB `transform_data_templates.use_ship`）。</summary>
        [JsonPropertyName("requires_ship")] public bool RequiresShip { get; set; }
        /// <summary>该步消耗物资（DB `transform_data_templates.use_gold`）。</summary>
        [JsonPropertyName("gold")] public int Gold { get; set; }
        /// <summary>可重复购买时的轮次描述；外层=轮次，内层=该轮各描述文本。</summary>
        [JsonPropertyName("rounds")] public List<List<string>> Rounds { get; set; } = new();
        /// <summary>节点图标相对路径（`icon/refit/{code}.png`，code 取自 DB `transform_data_templates.icon`；空=无图）。</summary>
        [JsonPropertyName("icon")] public string Icon { get; set; } = "";
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        /// <summary>是否无限可重复节点。</summary>
        [JsonPropertyName("is_infinite")] public bool IsInfinite { get; set; }
    }

    /// <summary>单个改造节点的花费（每轮）。</summary>
    public class RetrofitCost
    {
        [JsonPropertyName("coins")] public int Coins { get; set; }
        /// <summary>舰船复制品/布里数量。</summary>
        [JsonPropertyName("copies")] public int Copies { get; set; }
        /// <summary>每轮物品清单（外层=轮次；物品 id → items.json）。</summary>
        [JsonPropertyName("items_by_round")] public List<List<ItemRef>> ItemsByRound { get; set; } = new();
    }

    /// <summary>物品引用（id → items.json，amount 数量）。</summary>
    public class ItemRef
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("amount")] public int Amount { get; set; }
        /// <summary>物品名（DB `items` 装载时填充；用于直接展示材料清单）。</summary>
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        /// <summary>图标相对路径（`icon/item/props|equips/{文件名}.png`，由 DB `items.icon_path` 解析；空=无图）。</summary>
        [JsonPropertyName("icon")] public string IconRel { get; set; } = "";
        /// <summary>品质（DB `items.rarity`，用于图标底框；0=未知）。</summary>
        [JsonPropertyName("rarity")] public int Rarity { get; set; }
    }

    // ================================================================
    // 科研（PR/DR 船）
    // ================================================================

    /// <summary>
    /// META 舰专属数据（DB meta_breakouts / meta_repairs / meta_skill_tasks）。
    /// </summary>
    public class MetaData
    {
    /// <summary>突破档位（DB `meta_breakouts`，按等级升序）。</summary>
    [JsonPropertyName("breakouts")] public List<MetaBreakout> Breakouts { get; set; } = new();
    /// <summary>强化材料（DB `meta_repairs`，全局材料表，按提供经验降序）。</summary>
    [JsonPropertyName("repair_items")] public List<MetaRepairItem> RepairItems { get; set; } = new();
    /// <summary>技能任务（DB `meta_skill_tasks`，按技能与等级升序）。</summary>
    [JsonPropertyName("skill_tasks")] public List<MetaSkillTaskRow> SkillTasks { get; set; } = new();
    }

    /// <summary>META 突破一档。</summary>
    public class MetaBreakout
    {
    [JsonPropertyName("level")] public int Level { get; set; }
    /// <summary>消耗材料文本（「结晶：猎人·META ×1」）。</summary>
    [JsonPropertyName("items_text")] public string ItemsText { get; set; } = "";
    [JsonPropertyName("gold")] public int Gold { get; set; }
    /// <summary>突破效果文案（DB `view_key` 三语）。</summary>
    [JsonPropertyName("effect_text")] public string EffectText { get; set; } = "";
    }

    /// <summary>META 强化材料。</summary>
    public class MetaRepairItem
    {
    [JsonPropertyName("item_name")] public string ItemName { get; set; } = "";
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("exp")] public int Exp { get; set; }
    /// <summary>提供的属性加成（DB `effect_attr` 解析为「航空 +2」）。</summary>
    [JsonPropertyName("attr_text")] public string AttrText { get; set; } = "";
    }

    /// <summary>META 技能任务一行。</summary>
    public class MetaSkillTaskRow
    {
    [JsonPropertyName("skill_name")] public string SkillName { get; set; } = "";
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("need_exp")] public int NeedExp { get; set; }
    [JsonPropertyName("unlock_count")] public int UnlockCount { get; set; }
    [JsonPropertyName("levelup_count")] public int LevelupCount { get; set; }
    }


    /// <summary>科研船数据：开发里程碑 + 天运拟似（与"科研项目" research_projects.json 无关）。</summary>
    public class ResearchData
    {
      /// <summary>开发里程碑（下标 0..3 对应 Dev0/10/20/30 的累计属性奖励；空对象=该档无奖励）。</summary>
        [JsonPropertyName("milestones")] public List<ResearchMilestone> Milestones { get; set; } = new();
        /// <summary>天运拟似（null=无天运，即非 FATE 船）。</summary>
        [JsonPropertyName("fate")] public FateSimulationData? Fate { get; set; }

        // ---- DB `ship_blueprints` 直接提供的字段（0/空=数据库未提供） ----

        /// <summary>解锁条件文本（DB `unlock_word`，已去除 &lt;color&gt; 标记）。</summary>
        [JsonPropertyName("unlock_condition")] public string UnlockCondition { get; set; } = "";
        /// <summary>开发/强化蓝图道具名（DB `strengthen_item` → `items.game_id`）。</summary>
        [JsonPropertyName("strengthen_item_name")] public string StrengthenItemName { get; set; } = "";
        /// <summary>解锁任务链条数（DB `unlock_task` 数组长度；任务名称数据库未提供）。</summary>
        [JsonPropertyName("unlock_task_count")] public int UnlockTaskCount { get; set; }
        /// <summary>开发效果条数（DB `strengthen_effect` 数组长度；效果名称数据库未提供）。</summary>
        [JsonPropertyName("strengthen_effect_count")] public int StrengthenEffectCount { get; set; }
        /// <summary>天运拟合条数（DB `fate_strengthen` 数组长度；名称数据库未提供）。</summary>
        [JsonPropertyName("fate_strengthen_count")] public int FateStrengthenCount { get; set; }
        /// <summary>蓝图单价（DB `price`；0=未提供）。</summary>
        [JsonPropertyName("price")] public int Price { get; set; }

        /// <summary>获得道具（DB `gain_item_id` → items 名称，逗号分隔）。</summary>
        [JsonPropertyName("gain_items_text")] public string GainItemsText { get; set; } = "";
        /// <summary>改造技能（DB `change_skill`，形如「旧技能 → 新技能」）。</summary>
        [JsonPropertyName("change_skill_text")] public string ChangeSkillText { get; set; } = "";
        /// <summary>解锁任务链各阶段解锁时间（DB `unlock_task` 秒数换算）。</summary>
        [JsonPropertyName("unlock_task_times")] public List<string> UnlockTaskTimes { get; set; } = new();
        /// <summary>开发效果条数说明（DB `strengthen_effect`，效果名称数据库未提供）。</summary>
        [JsonPropertyName("strengthen_effect_text")] public string StrengthenEffectText { get; set; } = "";
        /// <summary>天运拟合条数说明（DB `fate_strengthen`，名称数据库未提供）。</summary>
        [JsonPropertyName("fate_strengthen_text")] public string FateStrengthenText { get; set; } = "";

        // ---- 本地编辑过渡字段（旧版编辑器文本；数据源装载产生 Milestones 而非这些字段） ----

        /// <summary>前置阵营文本（分号分隔）。</summary>
        [JsonPropertyName("legacy_prereq_text")] public string LegacyPreReqText { get; set; } = "";
        /// <summary>任务列表文本（每行"名称|描述|要求"）。</summary>
        [JsonPropertyName("legacy_tasks_text")] public string LegacyTasksText { get; set; } = "";
        /// <summary>开发奖励文本（每行 key=value）。</summary>
        [JsonPropertyName("legacy_develop_text")] public string LegacyDevelopText { get; set; } = "";
        /// <summary>蓝图需求（本地编辑过渡）。</summary>
        [JsonPropertyName("legacy_blueprint_required")] public int BlueprintRequired { get; set; }
        /// <summary>开发蓝图需求（本地编辑过渡）。</summary>
        [JsonPropertyName("legacy_develop_blueprint_required")] public int DevelopBlueprintRequired { get; set; }
    }

    /// <summary>科研开发里程碑：该里程碑解锁时给的属性加成（键如 hp/fp/aa…，值为绝对量）。</summary>
    public class ResearchMilestone
    {
        /// <summary>该里程碑对应开发等级（10/20/30）。</summary>
        [JsonPropertyName("dev_level")] public int DevLevel { get; set; }
        [JsonPropertyName("bonus")] public Dictionary<string, int> Bonus { get; set; } = new();
    }

    /// <summary>天运拟似（5 级全量加成，非逐级）。</summary>
    public class FateSimulationData
    {
        [JsonPropertyName("level")] public int Level { get; set; }
        /// <summary>天运 5 级全部属性加成。</summary>
        [JsonPropertyName("stats")] public Dictionary<string, int> Stats { get; set; } = new();
        /// <summary>天运技能替换（replace=null 表示新增技能）。</summary>
        [JsonPropertyName("skills")] public List<SkillUpgrade> Skills { get; set; } = new();
        /// <summary>天运实装日期，文本 yyyy-MM-dd。</summary>
        [JsonPropertyName("date")] public string DateText { get; set; } = "";
        /// <summary>【旧字段，仅读取兼容】Unix 毫秒；装载时并入 DateText 后置 null。</summary>
        [JsonPropertyName("date_ms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? LegacyDateMs { get; set; }
        /// <summary>天运实装日期的 Unix 毫秒（由 <see cref="DateText"/> 现算）。</summary>
        [JsonIgnore] public long DateMs => ShipDate.ToUnixMs(DateText);

        // ---- 本地编辑过渡字段 ----
        /// <summary>描述文本（本地编辑过渡；数据源无此文本）。</summary>
        [JsonPropertyName("legacy_description")] public string Description { get; set; } = "";
        /// <summary>蓝图需求（本地编辑过渡）。</summary>
        [JsonPropertyName("legacy_blueprint_required")] public int BlueprintRequired { get; set; }
    }

    /// <summary>技能升级/替换描述（SkillUpgradeData）：把技能 replace（null=新增）换成 with。</summary>
    public class SkillUpgrade
    {
        [JsonPropertyName("replace")] public int? Replace { get; set; }
        [JsonPropertyName("with")] public int With { get; set; }
    }

    // ================================================================
    // 获取方式（ship_drops 聚合）
    // ================================================================

    /// <summary>舰船获取方式（ship_drops.json 展开 + 类别推导标记）。</summary>
    public class AcquisitionData
    {
        /// <summary>轻型池可建造。</summary>
        [JsonPropertyName("light")] public bool Light { get; set; }
        /// <summary>重型池可建造。</summary>
        [JsonPropertyName("heavy")] public bool Heavy { get; set; }
        /// <summary>特型池可建造。</summary>
        [JsonPropertyName("special")] public bool Special { get; set; }
        /// <summary>建造计时 "HH:mm:ss"；null=不可建造。</summary>
        [JsonPropertyName("build_time")] public string? BuildTime { get; set; }
        /// <summary>建造池文本（DB `characters.build_pool`，如「轻型池」「重型池、特型池」；空=数据库未提供）。</summary>
        [JsonPropertyName("build_pool")] public string BuildPool { get; set; } = "";
        /// <summary>限时建造所属活动名（多语言；null=非限时建造）。</summary>
        [JsonPropertyName("limited_event")] public LocalizedString? LimitedEvent { get; set; }
        /// <summary>true 时 light/heavy/special 需视为限时开放。</summary>
        [JsonPropertyName("non_permanent")] public bool NonPermanent { get; set; }
        /// <summary>其它常驻获取途径（UnlockType 商店/任务码）。</summary>
        [JsonPropertyName("unlock_types")] public List<UnlockType> UnlockTypes { get; set; } = new();
        /// <summary>地图掉落（仅含非空章节）。</summary>
        [JsonPropertyName("map_drops")] public List<ChapterDrop> MapDrops { get; set; } = new();
        /// <summary>作战档案掉落（62 船）。</summary>
        [JsonPropertyName("archive_drops")] public List<WarArchiveDrop> ArchiveDrops { get; set; } = new();
        /// <summary>参与过的活动名（多语言；来自获取方式记录）。</summary>
        [JsonPropertyName("events")] public List<LocalizedString> Events { get; set; } = new();
        /// <summary>获取备注（多语言；旧 notes 英文 + notes_zh 中文在装载时合并到此）。</summary>
        [JsonPropertyName("notes")] public List<LocalizedString> Notes { get; set; } = new();
        /// <summary>图鉴内展示的获取备注（DB `character_acquire.in_codex = 1`，基本信息「备注」行用）。</summary>
        [JsonPropertyName("codex_notes")] public List<LocalizedString> CodexNotes { get; set; } = new();
        /// <summary>
        /// 【旧字段，仅读取兼容】中文备注（wiki_cache.OtherAcquire/DropLocations 等）。
        /// 装载/编辑时并入 <see cref="Notes"/> 的 "zh-Hans"，随后置 null，因而不再写回文件。
        /// </summary>
            /// <summary>主线打捞行（已按章节压缩：全 4 关 → 「第 N 章全关卡」）。</summary>
            [JsonPropertyName("drop_chapter_lines")] public List<string> DropChapterLines { get; set; } = new();
            /// <summary>活动打捞行（普通关卡全中 → 「{活动名}全关卡」；SP/EX 单独标注）。</summary>
            [JsonPropertyName("drop_event_lines")] public List<string> DropEventLines { get; set; } = new();
            /// <summary>档案打捞关卡数（有主线打捞时折叠展示）。</summary>
            [JsonPropertyName("drop_archive_count")] public int DropArchiveCount { get; set; }
            /// <summary>是否有主线打捞（决定档案/活动是否折叠）。</summary>
            [JsonPropertyName("has_main_drops")] public bool HasMainDrops { get; set; }
            /// <summary>建造补充：常驻池归属。</summary>
            [JsonPropertyName("build_pools")] public List<string> BuildPools { get; set; } = new();
            /// <summary>保底兑换说明。</summary>
            [JsonPropertyName("exchange_text")] public string ExchangeText { get; set; } = "";
            /// <summary>限时建造与 UP（仅无常驻池时生效）。</summary>
            [JsonPropertyName("limited_text")] public string LimitedText { get; set; } = "";
            /// <summary>商店兑换渠道名。</summary>
            [JsonPropertyName("shop_names")] public List<string> ShopNames { get; set; } = new();
            /// <summary>邀请函兑换道具名（完整列出）。</summary>
            [JsonPropertyName("invitation_items")] public List<string> InvitationItems { get; set; } = new();
            /// <summary>任务/活动奖励说明。</summary>
            [JsonPropertyName("mission_texts")] public List<string> MissionTexts { get; set; } = new();
            /// <summary>是否有祈愿建造（DB `acquire_types.code = pray_build`）。</summary>
            [JsonPropertyName("pray_build")] public bool PrayBuild { get; set; }
            /// <summary>祈愿池名（DB pray_build 的 `cost_key.pool`，如「轻型祈愿池」）。</summary>
            [JsonPropertyName("pray_pools")] public List<string> PrayPools { get; set; } = new();
            /// <summary>逐条打捞点（DB `map_drops` + `maps`；有它时优先于压缩行）。</summary>
            [JsonPropertyName("drop_points")] public List<DropPointInfo> DropPoints { get; set; } = new();

        [JsonPropertyName("notes_zh")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? LegacyNotesZh { get; set; }
    }

    /// <summary>某一章的掉落（章节号 + 掉落点列表）。</summary>
    public class ChapterDrop
    {
        /// <summary>章节号（1 起，数据源当前到 15/16）。</summary>
        [JsonPropertyName("chapter")] public int Chapter { get; set; }
        [JsonPropertyName("points")] public List<MapDropPoint> Points { get; set; } = new();
    }

    /// <summary>地图掉落点。</summary>
    public class MapDropPoint
    {
        /// <summary>章节内子图号（1~4）。</summary>
        [JsonPropertyName("map")] public int Map { get; set; }
        /// <summary>true=仅旗舰(Boss)点，false=任意节点。</summary>
        [JsonPropertyName("is_boss")] public bool IsBoss { get; set; }
    }

    /// <summary>作战档案掉落。</summary>
    public class WarArchiveDrop
    {
        /// <summary>档案活动名（多语言；对应 events 列表项）。</summary>
        [JsonPropertyName("event")] public LocalizedString Event { get; set; } = new();
        /// <summary>是否吃 60 次出击保底。</summary>
        [JsonPropertyName("has_pity")] public bool HasPity { get; set; }
        /// <summary>掉落点（图标识如 "B1"，含是否 Boss 点）。</summary>
        [JsonPropertyName("maps")] public List<ArchiveMapPoint> Maps { get; set; } = new();
    }

    /// <summary>档案图内掉落点。</summary>
    public class ArchiveMapPoint
    {
        [JsonPropertyName("map")] public string Map { get; set; } = "";
        [JsonPropertyName("is_boss")] public bool IsBoss { get; set; }
    }

    /// <summary>一条打捞点（DB `map_drops` 一行 = 一张图，来源与难度由 `source_type` / 图名解析）。</summary>
    public class DropPointInfo
    {
        /// <summary>来源：主线 / 活动 / 档案 / 彩蛋。</summary>
        [JsonPropertyName("source")] public string Source { get; set; } = "";
        /// <summary>关卡显示名（主线为「7-2」，活动/档案取 i18n 图名）。</summary>
        [JsonPropertyName("map")] public string Map { get; set; } = "";
        /// <summary>难度（普通 / 困难；无区分时为空）。</summary>
        [JsonPropertyName("difficulty")] public string Difficulty { get; set; } = "";
        [JsonPropertyName("is_boss")] public bool IsBoss { get; set; }
    }

    // ================================================================
    // 皮肤 / 台词
    // ================================================================

    /// <summary>皮肤（skins.json 展开；SkinId=-1 表示默认皮——默认皮仅存在于台词侧）。</summary>
    public class Skin
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        /// <summary>名称（数据源英文；中文待外挂）。</summary>
        [JsonPropertyName("name")] public LocalizedString Name { get; set; } = new();
        /// <summary>种类（Normal/Oath/Retrofit/Promo/…，-1=默认）。</summary>
        [JsonPropertyName("kind")] public GameSkinKind Kind { get; set; }
        /// <summary>主题分类码（1=圣诞 2=新年 … 9997=改造 9998=誓约 9999=其他）。</summary>
        [JsonPropertyName("category")] public int Category { get; set; }
        /// <summary>获取方式。</summary>
        [JsonPropertyName("obtain_type")] public SkinObtainType ObtainType { get; set; }
        /// <summary>价格（货币=Currency 物品 id）。</summary>
        [JsonPropertyName("price")] public int Price { get; set; }
        [JsonPropertyName("currency")] public int Currency { get; set; }
        [JsonPropertyName("is_permanent")] public bool IsPermanent { get; set; }
        [JsonPropertyName("icon")] public string Icon { get; set; } = "";
        // ---- 能力位 ----
        [JsonPropertyName("l2d")] public bool L2d { get; set; }
        [JsonPropertyName("dynamic")] public bool Dynamic { get; set; }
        [JsonPropertyName("asmr")] public bool Asmr { get; set; }
        [JsonPropertyName("custom_bgm")] public bool CustomBgm { get; set; }
        [JsonPropertyName("dynamic_bg")] public bool DynamicBg { get; set; }
        [JsonPropertyName("dual_form")] public bool DualForm { get; set; }
        [JsonPropertyName("l2d_plus")] public bool L2dPlus { get; set; }
        /// <summary>上架服务器标记（EN/JP/CN）。</summary>
        [JsonPropertyName("servers")] public List<string> Servers { get; set; } = new();

        /// <summary>是否为誓约皮。</summary>
        [JsonIgnore] public bool IsOath => Kind == GameSkinKind.Oath;
        /// <summary>是否为改造皮。</summary>
        [JsonIgnore] public bool IsRetrofit => Kind == GameSkinKind.Retrofit;
    }

    /// <summary>某一皮肤 id 下的台词组（ships_words 聚合）。</summary>
    public class SkinVoiceGroup
    {
        /// <summary>皮肤 id（-1=默认皮）。</summary>
        [JsonPropertyName("skin_id")] public int SkinId { get; set; } = -1;
        /// <summary>基础台词。</summary>
        [JsonPropertyName("lines")] public List<VoiceLine> Lines { get; set; } = new();
        /// <summary>条件附加台词（ex；同 Key 时覆盖基础台词）。</summary>
        [JsonPropertyName("extra")] public List<VoiceLine> Extra { get; set; } = new();
    }

    /// <summary>单条台词。</summary>
    public class VoiceLine
    {
        /// <summary>台词类型键（自由字符串：Login/Idle 1/Touch/…，显示文案由 UI 资源翻译）。</summary>
        [JsonPropertyName("key")] public string Key { get; set; } = "";
        /// <summary>场景代码（DB `line_scenes.code`，如 acquire / home / touch）。</summary>
        [JsonPropertyName("scene")] public string Scene { get; set; } = "";
        /// <summary>场景排序键（DB `line_scenes.sort_order`；越小越前，用于分组显示顺序）。</summary>
        [JsonPropertyName("scene_order")] public int SceneOrder { get; set; }
        /// <summary>场景显示名（DB `line_scenes.name_key` → i18n；库内文案缺失时为空，由 UI 兜底）。</summary>
        [JsonPropertyName("scene_name")] public LocalizedString SceneName { get; set; } = new();
        /// <summary>内容（数据源英文；个别皮肤台词为中文）。</summary>
        [JsonPropertyName("content")] public LocalizedString Content { get; set; } = new();
        /// <summary>触发条件（null=无条件）。</summary>
        [JsonPropertyName("conditions")] public VoiceLineConditions? Conditions { get; set; }
    }

    /// <summary>台词触发条件（全部可空）。</summary>
    public class VoiceLineConditions
    {
        /// <summary>需誓约。</summary>
        [JsonPropertyName("oath")] public bool? Oath { get; set; }
        /// <summary>好感度阈值。</summary>
        [JsonPropertyName("affinity")] public int? Affinity { get; set; }
        /// <summary>命中数量（与 Ships/Hulls/Nations/Rarities 配合，本船不计）。</summary>
        [JsonPropertyName("amount")] public int? Amount { get; set; }
        /// <summary>需要的船 id 列表。</summary>
        [JsonPropertyName("ships")] public List<int>? Ships { get; set; }
        /// <summary>当前无法触发。</summary>
        [JsonPropertyName("impossible")] public bool? Impossible { get; set; }
    }

    // ================================================================
    // 强化经验 / 舰队科技
    // ================================================================

    /// <summary>作强化材料时提供的属性经验（strengthen_exp）。</summary>
    public class EnhanceExp
    {
        [JsonPropertyName("fp")] public int Fp { get; set; }
        [JsonPropertyName("trp")] public int Trp { get; set; }
        [JsonPropertyName("avi")] public int Avi { get; set; }
        [JsonPropertyName("aa")] public int Aa { get; set; }
        [JsonPropertyName("rld")] public int Rld { get; set; }
    }

    /// <summary>强化数据（DB `character_enhance`）。</summary>
    public class EnhanceData
    {
        /// <summary>各项可强化上限（强化到 +N 为止）。</summary>
        [JsonPropertyName("cap_fp")] public int CapFp { get; set; }
        [JsonPropertyName("cap_trp")] public int CapTrp { get; set; }
        [JsonPropertyName("cap_avi")] public int CapAvi { get; set; }
        [JsonPropertyName("cap_rld")] public int CapRld { get; set; }
        [JsonPropertyName("cap_dur")] public int CapDur { get; set; }
        /// <summary>每级所需经验（DB `level_exp`，5 元素；空=数据库未提供）。</summary>
        [JsonPropertyName("level_exp")] public List<int> LevelExp { get; set; } = new();
        /// <summary>作为素材给予的经验（DB `attr_exp`，5 元素）。</summary>
        [JsonPropertyName("attr_exp")] public List<int> AttrExp { get; set; } = new();

        /// <summary>强化价值：作为素材提供的属性经验合计。</summary>
        [JsonIgnore] public int Value => AttrExp.Sum();
    }

    /// <summary>退役收益（DB `character_retire` 按舰种给物资；勋章按稀有度给数值）。</summary>
    public class RetireInfo
    {
        /// <summary>退役获得物资（按舰种）。</summary>
        [JsonPropertyName("coin")] public int Coin { get; set; }
        /// <summary>各稀有度勋章数量（DB `medal_n/r/sr/ssr/ur`）。</summary>
        [JsonPropertyName("medal_n")] public int MedalN { get; set; }
        [JsonPropertyName("medal_r")] public int MedalR { get; set; }
        [JsonPropertyName("medal_sr")] public int MedalSr { get; set; }
        [JsonPropertyName("medal_ssr")] public int MedalSsr { get; set; }
        [JsonPropertyName("medal_ur")] public int MedalUr { get; set; }
        /// <summary>UR 额外道具（DB `ur_extra_item_id` → items.game_id）与数量。</summary>
        [JsonPropertyName("ur_extra_item_id")] public int UrExtraItemId { get; set; }
        [JsonPropertyName("ur_extra_amount")] public int UrExtraAmount { get; set; }
        /// <summary>UR 额外道具名（装载期解析）。</summary>
        [JsonPropertyName("ur_extra_item_name")] public string UrExtraItemName { get; set; } = "";
        /// <summary>数据库给的勋章规则原文（自由文本，现已有数值列，仅保留备查）。</summary>
        [JsonPropertyName("note")] public string Note { get; set; } = "";
    }

    /// <summary>舰队科技（收集/120 级/满破 三项任务点数与加成 + 排序元数据）。</summary>
    public class FleetTech
    {
        [JsonPropertyName("collect")] public TechTaskBonus Collect { get; set; } = new();
        [JsonPropertyName("level120")] public TechTaskBonus Level120 { get; set; } = new();
        [JsonPropertyName("limit_break")] public TechTaskBonus LimitBreak { get; set; } = new();
        /// <summary>科技菜单排序类（→ fleet_tech_sort.json 键）。</summary>
        [JsonPropertyName("sort_class")] public int SortClass { get; set; }
        /// <summary>类内序号。</summary>
        [JsonPropertyName("sort_index")] public int SortIndex { get; set; }
    }

    /// <summary>一项科技任务的点数与加成。</summary>
    public class TechTaskBonus
    {
        [JsonPropertyName("pts")] public int Pts { get; set; }
        /// <summary>加成属性键（hp/fp/trp/avi/aa/rld/hit/eva/asw）。</summary>
        [JsonPropertyName("stat")] public string Stat { get; set; } = "";
        [JsonPropertyName("value")] public int Value { get; set; }
        /// <summary>加成目标舰种（hull 代码，展示时经映射表转 ShipType）。</summary>
        [JsonPropertyName("hulls")] public List<int> Hulls { get; set; } = new();
    }

    // ================================================================
    // 本地静态容器（第四阶段数据源化后将被装载器容器取代，暂保留）
    // ================================================================

    /// <summary>本地静态数据文件容器（旧格式壳，待第四阶段替换）。</summary>
    public class StaticData
    {
        [JsonPropertyName("versionInfo")] public DataVersionInfo VersionInfo { get; set; } = new();
        [JsonPropertyName("ships")] public List<ShipStatic> Ships { get; set; } = new();
    }

    /// <summary>数据版本信息。</summary>
    public class DataVersionInfo
    {
        [JsonPropertyName("appVersion")] public string AppVersion { get; set; } = "0.0.0";
        [JsonPropertyName("gameVersions")] public Dictionary<string, string> GameVersions { get; set; } = new();
        [JsonPropertyName("dataVersion")] public string DataVersion { get; set; } = "0.0.0.0.0";
    }
}
