using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// SQLite 静态数据源装载器（ships_static.db）：把多张表聚合回项目模型 <see cref="ShipStatic"/>。
    /// 约定见 docs/数据库使用说明.md：
    ///  - 所有可翻译文本在 i18n（zh/en/jp）→ 映射为 LocalizedString（zh→zh-Hans、jp→ja；繁中复制简中）
    ///  - 日期已是 yyyy-MM-dd 文本
    ///  - 展示名一律走 i18n[key][lang]，不使用 global_name
    ///  - DB 中没有对应数据的外观字段（专属兵装、科研里程碑/天运、礼物偏好、强化等级经验、科技排序等）留空
    /// </summary>
    public static class SqliteDataSourceLoader
    {
        public const string DbFileName = "ships_static.db";

        /// <summary>数据库候选路径：数据目录优先（可被更新覆盖），其次包内 Assets。</summary>
        public static string ResolveDbPath()
        {
            string dataPath = Path.Combine(App.DataRoot, DbFileName);
            if (File.Exists(dataPath)) return dataPath;
            string packaged = Path.Combine(AppContext.BaseDirectory, "Assets", DbFileName);
            if (File.Exists(packaged)) return packaged;
            return dataPath;
        }

        public static async Task<DataSourceLoader.LoadResult> LoadAsync(string dbPath)
        {
            var result = new DataSourceLoader.LoadResult();
            if (!File.Exists(dbPath))
            {
                result.SourceAvailable = false;
                result.Warnings.Add($"数据库不存在：{dbPath}");
                return result;
            }

            try
            {
                var warnings = new List<string>();
                var ships = await Task.Run(() => Load(dbPath, warnings));
                result.Ships = ships;
                result.SourceAvailable = ships.Count > 0;
                if (result.SourceAvailable)
                {
                    result.DataVersion = "sqlite-" + File.GetLastWriteTime(dbPath)
                        .ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
                    result.Warnings.Add($"SQLite 数据源装载 {ships.Count} 艘");
                }
                result.Warnings.AddRange(warnings);
                return result;
            }
            catch (Exception ex)
            {
                result.SourceAvailable = false;
                result.Warnings.Add($"SQLite 装载失败：{ex.Message}");
                LogService.Error("SQLite 数据源装载失败", nameof(SqliteDataSourceLoader), ex);
                return result;
            }
        }

        // ================= 库结构探测（兼容旧版数据库） =================

        /// <summary>
        /// 只读探测表/列是否存在。旧版 ships_static.db（如数据目录里升级前的副本）缺少新列或整张新表，
        /// 装载器据此降级查询，避免 "no such column / no such table" 直接抛异常。
        /// </summary>
        private sealed class DbSchema
        {
            private readonly HashSet<string> _tables = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, HashSet<string>> _columns = new(StringComparer.OrdinalIgnoreCase);

            internal DbSchema(SqliteDatabase db)
            {
                using (var q = db.Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"))
                {
                    while (q.Read())
                    {
                        string? name = q.Text(0);
                        if (!string.IsNullOrEmpty(name)) _tables.Add(name);
                    }
                }

                foreach (string table in _tables)
                {
                    var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using var q = db.Query($"PRAGMA table_info(\"{table.Replace("\"", "\"\"")}\")");
                    while (q.Read())
                    {
                        string? col = q.Text(1);
                        if (!string.IsNullOrEmpty(col)) cols.Add(col);
                    }
                    _columns[table] = cols;
                }
            }

            internal bool HasTable(string table) => _tables.Contains(table);

            internal bool HasColumn(string table, string column)
                => _columns.TryGetValue(table, out var cols) && cols.Contains(column);

            /// <summary>SELECT 列表达式：有该列则用列名，否则用 NULL 占位（保证列序号不变）。</summary>
            internal string ColumnOrNull(string table, string column)
                => HasColumn(table, column) ? column : "NULL";
        }

        // ================= 装载上下文 =================
        private sealed class Ctx
        {
            /// <summary>`{namecode:N}` 占位符。</summary>
            internal static readonly Regex NameCodeRegex = new(@"\{namecode:(\d+)\}", RegexOptions.Compiled);
            public readonly Dictionary<string, string> Zh = new(StringComparer.Ordinal);
            public readonly Dictionary<string, string> En = new(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Jp = new(StringComparer.Ordinal);

            public readonly Dictionary<long, string> FactionCode = new();
            public readonly Dictionary<long, (string Code, string BaseCode)> ShipTypeInfo = new();
            public readonly Dictionary<long, string> RarityCode = new();
            public readonly Dictionary<long, string> ArmorCode = new();
            public readonly Dictionary<long, string> CodexCode = new();
            public readonly Dictionary<long, string> ClassNameKey = new();
            public readonly Dictionary<long, string> SkinTypeCode = new();
            public readonly Dictionary<long, string> SceneCode = new();
            public readonly Dictionary<long, string> AcquireTypeCode = new();
            public readonly Dictionary<long, string> SkillTypeCode = new();
            public readonly Dictionary<long, string> StatCode = new();
            public readonly Dictionary<long, string> StaffNameKey = new();
            public readonly Dictionary<long, long> SkinRowToGameId = new();
            /// <summary>图鉴系列：id → (前缀, 补零位数, 代码)。</summary>
            public readonly Dictionary<long, (string Prefix, int Padding, string Code)> CodexSeries = new();
            /// <summary>道具 game_id → 名称文案键（items 表）。</summary>
            public readonly Dictionary<long, string> ItemNameKey = new();
            /// <summary>道具 game_id → 图标相对路径（由 `items.icon_path` 解析并核实文件存在）。</summary>
            public readonly Dictionary<long, string> ItemIconRel = new();
            /// <summary>道具 game_id → 品质（`items.rarity`，用于图标底框）。</summary>
            public readonly Dictionary<long, int> ItemRarity = new();
            /// <summary>技能 id → 名称文案键（`skills.id` / `skills.game_id` 双索引）。</summary>
            public readonly Dictionary<long, string> SkillNameRows = new();
            /// <summary>namecode 编号 → (和谐名, 原名)；DB `name_codes`：code=和谐名、name=原名。</summary>
            public readonly Dictionary<long, (string Harmony, string Original)> NameCodes = new();
            /// <summary>原名（中文）→ 和谐名，用于舰船「和谐名」字段反查。</summary>
            public readonly Dictionary<string, string> HarmonyByName = new(StringComparer.Ordinal);

            /// <summary>i18n 键 → 多语言对象（繁中暂复制简中）；顺带还原 `{namecode:N}` 占位符。</summary>
            public LocalizedString Loc(string? key)
            {
                var loc = new LocalizedString();
                if (string.IsNullOrEmpty(key)) return loc;
                if (Zh.TryGetValue(key, out var zh) && !string.IsNullOrEmpty(zh))
                {
                    loc["zh-Hans"] = zh;
                    loc["zh-Hant"] = zh;
                }
                if (En.TryGetValue(key, out var en) && !string.IsNullOrEmpty(en)) loc["en"] = en;
                if (Jp.TryGetValue(key, out var jp) && !string.IsNullOrEmpty(jp)) loc["ja"] = jp;

                // `{namecode:N}` 一律换成**反和谐原名**（各语言用同一中文名兜底，避免露出裸占位符）
                if (loc.Count > 0 && loc.Any(kv => kv.Value.Contains("{namecode:", StringComparison.Ordinal)))
                {
                    foreach (string lang in loc.Keys.ToList())
                        loc[lang] = ReplaceNameCodes(loc[lang]);
                }
                return loc;
            }

            /// <summary>把文本里的 `{namecode:N}` 替换为原名；查不到的编号保持原样。</summary>
            public string ReplaceNameCodes(string text)
            {
                if (string.IsNullOrEmpty(text) || !text.Contains("{namecode:", StringComparison.Ordinal)) return text;
                return NameCodeRegex.Replace(text, m =>
                    long.TryParse(m.Groups[1].Value, out long id) && NameCodes.TryGetValue(id, out var pair)
                        ? pair.Original
                        : m.Value);
            }

            public string Text(string? key)
            {
                if (string.IsNullOrEmpty(key)) return "";
                if (Zh.TryGetValue(key, out var zh) && !string.IsNullOrEmpty(zh)) return zh;
                if (En.TryGetValue(key, out var en) && !string.IsNullOrEmpty(en)) return en;
                return "";
            }
        }

        private static List<ShipStatic> Load(string dbPath, List<string> warnings)
        {
            using var db = SqliteDatabase.OpenReadOnly(dbPath);
            var ctx = new Ctx();
            var schema = new DbSchema(db);

            // ---- 旧库兼容提示（数据目录里可能是升级前的副本） ----
            var missing = new List<string>();
            if (!schema.HasColumn("characters", "codex_no")) missing.Add("characters.codex_no");
            if (!schema.HasColumn("characters", "build_pool")) missing.Add("characters.build_pool");
            if (!schema.HasTable("character_performance")) missing.Add("character_performance");
            if (!schema.HasTable("spweapons")) missing.Add("spweapons");
            if (!schema.HasTable("ship_blueprints")) missing.Add("ship_blueprints");
            if (!schema.HasTable("character_gift_dislike")) missing.Add("character_gift_dislike");
            if (!schema.HasTable("items")) missing.Add("items");
            if (!schema.HasTable("transform_data_templates")) missing.Add("transform_data_templates");
            if (!schema.HasColumn("character_refit_step", "real_item_id")) missing.Add("character_refit_step.real_item_id");
            if (missing.Count > 0)
            {
                warnings.Add("数据库版本较旧，缺少：" + string.Join("、", missing) +
                             "（相关字段按空值显示；请用新版 ships_static.db 替换数据目录里的副本）");
            }

            // ---- i18n ----
            using (var q = db.Query("SELECT key, lang, value FROM i18n"))
            {
                while (q.Read())
                {
                    string? key = q.Text(0);
                    string? lang = q.Text(1);
                    string? value = q.Text(2);
                    if (string.IsNullOrEmpty(key) || value == null) continue;
                    switch (lang)
                    {
                        case "zh": ctx.Zh[key] = value; break;
                        case "en": ctx.En[key] = value; break;
                        case "jp": ctx.Jp[key] = value; break;
                    }
                }
            }

            // ---- 字典表 ----
            using (var q = db.Query("SELECT id, code FROM factions"))
                while (q.Read()) ctx.FactionCode[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, code, base_code FROM ship_types"))
                while (q.Read()) ctx.ShipTypeInfo[q.Long(0)] = (q.Text(1) ?? "", q.Text(2) ?? "");
            using (var q = db.Query("SELECT id, code FROM rarities"))
                while (q.Read()) ctx.RarityCode[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, code FROM armor_type"))
                while (q.Read()) ctx.ArmorCode[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, code FROM codex_series"))
                while (q.Read()) ctx.CodexCode[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, name_key FROM ship_classes"))
                while (q.Read()) ctx.ClassNameKey[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, code FROM skin_types"))
                while (q.Read()) ctx.SkinTypeCode[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, code FROM line_scenes"))
                while (q.Read()) ctx.SceneCode[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, code FROM acquire_types"))
                while (q.Read()) ctx.AcquireTypeCode[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, code FROM skill_type"))
                while (q.Read()) ctx.SkillTypeCode[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, code FROM stat_ids"))
                while (q.Read()) ctx.StatCode[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, name_key FROM staff"))
                while (q.Read()) ctx.StaffNameKey[q.Long(0)] = q.Text(1) ?? "";
            using (var q = db.Query("SELECT id, game_id FROM skins"))
                while (q.Read())
                {
                    long rowId = q.Long(0);
                    if (int.TryParse(q.Text(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int sid))
                        ctx.SkinRowToGameId[rowId] = sid;
                }
            if (schema.HasTable("codex_series"))
            {
                string sql = "SELECT id, code, " + schema.ColumnOrNull("codex_series", "prefix") + ", "
                             + schema.ColumnOrNull("codex_series", "padding") + " FROM codex_series";
                using var q = db.Query(sql);
                while (q.Read())
                    ctx.CodexSeries[q.Long(0)] = (q.Text(2) ?? "", q.Int(3), q.Text(1) ?? "");
            }
            if (schema.HasTable("items"))
            {
                string iconColumn = schema.ColumnOrNull("items", "icon_path");
                string rarityColumn = schema.ColumnOrNull("items", "rarity");
                using var q = db.Query("SELECT game_id, name_key, " + iconColumn + ", " + rarityColumn + " FROM items");
                while (q.Read())
                {
                    long gameId = q.Long(0);
                    string key = q.Text(1) ?? "";
                    if (gameId <= 0) continue;
                    if (key.Length > 0) ctx.ItemNameKey[gameId] = key;
                    string iconRel = ResolveItemIconRel(q.Text(2));
                    if (iconRel.Length > 0) ctx.ItemIconRel[gameId] = iconRel;
                    int rarity = q.Int(3);
                    if (rarity > 0) ctx.ItemRarity[gameId] = rarity;
                }
            }

            // 和谐名对照（`{namecode:N}`）：code=和谐名、name=原名
            if (schema.HasTable("name_codes"))
            {
                using var q = db.Query("SELECT id, code, name FROM name_codes");
                while (q.Read())
                {
                    long id = q.Long(0);
                    string harmony = q.Text(1) ?? "";
                    string original = q.Text(2) ?? "";
                    if (id <= 0 || original.Length == 0) continue;
                    ctx.NameCodes[id] = (harmony, original);
                    if (harmony.Length > 0) ctx.HarmonyByName[original] = harmony;
                }
            }

            // ---- 按船分组的附属数据 ----
            var stats = new Dictionary<long, List<long[]>>();
            using (var q = db.Query("SELECT character_id, breakthrough, hp, armor, reload, firepower, torpedo, evasion, aa, aviation, accuracy, asw, luck, oil, speed FROM character_stats ORDER BY character_id, breakthrough"))
            {
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (!stats.TryGetValue(cid, out var list)) stats[cid] = list = new List<long[]>();
                    list.Add(new[]
                    {
                        q.Long(1), q.Long(2), q.Long(3), q.Long(4), q.Long(5), q.Long(6), q.Long(7),
                        q.Long(8), q.Long(9), q.Long(10), q.Long(11), q.Long(12), q.Long(13), q.Long(14)
                    });
                    // 速度是 REAL，单独存回列表尾部会破坏类型；下面按需再查
                }
            }

            var growth = new Dictionary<long, long[]>();
            using (var q = db.Query("SELECT character_id, scale_hp, scale_fp, scale_trp, scale_aa, scale_avi, scale_rld, scale_hit, scale_eva, scale_asw, scale_spd, scale_luck, scale_extra_hp, scale_extra_fp, scale_extra_trp, scale_extra_aa, scale_extra_avi, scale_extra_rld, scale_extra_hit, scale_extra_eva, scale_extra_asw, scale_extra_spd, scale_extra_luck FROM character_stat_growth"))
            {
                while (q.Read())
                {
                    var row = new long[23];
                    for (int i = 1; i <= 22; i++) row[i - 1] = q.Long(i);
                    growth[q.Long(0)] = row;
                }
            }

            var slots = new Dictionary<long, List<long[]>>();
            using (var q = db.Query("SELECT character_id, breakthrough, slot_index, efficiency, base_mounts, preload, parallel, capacity, default_equip_id FROM character_slots ORDER BY character_id, breakthrough, slot_index"))
            {
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (!slots.TryGetValue(cid, out var list)) slots[cid] = list = new List<long[]>();
                    list.Add(new[] { q.Long(1), q.Long(2), (long)(q.Double(3) * 1000), q.Long(4), q.Long(5), q.Long(6), q.Long(7, -1), q.Long(8, -1) });
                }
            }

            var slotTypes = new Dictionary<long, List<(int Slot, int TypeId)>>();
            using (var q = db.Query("SELECT character_id, slot_index, equip_type_id FROM character_slot_types ORDER BY character_id, slot_index, sort_order"))
            {
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (!slotTypes.TryGetValue(cid, out var list)) slotTypes[cid] = list = new List<(int, int)>();
                    list.Add((q.Int(1), q.Int(2)));
                }
            }

            var charSkills = new Dictionary<long, List<(int Slot, long SkillRowId)>>();
            using (var q = db.Query("SELECT character_id, skill_id, slot_index FROM character_skills ORDER BY character_id, slot_index"))
            {
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (!charSkills.TryGetValue(cid, out var list)) charSkills[cid] = list = new List<(int, long)>();
                    list.Add((q.Int(2), q.Long(1)));
                }
            }

            var skillRows = new Dictionary<long, (string NameKey, string DescKey, int TypeId, int MaxLevel, long GameId)>();
            using (var q = db.Query("SELECT id, name_key, desc_key, skill_type_id, max_level, game_id FROM skills"))
            {
                while (q.Read())
                {
                    long skillRowId = q.Long(0);
                    string nameKey = q.Text(1) ?? "";
                    skillRows[skillRowId] = (nameKey, q.Text(2) ?? "", q.Int(3), q.Int(4, 1), q.Long(5));
                    // 改造模板里的 skill_id 用 game_id（= 行 id）指向技能，两个键都建，避免错位
                    if (nameKey.Length > 0)
                    {
                        ctx.SkillNameRows[skillRowId] = nameKey;
                        long gameId = q.Long(5);
                        if (gameId > 0) ctx.SkillNameRows[gameId] = nameKey;
                    }
                }
            }

            // 技能图标：文件按 skills.game_id 命名；缺图时回退到「强化前」的同名技能图标
            bool HasSkillIcon(long gameId) => gameId > 0 && AssetExists("icon/skill/" + gameId + ".png");
            var iconByName = new Dictionary<string, long>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var row in skillRows.Values)
            {
                if (!HasSkillIcon(row.GameId)) continue;
                var loc = ctx.Loc(row.NameKey);
                foreach (var lang in new[] { "zh-Hans", "en" })
                {
                    if (loc.TryGetValue(lang, out var text) && !string.IsNullOrWhiteSpace(text))
                        iconByName[text.Trim()] = row.GameId;
                }
            }
            bool HasSkillIconFile(long iconId) => iconId > 0 && HasSkillIcon(iconId);

            string ResolveSkillIcon(long gameId, string nameKey, GameSkillKind kind, ShipType? hull)
            {
                if (HasSkillIconFile(gameId)) return "icon/skill/" + gameId + ".png";

                switch (gameId)
                {
                    case 6411:
                    case 6415:
                        return "icon/item/equips/720.png";
                    case 9061:
                        return "icon/skill/2060.png";
                    case 9071:
                        return "icon/skill/1060.png";
                    case 9090:
                        return "icon/skill/11200.png";
                }

                var loc = ctx.Loc(nameKey);
                loc.TryGetValue("zh-Hans", out var zhName);
                loc.TryGetValue("en", out var enName);
                zhName ??= "";
                enName ??= "";

                // 全弹发射：id 段不统一（200xx/210xx/220xx/250xxx/30192…），按名称 + 舰种归并
                bool isAllOutAssault = zhName.Contains("全弹发射") ||
                                       enName.Contains("All Out Assault", StringComparison.OrdinalIgnoreCase) ||
                                       (gameId >= 20000 && gameId <= 29999);
                if (isAllOutAssault)
                {
                    long assaultIcon = hull switch
                    {
                        ShipType.DD or ShipType.DDG => 20000,
                        ShipType.CL => 20100,
                        ShipType.CA or ShipType.CB => 20200,
                        ShipType.SS or ShipType.SSV => 20300,
                        _ => 0
                    };
                    if (HasSkillIconFile(assaultIcon)) return "icon/skill/" + assaultIcon + ".png";
                }

                // 指定区间：末位清零（abcd→abc0、abcde→abcd0）
                bool inTensRange = (gameId >= 1000 && gameId <= 5999) || gameId == 10522 || gameId == 19002 ||
                                   (gameId >= 29000 && gameId <= 802402);
                if (inTensRange)
                {
                    long tens = (gameId / 10) * 10;
                    if (HasSkillIconFile(tens)) return "icon/skill/" + tens + ".png";
                }

                // 名称带「.改」后缀 → 用同名技能图标
                foreach (string raw in new[] { zhName, enName })
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    string baseName = System.Text.RegularExpressions.Regex.Replace(raw.Trim(), @"\s*[\.·]?改$", "").Trim();
                    if (!string.IsNullOrEmpty(baseName) && iconByName.TryGetValue(baseName, out var baseIcon))
                        return "icon/skill/" + baseIcon + ".png";
                }

                // 序号降级（II→I → 去序号）
                foreach (string raw in new[] { zhName, enName })
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    foreach (string candidate in EnumerateDowngradeNames(raw))
                    {
                        if (iconByName.TryGetValue(candidate, out var iconId))
                            return "icon/skill/" + iconId + ".png";
                    }
                }

                return kind switch
                {
                    GameSkillKind.Offensive => "icon/skill/Attack_Default.png",
                    GameSkillKind.Defensive => "icon/skill/Defence_Default.png",
                    GameSkillKind.Support => "icon/skill/Help_Default.png",
                    _ => "icon/item/unkown.png"
                };
            }

            var skillValues = new Dictionary<long, List<string[]>>();
            using (var q = db.Query("SELECT skill_id, level, value1, value2, value3, value4, value5, value6, value7, value8 FROM skill_values ORDER BY skill_id, level"))
            {
                while (q.Read())
                {
                    long sid = q.Long(0);
                    if (!skillValues.TryGetValue(sid, out var list)) skillValues[sid] = list = new List<string[]>();
                    var vals = new string[8];
                    for (int i = 0; i < 8; i++) vals[i] = q.Text(2 + i) ?? "";
                    list.Add(vals);
                }
            }

            var refit = new Dictionary<long, (int Type, int StepCount, int SkillId)>();
            if (schema.HasTable("character_refit"))
            {
                string sql = "SELECT character_id, refit_type, step_count, "
                             + schema.ColumnOrNull("character_refit", "refit_skill_id") + " FROM character_refit";
                using var q = db.Query(sql);
                while (q.Read()) refit[q.Long(0)] = (q.Int(1), q.Int(2), q.Int(3));
            }

            var refitSteps = new Dictionary<long, List<(int Step, int ItemType, int TemplateId, int RealItemId, int RealAmount)>>();
            if (schema.HasTable("character_refit_step"))
            {
                // 旧库只有 item_id/amount；新库多出 real_item_id/real_amount（真实材料）
                string sql = "SELECT character_id, step, item_type, item_id, "
                             + schema.ColumnOrNull("character_refit_step", "real_item_id") + ", "
                             + schema.ColumnOrNull("character_refit_step", "real_amount")
                             + " FROM character_refit_step ORDER BY character_id, step";
                using var q = db.Query(sql);
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (!refitSteps.TryGetValue(cid, out var list))
                        refitSteps[cid] = list = new List<(int, int, int, int, int)>();
                    // item_id = 改造步骤模板 id（transform_data_templates）；real_item_id = 真实材料道具（items.game_id）
                    list.Add((q.Int(1), q.Int(2), q.Int(3), q.Int(4), q.Int(5, 1)));
                }
            }

            // 改造步骤模板：步骤 id → 名称 / 消耗物资 / 属性加成 / 星级与等级门槛 / 节点图标 / 习得技能 / 是否吃本体
            var refitTemplates = new Dictionary<long, (string NameKey, int Gold, string Effect, int StarLimit, int LevelLimit, string Icon, int SkillId, int UseShip)>();
            if (schema.HasTable("transform_data_templates"))
            {
                using var q = db.Query("SELECT id, name_key, use_gold, effect, star_limit, level_limit, "
                                       + schema.ColumnOrNull("transform_data_templates", "icon") + ", "
                                       + schema.ColumnOrNull("transform_data_templates", "skill_id") + ", "
                                       + schema.ColumnOrNull("transform_data_templates", "use_ship")
                                       + " FROM transform_data_templates");
                while (q.Read())
                {
                    refitTemplates[q.Long(0)] = (q.Text(1) ?? "", q.Int(2), q.Text(3) ?? "",
                                                q.Int(4), q.Int(5), q.Text(6) ?? "", q.Int(7), q.Int(8));
                }
            }

            var enhance = new Dictionary<long, (int Fp, int Trp, int Avi, int Rld, int Dur, string AttrExp, string LevelExp)>();
            using (var q = db.Query("SELECT character_id, firepower, torpedo, aviation, reload, durability, "
                                   + schema.ColumnOrNull("character_enhance", "attr_exp") + ", "
                                   + schema.ColumnOrNull("character_enhance", "level_exp")
                                   + " FROM character_enhance"))
            {
                while (q.Read())
                    enhance[q.Long(0)] = (q.Int(1), q.Int(2), q.Int(3), q.Int(4), q.Int(5), q.Text(6) ?? "", q.Text(7) ?? "");
            }

            var tech = new Dictionary<long, Dictionary<int, (int Pts, int StatId, int Value, int Target)>>();
            using (var q = db.Query("SELECT character_id, tier, tech_point, stat, value, target_type FROM character_fleet_tech"))
            {
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (!tech.TryGetValue(cid, out var map)) tech[cid] = map = new Dictionary<int, (int, int, int, int)>();
                    map[q.Int(1)] = (q.Int(2), q.Int(3), q.Int(4), q.Int(5));
                }
            }

            var retire = new Dictionary<long, (int Coin, string Note, int MedalN, int MedalR, int MedalSr, int MedalSsr, int MedalUr, int UrExtraId, int UrExtraAmount)>();
            using (var q = db.Query("SELECT ship_type_id, coin, medal_note, "
                                   + schema.ColumnOrNull("character_retire", "medal_n") + ", "
                                   + schema.ColumnOrNull("character_retire", "medal_r") + ", "
                                   + schema.ColumnOrNull("character_retire", "medal_sr") + ", "
                                   + schema.ColumnOrNull("character_retire", "medal_ssr") + ", "
                                   + schema.ColumnOrNull("character_retire", "medal_ur") + ", "
                                   + schema.ColumnOrNull("character_retire", "ur_extra_item_id") + ", "
                                   + schema.ColumnOrNull("character_retire", "ur_extra_amount")
                                   + " FROM character_retire"))
            {
                while (q.Read())
                    retire[q.Long(0)] = (q.Int(1), q.Text(2) ?? "", q.Int(3), q.Int(4), q.Int(5), q.Int(6), q.Int(7), q.Int(8), q.Int(9));
            }

            var acquire = new Dictionary<long, List<(long AcqId, int TypeId, long MapId, long EventId, string ShopKey, string CostKey, bool InCodex)>>();
            using (var q = db.Query("SELECT character_id, acquire_id, am.type_id, am.map_id, am.event_id, am.shop_key, am.cost_key, "
                                   + schema.ColumnOrNull("character_acquire", "in_codex")
                                   + " FROM character_acquire ca JOIN acquire_methods am ON am.id = ca.acquire_id ORDER BY ca.character_id"))
            {
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (!acquire.TryGetValue(cid, out var list)) acquire[cid] = list = new List<(long, int, long, long, string, string, bool)>();
                    list.Add((q.Long(1), q.Int(2), q.Long(3), q.Long(4), q.Text(5) ?? "", q.Text(6) ?? "", q.Int(7) != 0));
                }
            }

            // ---- 建造补充（时间 / 常驻池 / 保底兑换 / 限时 UP） ----
            var buildTimeOf = new Dictionary<long, string>();
            var buildPoolOf = new Dictionary<long, List<string>>();
            var exchangeOf = new Dictionary<long, string>();
            var limitedOf = new Dictionary<long, List<string>>();

            if (schema.HasTable("build_times"))
            {
                using var q = db.Query("SELECT character_id, build_time FROM build_times WHERE build_time IS NOT NULL AND build_time <> ''");
                while (q.Read())
                {
                    long cid = q.Long(0);
                    string t = q.Text(1) ?? "";
                    if (cid > 0 && t.Length > 0) buildTimeOf[cid] = t;
                }
            }
            if (schema.HasTable("build_pool_members"))
            {
                using var q = db.Query("SELECT DISTINCT character_id, pool FROM build_pool_members WHERE character_id IS NOT NULL");
                while (q.Read())
                {
                    long cid = q.Long(0);
                    string pool = q.Text(1) ?? "";
                    if (cid <= 0 || pool.Length == 0) continue;
                    if (!buildPoolOf.TryGetValue(cid, out var list)) buildPoolOf[cid] = list = new List<string>();
                    if (!list.Contains(pool)) list.Add(pool);
                }
            }
            if (schema.HasTable("build_exchange"))
            {
                using var q = db.Query("SELECT id, exchange_request, ships FROM build_exchange WHERE ships IS NOT NULL AND ships <> ''");
                while (q.Read())
                {
                    long cid = q.Long(0);
                    int points = q.Int(1);
                    string exchangeShips = q.Text(2) ?? "";
                    if (cid <= 0 || exchangeShips.Length == 0) continue;
                    exchangeOf[cid] = points > 0 ? $"{points} 点数｜可兑换：{exchangeShips}" : $"可兑换：{exchangeShips}";
                }
            }
            if (schema.HasTable("build_activity_ships"))
            {
                using var q = db.Query("SELECT character_id, rate, is_up FROM build_activity_ships WHERE character_id IS NOT NULL");
                while (q.Read())
                {
                    long cid = q.Long(0);
                    string rate = q.Text(1) ?? "";
                    int up = q.Int(2);
                    if (cid <= 0 || rate.Length == 0) continue;
                    if (!limitedOf.TryGetValue(cid, out var list)) limitedOf[cid] = list = new List<string>();
                    string text = (up == 1 ? "UP " : "") + rate;
                    if (!list.Contains(text)) list.Add(text);
                }
            }

            // ---- 打捞（map_drops + maps + map_types + events），用于「全关卡」压缩 ----
            var dropMapMeta = new Dictionary<long, (string TypeCode, int Chapter, long EventId, string NameKey)>();
            if (schema.HasTable("maps") && schema.HasTable("map_types"))
            {
                using var q = db.Query(
                    "SELECT m.id, mt.code, COALESCE(m.chapter, 0), COALESCE(m.event_id, 0), COALESCE(m.name_key, '') " +
                    "FROM maps m LEFT JOIN map_types mt ON mt.id = m.map_type_id");
                while (q.Read())
                    dropMapMeta[q.Long(0)] = (q.Text(1) ?? "", q.Int(2), q.Long(3), q.Text(4) ?? "");
            }

            var dropsByChar = new Dictionary<long, List<(long MapId, string Source)>>();
            if (schema.HasTable("map_drops"))
            {
                using var q = db.Query("SELECT map_id, character_id, COALESCE(source_type, '') FROM map_drops");
                while (q.Read())
                {
                    long cid = q.Long(1);
                    if (cid <= 0) continue;
                    if (!dropsByChar.TryGetValue(cid, out var list)) dropsByChar[cid] = list = new List<(long, string)>();
                    list.Add((q.Long(0), q.Text(2) ?? ""));
                }
            }

            // 每个章节/活动的「全部普通关卡」集合（用于全关卡判定）
            var chapterAllStages = new Dictionary<int, HashSet<int>>();
            var eventNormalStages = new Dictionary<long, HashSet<string>>();
            foreach (var (mapId, meta) in dropMapMeta)
            {
                if (meta.TypeCode == "main" && meta.Chapter > 0)
                {
                    int stage = StageNumber(meta.NameKey);
                    if (stage <= 0) continue;
                    if (!chapterAllStages.TryGetValue(meta.Chapter, out var set))
                        chapterAllStages[meta.Chapter] = set = new HashSet<int>();
                    set.Add(stage);
                }
                else if (meta.EventId > 0)
                {
                    string key = NormalStageKey(meta.NameKey);
                    if (key == null) continue;
                    if (!eventNormalStages.TryGetValue(meta.EventId, out var set))
                        eventNormalStages[meta.EventId] = set = new HashSet<string>();
                    set.Add(key);
                }
            }

            // 活动名（events.name_key → i18n）
            var eventNameOf = new Dictionary<long, string>();
            if (schema.HasTable("events"))
            {
                using var q = db.Query("SELECT id, name_key FROM events");
                while (q.Read())
                {
                    long id = q.Long(0);
                    string text = ctx.Loc(q.Text(1)).GetLocalized();
                    if (id > 0 && !string.IsNullOrEmpty(text)) eventNameOf[id] = text;
                }
            }

            var staffByChar = new Dictionary<long, List<(string Role, long StaffId)>>();
            using (var q = db.Query("SELECT character_id, staff_id, role FROM character_staff ORDER BY character_id"))
            {
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (!staffByChar.TryGetValue(cid, out var list)) staffByChar[cid] = list = new List<(string, long)>();
                    list.Add((q.Text(2) ?? "", q.Long(1)));
                }
            }

            var skins = new Dictionary<long, List<long>>();
            using (var q = db.Query("SELECT id, character_id, game_id, name_key, skin_type_id, release_date, is_l2d, is_dynamic, is_asmr, has_custom_bgm, has_dynamic_bg, is_dual_form, is_l2d_plus, price, currency, is_permanent, servers FROM skins ORDER BY character_id"))
            {
                while (q.Read())
                {
                    long cid = q.Long(1);
                    if (!skins.TryGetValue(cid, out var list)) skins[cid] = list = new List<long>();
                    list.Add(q.Long(0));   // 后续按 row id 另行填充
                }
            }

            var skinInfo = new Dictionary<long, Skin>();
            using (var q = db.Query("SELECT id, game_id, name_key, skin_type_id, release_date, is_l2d, is_dynamic, is_asmr, has_custom_bgm, has_dynamic_bg, is_dual_form, is_l2d_plus, price, currency, is_permanent, servers FROM skins"))
            {
                while (q.Read())
                {
                    long rowId = q.Long(0);
                    int.TryParse(q.Text(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int gameSkinId);
                    var kind = SkinKindFromCode(ctx, q.Long(3));
                    var skin = new Skin
                    {
                        Id = gameSkinId,
                        Name = ctx.Loc(q.Text(2)),
                        Kind = kind,
                        Price = q.Int(12),
                        Currency = q.Int(13),
                        IsPermanent = q.Long(14) == 1,
                        L2d = q.Long(5) == 1,
                        Dynamic = q.Long(6) == 1,
                        Asmr = q.Long(7) == 1,
                        CustomBgm = q.Long(8) == 1,
                        DynamicBg = q.Long(9) == 1,
                        DualForm = q.Long(10) == 1,
                        L2dPlus = q.Long(11) == 1,
                        ObtainType = q.Int(12) > 0 ? SkinObtainType.Gems : SkinObtainType.Unobtainable
                    };
                    string servers = q.Text(15) ?? "";
                    if (!string.IsNullOrEmpty(servers))
                        skin.Servers.AddRange(servers.Split(new[] { ',', ';', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct());
                    skinInfo[rowId] = skin;
                }
            }

            var lines = new Dictionary<long, List<(int GameSkinId, string Scene, string TextKey, int Sort, int SceneOrder, string SceneNameKey)>>();
            using (var q = db.Query("SELECT l.character_id, l.skin_id, s.code, l.text_key, l.sort_order, s.name_key, s.sort_order "
                                    + "FROM lines l LEFT JOIN line_scenes s ON s.id = l.scene_id "
                                    + "ORDER BY l.character_id, l.skin_id, l.sort_order"))
            {
                while (q.Read())
                {
                    long cid = q.Long(0);
                    long skinRow = q.Long(1);
                    int gameSkinId = skinRow > 0 && ctx.SkinRowToGameId.TryGetValue(skinRow, out long gid) ? (int)gid : -1;
                    if (!lines.TryGetValue(cid, out var list)) lines[cid] = list = new List<(int, string, string, int, int, string)>();
                    // 列序：0=角色 1=皮肤行 2=场景代码 3=文案键 4=行内序号 5=场景文案键 6=场景排序
                    list.Add((gameSkinId, q.Text(2) ?? "", q.Text(3) ?? "", q.Int(4), q.Int(6), q.Text(5) ?? ""));
                }
            }

            var eventsById = new Dictionary<long, string>();
            using (var q = db.Query("SELECT id, name_key FROM events"))
            {
                while (q.Read()) eventsById[q.Long(0)] = q.Text(1) ?? "";
            }

            /// 事件名文案键是 `{events.name_key}_name`（如 event_2017_kaifu_name），裸键没有 i18n
            LocalizedString EventName(long eventId)
            {
                if (eventId <= 0 || !eventsById.TryGetValue(eventId, out var evKey) || evKey.Length == 0)
                    return new LocalizedString();
                var loc = ctx.Loc(evKey + "_name");
                if (loc.Count > 0) return loc;
                return ctx.Loc(evKey);
            }

            /// 实装活动：`characters.release_event_id` → events → `{name_key}_name`
            var releaseEventOf = new Dictionary<long, long>();
            if (schema.HasColumn("characters", "release_event_id"))
            {
                using var q = db.Query("SELECT id, release_event_id FROM characters WHERE release_event_id IS NOT NULL");
                while (q.Read())
                {
                    long cid = q.Long(0);
                    long eventId = q.Long(1);
                    if (cid > 0 && eventId > 0) releaseEventOf[cid] = eventId;
                }
            }

            // ---- 图鉴六维评价（1~7 → F..S） ----
            var performance = new Dictionary<long, int[]>();
            if (schema.HasTable("character_performance"))
            {
                using var q = db.Query("SELECT character_id, durability, aa, evasion, aviation, torpedo, firepower FROM character_performance");
                while (q.Read())
                {
                    performance[q.Long(0)] = new[] { q.Int(1), q.Int(2), q.Int(3), q.Int(4), q.Int(5), q.Int(6) };
                }
            }

            // ---- 专属兵装（spweapons 按 unique_character_id 分组；一船可能多件） ----
            var spweaponTypeShips = new Dictionary<long, List<int>>();
            if (schema.HasTable("spweapon_types"))
            {
                using var q = db.Query("SELECT id, ship_types FROM spweapon_types");
                while (q.Read())
                {
                    var types = new List<int>();
                    foreach (int t in ParseIntArray(q.Text(1) ?? ""))
                        if (t > 0) types.Add(t);
                    spweaponTypeShips[q.Long(0)] = types;
                }
            }

            var spweaponsByChar = new Dictionary<long, List<(int GameId, string NameKey, string DescKey, int RarityId,
                long TypeId, int EffectSkillRowId, int UpgradeSkillRowId, string Attr1, int Val1, int Rand1, string Attr2, int Val2, int Rand2)>>();
            if (schema.HasTable("spweapons"))
            {
                string sql = "SELECT game_id, name_key, desc_key, rarity_id, unique_character_id, effect_skill_id, "
                             + "skill_upgrade_id, "
                             + schema.ColumnOrNull("spweapons", "attribute_1") + ", "
                             + schema.ColumnOrNull("spweapons", "value_1") + ", "
                             + schema.ColumnOrNull("spweapons", "value_1_random") + ", "
                             + schema.ColumnOrNull("spweapons", "attribute_2") + ", "
                             + schema.ColumnOrNull("spweapons", "value_2") + ", "
                             + schema.ColumnOrNull("spweapons", "value_2_random") + ", "
                             + schema.ColumnOrNull("spweapons", "spweapon_type_id")
                             + " FROM spweapons WHERE unique_character_id IS NOT NULL";
                using var q = db.Query(sql);
                while (q.Read())
                {
                    long cid = q.Long(4);
                    if (cid <= 0) continue;
                    if (!spweaponsByChar.TryGetValue(cid, out var list))
                        spweaponsByChar[cid] = list = new List<(int, string, string, int, long, int, int, string, int, int, string, int, int)>();
                    list.Add((q.Int(0), q.Text(1) ?? "", q.Text(2) ?? "", q.Int(3), q.Long(13),
                              q.Int(5), q.Int(6),
                              q.Text(7) ?? "", q.Int(8), q.Int(9),
                              q.Text(10) ?? "", q.Int(11), q.Int(12)));
                }
            }

            // ---- 科研开发 / 天运（ship_blueprints 按 character_id 分组） ----
            var blueprints = new Dictionary<long, (string UnlockWord, string StrengthenEffect, int StrengthenItem,
                string FateStrengthen, string UnlockTask, int Price, string GainItems, string ChangeSkill)>();
            if (schema.HasTable("ship_blueprints"))
            {
                bool hasGain = schema.ColumnOrNull("ship_blueprints", "gain_item_id") != null;
                bool hasChange = schema.ColumnOrNull("ship_blueprints", "change_skill") != null;
                string sql = "SELECT character_id, unlock_word, strengthen_effect, strengthen_item, fate_strengthen, unlock_task, price" +
                             (hasGain ? ", gain_item_id" : ", NULL") +
                             (hasChange ? ", change_skill" : ", NULL") +
                             " FROM ship_blueprints WHERE character_id IS NOT NULL";
                using var q = db.Query(sql);
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (cid <= 0) continue;
                    blueprints[cid] = (q.Text(1) ?? "", q.Text(2) ?? "", q.Int(3),
                                       q.Text(4) ?? "", q.Text(5) ?? "", q.Int(6),
                                       q.Text(7) ?? "", q.Text(8) ?? "");
                }
            }

            // ---- META：突破 / 强化材料 / 技能任务 ----
            var metaBreakouts = new Dictionary<long, List<(int Level, int Mat1, int Num1, int Mat2, int Num2, int Gold, string ViewKey)>>();
            if (schema.HasTable("meta_breakouts"))
            {
                using var q = db.Query("SELECT character_id, level, item1, item1_num, item2, item2_num, gold, view_key FROM meta_breakouts ORDER BY character_id, level");
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (cid <= 0) continue;
                    if (!metaBreakouts.TryGetValue(cid, out var list))
                        metaBreakouts[cid] = list = new List<(int, int, int, int, int, int, string)>();
                    list.Add((q.Int(1), q.Int(2), q.Int(3), q.Int(4), q.Int(5), q.Int(6), q.Text(7) ?? ""));
                }
            }

            var metaRepairs = new List<(int ItemId, int Count, int Exp, string Attr)>();
            if (schema.HasTable("meta_repairs"))
            {
                using var q = db.Query(
                    "SELECT item_id, SUM(item_num), MAX(repair_exp), effect_attr FROM meta_repairs " +
                    "GROUP BY item_id ORDER BY MAX(repair_exp) DESC");
                while (q.Read())
                    metaRepairs.Add((q.Int(0), q.Int(1), q.Int(2), q.Text(3) ?? ""));
            }

            var metaSkillTasks = new Dictionary<long, List<(int Level, int NeedExp, int UnlockCount, int LevelupCount)>>();
            if (schema.HasTable("meta_skill_tasks"))
            {
                using var q = db.Query("SELECT skill_id, level, need_exp, skill_unlock, skill_levelup_task FROM meta_skill_tasks ORDER BY skill_id, level");
                while (q.Read())
                {
                    long sid = q.Long(0);
                    if (sid <= 0) continue;
                    if (!metaSkillTasks.TryGetValue(sid, out var list))
                        metaSkillTasks[sid] = list = new List<(int, int, int, int)>();
                    list.Add((q.Int(1), q.Int(2), CountJsonArray(q.Text(3)), CountJsonArray(q.Text(4))));
                }
            }

            // ---- 礼物偏好（只有"讨厌"清单；item_id = items.game_id） ----
            var giftDislikes = new Dictionary<long, List<int>>();
            if (schema.HasTable("character_gift_dislike"))
            {
                using var q = db.Query("SELECT character_id, item_id FROM character_gift_dislike ORDER BY character_id, item_id");
                while (q.Read())
                {
                    long cid = q.Long(0);
                    if (!giftDislikes.TryGetValue(cid, out var list)) giftDislikes[cid] = list = new List<int>();
                    list.Add(q.Int(1));
                }
            }

            // ---- 主表（codex_no / build_pool 为新增列，旧库用 NULL 占位） ----
            var ships = new List<ShipStatic>();
            string charsSql = "SELECT id, game_id, global_name, name_key, codex_series_id, sort_order, ship_type_id, "
                              + "faction_id, rarity_id, class_id, release_date, can_refit, build_time, "
                              + schema.ColumnOrNull("characters", "codex_no") + ", "
                              + schema.ColumnOrNull("characters", "build_pool")
                              + " FROM characters ORDER BY sort_order";
            using var chr = db.Query(charsSql);
            while (chr.Read())
            {
                long rowId = chr.Long(0);
                string gameId = chr.Text(1) ?? "";
                int.TryParse(gameId, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id);
                if (id <= 0) id = (int)rowId;

                string globalName = chr.Text(2) ?? "";
                string nameKey = chr.Text(3) ?? "";
                long codexId = chr.Long(4);
                string codexCode = ctx.CodexCode.TryGetValue(codexId, out var cc) ? cc : "normal";
                var category = CategoryFromCodex(codexCode);

                long shipTypeId = chr.Long(6);
                var hull = ctx.ShipTypeInfo.TryGetValue(shipTypeId, out var st) ? BaseCodeToHull(st.BaseCode) : null;
                long factionId = chr.Long(7);
                string factionCode = ctx.FactionCode.TryGetValue(factionId, out var fc) ? fc : "other";
                long rarityId = chr.Long(8);
                string rarityCode = ctx.RarityCode.TryGetValue(rarityId, out var rc) ? rc : "N";

                var ship = new ShipStatic
                {
                    Id = id,
                    GlobalName = globalName,
                    Name = ctx.Loc(nameKey),
                    Category = category,
                    Faction = CodeToFaction(factionCode),
                    Type = hull ?? ShipType.UNKNOWN,
                    TypeId = (int)shipTypeId,
                    Rarity = CodeToRarity(rarityCode),
                    GameOrder = chr.Int(5),
                    DateText = chr.Text(10) ?? "",
                    WikiNumber = MakeWikiNumber(ctx, codexId, chr.Text(13))
                };

                long classId = chr.Long(9);
                if (classId > 0 && ctx.ClassNameKey.TryGetValue(classId, out var ckey))
                    ship.Class = ctx.Loc(ckey);

                // 和谐名：按中文原名反查 name_codes（表里 code=和谐名、name=原名）
                string zhShipName = ship.Name.TryGetValue("zh-Hans", out var zhName) ? zhName : "";
                if (zhName.Length > 0 && ctx.HarmonyByName.TryGetValue(zhName, out var harmonyName))
                    ship.HarmonyName = harmonyName;

                bool canRefit = chr.Long(11) == 1;
                ship.Flags = ShipFlags.None;
                if (canRefit) ship.Flags |= ShipFlags.Retro;
                if (category == ShipCategory.Research) ship.Flags |= ShipFlags.Research;
                if (category == ShipCategory.META) ship.Flags |= ShipFlags.Meta;
                if (globalName.IndexOf("Bulin", StringComparison.OrdinalIgnoreCase) >= 0) ship.Flags |= ShipFlags.Bulin;

                // ---- 属性 / 突破档 ----
                if (stats.TryGetValue(rowId, out var statRows))
                {
                    var grow = growth.TryGetValue(rowId, out var g) ? g : null;
                    foreach (var r in statRows)
                    {
                        var lvl = new ShipStatsLevel
                        {
                            Hp = (int)r[1],
                            Armor = (int)r[2] switch { 2 => ArmorType.Medium, 3 => ArmorType.Heavy, _ => ArmorType.Light },
                            Rld = (int)r[3],
                            Fp = (int)r[4],
                            Trp = (int)r[5],
                            Eva = (int)r[6],
                            Aa = (int)r[7],
                            Avi = (int)r[8],
                            Hit = (int)r[9],
                            Asw = (int)r[10],
                            Luck = (int)r[11]
                        };
                        if (grow != null)
                        {
                            lvl.Scaling = new StatsGrowth
                            {
                                Hp = (int)grow[0], Fp = (int)grow[1], Trp = (int)grow[2], Aa = (int)grow[3],
                                Avi = (int)grow[4], Rld = (int)grow[5], Hit = (int)grow[6], Eva = (int)grow[7],
                                Asw = (int)grow[8], Speed = (int)grow[9], Luck = (int)grow[10]
                            };
                            lvl.ScalingExtra = new StatsGrowth
                            {
                                Hp = (int)grow[11], Fp = (int)grow[12], Trp = (int)grow[13], Aa = (int)grow[14],
                                Avi = (int)grow[15], Rld = (int)grow[16], Hit = (int)grow[17], Eva = (int)grow[18],
                                Asw = (int)grow[19], Speed = (int)grow[20], Luck = (int)grow[21]
                            };
                        }
                        // 速度（REAL）按突破档另取
                        lvl.Speed = 0;
                        // 突破档元数据：DB 只提供油耗
                        ship.LimitBreaks.Add(new LimitBreakInfo { OilStart = (int)r[12], OilEnd = (int)r[12] });
                        ship.StatsLevels.Add(lvl);
                    }
                    // 补速度
                    using (var sp = db.Query("SELECT breakthrough, speed FROM character_stats WHERE character_id = ? ORDER BY breakthrough", rowId))
                    {
                        int i = 0;
                        while (sp.Read() && i < ship.StatsLevels.Count)
                        {
                            ship.StatsLevels[i].Speed = sp.Double(1);
                            i++;
                        }
                    }
                }

                // ---- 装备槽（按突破档） ----
                if (slots.TryGetValue(rowId, out var slotRows))
                {
                    var typesBySlot = new Dictionary<int, List<int>>();
                    if (slotTypes.TryGetValue(rowId, out var sts))
                    {
                        foreach (var (slot, typeId) in sts)
                        {
                            if (!typesBySlot.TryGetValue(slot, out var tl)) typesBySlot[slot] = tl = new List<int>();
                            tl.Add(typeId);
                        }
                    }
                    var byLb = slotRows.GroupBy(s => (int)s[0]).OrderBy(gp => gp.Key);
                    foreach (var gp in byLb)
                    {
                        var levelSlots = new List<EquipmentSlotInfo>();
                        foreach (var s in gp)
                        {
                            int slotIndex = (int)s[1];
                            levelSlots.Add(new EquipmentSlotInfo
                            {
                                Types = typesBySlot.TryGetValue(slotIndex, out var tl) ? tl.Distinct().ToList() : new List<int>(),
                                Efficiency = s[2] / 1000.0,
                                Base = (int)s[3],
                                Preload = (int)s[4],
                                Parallel = (int)s[5],
                                Capacity = s[6] >= 0 ? (int)s[6] : null,
                                DefaultEquipId = (int)s[7]
                            });
                        }
                        ship.SlotLevels.Add(levelSlots);
                    }
                }

                // ---- 技能 ----
                if (charSkills.TryGetValue(rowId, out var cs))
                {
                    var ids = new List<int>();
                    foreach (var (slot, skillRowId) in cs.OrderBy(t => t.Slot))
                    {
                        ids.Add((int)skillRowId);
                        if (!skillRows.TryGetValue(skillRowId, out var sr)) continue;
                        var skill = new Skill
                        {
                            Id = (int)skillRowId,
                            Name = ctx.Loc(sr.NameKey),
                            Description = ctx.Loc(sr.DescKey),
                            Kind = SkillKindFromCode(ctx, sr.TypeId),
                            Max = sr.MaxLevel,
                            IconRel = ResolveSkillIcon(sr.GameId, sr.NameKey, SkillKindFromCode(ctx, sr.TypeId), hull)
                        };
                        if (skillValues.TryGetValue(skillRowId, out var valueRows))
                        {
                            int placeholders = 0;
                            foreach (var vr in valueRows)
                                for (int i = 0; i < vr.Length; i++)
                                    if (!string.IsNullOrEmpty(vr[i]) && i + 1 > placeholders) placeholders = i + 1;
                            for (int p = 0; p < placeholders; p++)
                            {
                                var column = new List<string>();
                                foreach (var vr in valueRows) column.Add(p < vr.Length ? vr[p] : "");
                                skill.Values.Add(column);
                            }
                            // skills.max_level 在部分技能（装备技能）为 0：用数值表行数兜底
                            if (skill.Max <= 0 && valueRows.Count > 0) skill.Max = valueRows.Count;
                        }
                        ship.Skills.Add(skill);
                    }
                    if (ids.Count > 0) ship.SkillIdsByLb.Add(ids);
                }

                // ---- 改造（步骤模板给名称/物资/属性/门槛，real_item_id 给真实材料） ----
                if (canRefit || refit.ContainsKey(rowId))
                {
                    var retro = new RetrofitData { CanRetrofit = canRefit };
                    if (refit.TryGetValue(rowId, out var rf))
                    {
                        retro.CanRetrofit = true;
                        retro.StepCount = rf.StepCount;
                        if (rf.SkillId > 0 && skillRows.TryGetValue(rf.SkillId, out var refitSkill))
                            retro.RefitSkillName = ctx.Loc(refitSkill.NameKey);
                        if (refitSteps.TryGetValue(rowId, out var steps))
                        {
                            foreach (var gp in steps.GroupBy(s => s.Step).OrderBy(gp => gp.Key))
                            {
                                // 同一「步骤」的多行 = 该步的多个节点（游戏里 transform_list[step][innerIndex]），
                                // 每行一个节点：模板给名称/图标/技能/属性加成，real_item_id 给该节点真实材料。
                                int innerIndex = 0;
                                foreach (var s in gp.OrderBy(s => s.TemplateId))
                                {
                                    innerIndex++;
                                    string letter = $"S{gp.Key}_{innerIndex}";
                                    var tpl = refitTemplates.TryGetValue(s.TemplateId, out var t)
                                        ? t
                                        : (NameKey: "", Gold: 0, Effect: "", StarLimit: 0, LevelLimit: 0, Icon: "", SkillId: 0, UseShip: 0);

                                    int materialId = s.RealItemId > 0 ? s.RealItemId : s.ItemType;
                                    var items = new List<ItemRef>
                                    {
                                        new()
                                        {
                                            Id = materialId,
                                            Amount = s.RealAmount,
                                            Name = ItemNameOf(ctx, materialId),
                                            IconRel = ItemIconRelOf(ctx, materialId),
                                            Rarity = ItemRarityOf(ctx, materialId)
                                        }
                                    };

                                    retro.Nodes.Add(new RetrofitNode
                                    {
                                        Letter = letter,
                                        Name = ctx.Loc(tpl.NameKey),
                                        MinLevel = tpl.LevelLimit,
                                        MinLb = tpl.StarLimit,
                                        Gold = tpl.Gold,
                                        EffectText = EffectJsonToText(tpl.Effect, tpl.SkillId, ctx),
                                        SkillName = SkillNameOf(ctx, tpl.SkillId),
                                        Step = gp.Key,
                                        IndexInStep = innerIndex,
                                        RequiresShip = tpl.UseShip > 0,
                                        Icon = RefitIconRel(tpl.Icon)
                                    });
                                    retro.CostByLetter[letter] = new RetrofitCost
                                    {
                                        Coins = tpl.Gold,
                                        Copies = 0,
                                        ItemsByRound = new List<List<ItemRef>> { items }
                                    };
                                    if (retro.MinLevel < tpl.LevelLimit) retro.MinLevel = tpl.LevelLimit;
                                    if (retro.MinLb < tpl.StarLimit) retro.MinLb = tpl.StarLimit;
                                }
                            }
                        }
                    }
                    ship.Retrofit = retro;
                }

                // ---- 强化（character_enhance：可强化上限 + 等级经验 + 作为素材给予的经验） ----
                if (enhance.TryGetValue(rowId, out var enh))
                {
                    var data = new EnhanceData
                    {
                        CapFp = enh.Fp,
                        CapTrp = enh.Trp,
                        CapAvi = enh.Avi,
                        CapRld = enh.Rld,
                        CapDur = enh.Dur
                    };
                    // attr_exp = 作为素材给予的经验；5 项按 [炮击,雷击,?,航空,装填] 取用（与旧字段同构）
                    var attr = ParseIntArray(enh.AttrExp);
                    if (attr.Count >= 5)
                    {
                        data.AttrExp.AddRange(attr.Take(5));
                        ship.StrengthenExp = new EnhanceExp
                        {
                            Fp = attr[0], Trp = attr[1], Avi = attr[3], Rld = attr[4], Aa = 0
                        };
                    }
                    var levelExp = ParseIntArray(enh.LevelExp);
                    if (levelExp.Count > 0) data.LevelExp.AddRange(levelExp);
                    if (data.CapFp + data.CapTrp + data.CapAvi + data.CapRld + data.CapDur > 0 || data.AttrExp.Count > 0)
                        ship.Enhance = data;
                }

                // ---- 舰队科技 ----
                if (tech.TryGetValue(rowId, out var tiers))
                {
                    var ft = new FleetTech();
                    if (tiers.TryGetValue(1, out var t1)) ft.Collect = MakeTechBonus(ctx, t1);
                    if (tiers.TryGetValue(2, out var t2)) ft.Level120 = MakeTechBonus(ctx, t2);
                    if (tiers.TryGetValue(3, out var t3)) ft.LimitBreak = MakeTechBonus(ctx, t3);
                    ship.FleetTech = ft;
                }

                // ---- 退役收益（物资按舰种；勋章按稀有度取列，不再由客户端硬编码规则） ----
                if (retire.TryGetValue(shipTypeId, out var ret))
                {
                    ship.Retire = new RetireInfo
                    {
                        Coin = ret.Coin,
                        MedalN = ret.MedalN,
                        MedalR = ret.MedalR,
                        MedalSr = ret.MedalSr,
                        MedalSsr = ret.MedalSsr,
                        MedalUr = ret.MedalUr,
                        UrExtraItemId = ret.UrExtraId,
                        UrExtraAmount = ret.UrExtraAmount,
                        UrExtraItemName = ItemNameOf(ctx, ret.UrExtraId),
                        Note = ret.Note
                    };
                    ship.RetirementReward = !string.IsNullOrEmpty(ret.Note) ? ret.Note : "";
                }

                // ---- 获取方式 ----
                var acq = ship.Acquisition;
                acq.BuildTime = chr.Text(12);
                acq.BuildPool = chr.Text(14) ?? "";
                var notes = new HashSet<string>(StringComparer.Ordinal);
                if (acquire.TryGetValue(rowId, out var acqRows))
                {
                    foreach (var a in acqRows)
                    {
                        string typeCode = ctx.AcquireTypeCode.TryGetValue(a.TypeId, out var tc) ? tc : "";
                        switch (typeCode)
                        {
                            case "build":
                                if (!acq.Light && !acq.Heavy && !acq.Special) acq.Light = true;
                                break;
                            case "map_drop":
                                AddMapDropFromCost(acq, a.CostKey);
                                break;
                            case "shop":
                                // 商店类型在 cost_key.warp（medal/supplies/fragment/guild/meta）
                                foreach (var shop in ParseStringValues(a.CostKey, "warp"))
                                {
                                    AddUnlockType(acq, ShopWarpToUnlock(shop));
                                    string shopName = ShopWarpName(shop);
                                    if (!string.IsNullOrEmpty(shopName) && !acq.ShopNames.Contains(shopName))
                                        acq.ShopNames.Add(shopName);
                                }
                                if (!string.IsNullOrEmpty(a.ShopKey) && acq.UnlockTypes.Count == 0)
                                    AddUnlockType(acq, ShopWarpToUnlock(a.ShopKey.ToLowerInvariant()));
                                break;
                            case "mission":
                                if (!acq.UnlockTypes.Contains(UnlockType.WeeklyMissions)) acq.UnlockTypes.Add(UnlockType.WeeklyMissions);
                                if (!acq.MissionTexts.Contains("每周任务奖励")) acq.MissionTexts.Add("每周任务奖励");
                                break;
                            case "invitation":
                                if (a.ShopKey.StartsWith("item_", StringComparison.OrdinalIgnoreCase)
                                    && int.TryParse(a.ShopKey.Substring(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out int itemGid))
                                {
                                    string itemName = ItemNameOf(ctx, itemGid);
                                    if (!string.IsNullOrEmpty(itemName) && !acq.InvitationItems.Contains(itemName))
                                        acq.InvitationItems.Add(itemName);
                                }
                                break;
                            case "newserver":
                                if (!acq.MissionTexts.Contains("新服活动奖励")) acq.MissionTexts.Add("新服活动奖励");
                                break;
                            case "event_reward":
                                if (!acq.MissionTexts.Contains("活动奖励")) acq.MissionTexts.Add("活动奖励");
                                break;
                            case "pray_build":
                                acq.PrayBuild = true;
                                // cost_key 形如 {"activity_id":50618,"pool":"轻型祈愿池","pickup_num":2}
                                string? prayPool = JsonStringProperty(a.CostKey, "pool");
                                if (!string.IsNullOrEmpty(prayPool) && !acq.PrayPools.Contains(prayPool))
                                    acq.PrayPools.Add(prayPool);
                                break;
                            case "invite":
                                // 带 projectName 的是建造池（light/heavy/special）；否则为勋章支援
                                bool pool = AddPoolFromCost(acq, a.CostKey);
                                if (!pool) AddUnlockType(acq, UnlockType.Requisition);
                                break;
                        }
                        if (a.EventId > 0 && eventsById.TryGetValue(a.EventId, out var evKey))
                        {
                            var evLoc = EventName(a.EventId);
                            if (evLoc.Count > 0 && acq.Events.All(e => e.GetLocalized() != evLoc.GetLocalized()))
                                acq.Events.Add(evLoc);
                        }
                        string descKey = "acq_" + a.AcqId.ToString(CultureInfo.InvariantCulture) + "_desc";
                        var loc = ctx.Loc(descKey);
                        string text = loc.GetLocalized();
                        if (!string.IsNullOrEmpty(text) && notes.Add(text))
                        {
                            // Notes = 全部获取方式（获取方式卡片用）；
                            // CodexNotes = 仅 in_codex=1（基本信息「备注」行用，完整清单看卡片）
                            acq.Notes.Add(loc);
                            if (a.InCodex) acq.CodexNotes.Add(loc);
                        }
                    }
                }
                if (acq.Events.Count > 0) ship.Events.AddRange(acq.Events);

                // 实装活动：DB `characters.release_event_id`（888/902 有值）→ events 名
                if (releaseEventOf.TryGetValue(rowId, out long releaseEventId))
                {
                    var releaseEvent = EventName(releaseEventId);
                    if (releaseEvent.Count > 0) ship.ReleaseEvent = releaseEvent;
                }

                // 建造补充：时间 / 常驻池 / 保底兑换 / 限时 UP
                if (buildTimeOf.TryGetValue(rowId, out var bt) && string.IsNullOrEmpty(acq.BuildTime))
                    acq.BuildTime = bt;
                if (buildPoolOf.TryGetValue(rowId, out var pools))
                {
                    foreach (var pool in pools)
                        if (!acq.BuildPools.Contains(pool)) acq.BuildPools.Add(pool);
                }
                if (exchangeOf.TryGetValue(rowId, out var exchange)) acq.ExchangeText = exchange;
                bool hasPermanentPool = acq.BuildPools.Count > 0
                    || acq.Light || acq.Heavy || acq.Special
                    || !string.IsNullOrEmpty(acq.BuildPool);
                if (!hasPermanentPool && limitedOf.TryGetValue(rowId, out var ups))
                    acq.LimitedText = string.Join("、", ups);

                // ---- 打捞点（map_drops 一行 = 一张图；来源取 source_type，困难图名以 _h 结尾） ----
                if (dropsByChar.TryGetValue(rowId, out var mapDrops))
                {
                    var seenPoints = new HashSet<string>(StringComparer.Ordinal);
                    var chapterHits = new SortedDictionary<int, SortedSet<int>>();

                    foreach (var (mapId, source) in mapDrops)
                    {
                        if (!dropMapMeta.TryGetValue(mapId, out var meta)) continue;

                        string kind = DropSourceKind(source, meta.TypeCode);
                        bool hard = IsHardMap(meta.TypeCode, meta.NameKey);
                        string mapName = MapDisplayName(ctx, meta.NameKey);

                        // 同一张图可能同时出现在 activity 与 rerun 中，按「来源 + 图 + 难度」去重；
                        // 普通图与困难图（map_7_2 / map_7_2_h）名字解析后相同，但难度不同 → 计成两个获取点
                        if (seenPoints.Add($"{kind}|{mapName}|{hard}"))
                        {
                            acq.DropPoints.Add(new DropPointInfo
                            {
                                Source = kind,
                                Map = mapName,
                                Difficulty = kind == "主线" ? (hard ? "困难" : "普通") : "",
                                IsBoss = false
                            });
                        }

                        if (kind == "主线" && meta.Chapter > 0)
                        {
                            int stage = StageNumber(meta.NameKey);
                            if (stage <= 0) continue;
                            if (!chapterHits.TryGetValue(meta.Chapter, out var set))
                                chapterHits[meta.Chapter] = set = new SortedSet<int>();
                            set.Add(stage);
                        }
                    }

                    foreach (var (chapter, stages) in chapterHits)
                    {
                        bool full = chapterAllStages.TryGetValue(chapter, out var all) && all.Count > 0 && all.SetEquals(stages);
                        acq.DropChapterLines.Add(full
                            ? $"第 {chapter} 章全关卡"
                            : $"第 {chapter} 章：{string.Join("、", stages.Select(x => $"{chapter}-{x}"))}");
                    }

                    acq.DropArchiveCount = acq.DropPoints.Count(p => p.Source == "档案");
                    acq.HasMainDrops = chapterHits.Count > 0;
                }

                // ---- 画师 / CV ----
                if (staffByChar.TryGetValue(rowId, out var staffList))
                {
                    foreach (var (role, staffId) in staffList)
                    {
                        if (!ctx.StaffNameKey.TryGetValue(staffId, out var skey)) continue;
                        var loc = ctx.Loc(skey);
                        if (string.Equals(role, "cv", StringComparison.OrdinalIgnoreCase))
                        {
                            if (loc.Count > 0 && ship.CV.Count == 0) ship.CV = loc;
                        }
                        else if (string.Equals(role, "artist", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrEmpty(ship.Artist)) ship.Artist = loc.GetLocalized();
                        }
                    }
                }

                // ---- 皮肤 ----
                if (skins.TryGetValue(rowId, out var skinRowIds))
                {
                    foreach (long srid in skinRowIds)
                    {
                        if (!skinInfo.TryGetValue(srid, out var skin)) continue;
                        ship.Skins.Add(skin);
                        if (skin.Kind == GameSkinKind.Oath) ship.HasOathSkin = true;
                    }
                }

                // ---- 台词（按皮肤分组；组内按场景排序，供 UI 分场景展示） ----
                if (lines.TryGetValue(rowId, out var lineRows))
                {
                    foreach (var gp in lineRows.GroupBy(l => l.GameSkinId).OrderByDescending(gp => gp.Key))
                    {
                        var group = new SkinVoiceGroup { SkinId = gp.Key };
                        foreach (var l in gp.OrderBy(l => l.SceneOrder).ThenBy(l => l.Sort))
                        {
                            var content = ctx.Loc(l.TextKey);
                            if (content.Count == 0) continue;
                            group.Lines.Add(new VoiceLine
                            {
                                Key = l.Scene,
                                Scene = l.Scene,
                                SceneOrder = l.SceneOrder,
                                SceneName = ctx.Loc(l.SceneNameKey),
                                Content = content
                            });
                        }
                        if (group.Lines.Count > 0) ship.WordsBySkin.Add(group);
                    }
                }

                // ---- 图鉴六维评价（1~7 → F..S） ----
                if (performance.TryGetValue(rowId, out var perf))
                {
                    ship.Performance = new ShipPerformance
                    {
                        Durability = perf[0],
                        Aa = perf[1],
                        Evasion = perf[2],
                        Aviation = perf[3],
                        Torpedo = perf[4],
                        Firepower = perf[5]
                    };
                }

                // ---- 礼物偏好（数据库只有"讨厌"清单） ----
                if (giftDislikes.TryGetValue(rowId, out var giftIds))
                {
                    foreach (int giftId in giftIds)
                    {
                        ship.GiftDislikeIds.Add(giftId);
                        ship.GiftDislikeNames.Add(ItemNameOf(ctx, giftId));
                        ship.GiftDislikeIcons.Add(ItemIconRelOf(ctx, giftId));
                        ship.GiftDislikeRarities.Add(ItemRarityOf(ctx, giftId));
                    }
                }

                // ---- 专属兵装（spweapons：名称/描述/稀有度 + 效果技能 + 技能强化 + 属性加成） ----
                if (spweaponsByChar.TryGetValue(rowId, out var spwRows))
                {
                    foreach (var s in spwRows)
                    {
                        var aug = new UniqueAugmentInfo
                        {
                            Id = s.GameId,
                            Name = ctx.Loc(s.NameKey),
                            Description = ctx.Loc(s.DescKey),
                            Rarity = ctx.RarityCode.TryGetValue(s.RarityId, out var rcode)
                                ? CodeToRarity(rcode) : Rarity.Unknown,
                            Craftable = true
                        };
                        if (skillRows.TryGetValue(s.EffectSkillRowId, out var effectSkill))
                        {
                            aug.EffectSkillName = ctx.Loc(effectSkill.NameKey);
                            aug.EffectSkillDescription = ctx.Loc(effectSkill.DescKey);
                        }
                        if (skillRows.TryGetValue(s.UpgradeSkillRowId, out var upgradeSkill))
                            aug.UpgradeSkillName = ctx.Loc(upgradeSkill.NameKey);
                        AddAugmentAttribute(aug, s.Attr1, s.Val1, s.Rand1);
                        AddAugmentAttribute(aug, s.Attr2, s.Val2, s.Rand2);
                        aug.TypeName = ShipTypesText(spweaponTypeShips, s.TypeId);
                        ship.UniqueAugments.Add(aug);
                    }
                    if (ship.UniqueAugments.Count > 0) ship.UniqueAugment = ship.UniqueAugments[0];
                }

                // ---- 科研（ship_blueprints：解锁条件 / 蓝图 / 任务链 / 开发与天运条数） ----
                if (blueprints.TryGetValue(rowId, out var bp))
                {
                    var research = new ResearchData
                    {
                        UnlockCondition = StripColorTags(bp.UnlockWord),
                        StrengthenItemName = ItemNameOf(ctx, bp.StrengthenItem),
                        UnlockTaskCount = CountJsonArray(bp.UnlockTask),
                        StrengthenEffectCount = CountJsonArray(bp.StrengthenEffect),
                        FateStrengthenCount = CountJsonArray(bp.FateStrengthen),
                        Price = bp.Price
                    };
                    research.StrengthenEffectText = research.StrengthenEffectCount > 0
                        ? $"数据库提供 {research.StrengthenEffectCount} 条开发效果（效果名称数据库未提供）" : "";
                    research.FateStrengthenText = research.FateStrengthenCount > 0
                        ? $"数据库提供 {research.FateStrengthenCount} 级天运拟合（名称数据库未提供）" : "";

                    // 解锁任务链：[[任务 id, 解锁秒数], ...] → 阶段解锁时间
                    if (!string.IsNullOrWhiteSpace(bp.UnlockTask))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(bp.UnlockTask);
                            int stage = 0;
                            foreach (var row in doc.RootElement.EnumerateArray())
                            {
                                stage++;
                                int seconds = row.ValueKind == System.Text.Json.JsonValueKind.Array && row.GetArrayLength() > 1
                                    ? (int)row[1].GetDouble() : 0;
                                research.UnlockTaskTimes.Add($"第 {stage} 阶段：{DescribeSeconds(seconds)}");
                            }
                        }
                        catch { /* 忽略非法 JSON */ }
                    }

                    // 获得道具
                    if (!string.IsNullOrWhiteSpace(bp.GainItems))
                    {
                        var names = new List<string>();
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(bp.GainItems);
                            foreach (var item in doc.RootElement.EnumerateArray())
                            {
                                int gid = (int)item.GetDouble();
                                string name = ItemNameOf(ctx, gid);
                                names.Add(string.IsNullOrEmpty(name) ? $"#{gid}" : name);
                            }
                        }
                        catch { /* 忽略 */ }
                        research.GainItemsText = string.Join("、", names);
                    }

                    // 改造技能 [[旧, 新]] → 「旧 → 新」
                    if (!string.IsNullOrWhiteSpace(bp.ChangeSkill))
                    {
                        var parts = new List<string>();
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(bp.ChangeSkill);
                            foreach (var pair in doc.RootElement.EnumerateArray())
                            {
                                if (pair.ValueKind != System.Text.Json.JsonValueKind.Array || pair.GetArrayLength() < 2) continue;
                                string from = SkillNameByRowId(ctx, skillRows, (long)pair[0].GetDouble());
                                string to = SkillNameByRowId(ctx, skillRows, (long)pair[1].GetDouble());
                                parts.Add($"{from} → {to}");
                            }
                        }
                        catch { /* 忽略 */ }
                        research.ChangeSkillText = string.Join("；", parts);
                    }

                    if (research.FateStrengthenCount > 0)
                        research.Fate = new FateSimulationData { Level = research.FateStrengthenCount };
                    ship.Research = research;
                }

                // ---- META 舰：突破 / 强化材料 / 技能任务 ----
                if (category == ShipCategory.META)
                {
                    var meta = new MetaData();

                    if (metaBreakouts.TryGetValue(rowId, out var breakouts))
                    {
                        foreach (var b in breakouts)
                        {
                            var items = new List<string>();
                            if (b.Mat1 > 0) items.Add(ItemWithCount(ctx, b.Mat1, b.Num1));
                            if (b.Mat2 > 0 && b.Num2 > 0) items.Add(ItemWithCount(ctx, b.Mat2, b.Num2));
                            meta.Breakouts.Add(new MetaBreakout
                            {
                                Level = b.Level,
                                ItemsText = string.Join("、", items),
                                Gold = b.Gold,
                                EffectText = StripColorTags(ctx.Loc(b.ViewKey).GetLocalized())
                            });
                        }
                    }

                    foreach (var r in metaRepairs)
                    {
                        string itemName = ItemNameOf(ctx, r.ItemId);
                        meta.RepairItems.Add(new MetaRepairItem
                        {
                            ItemName = string.IsNullOrEmpty(itemName) ? $"#{r.ItemId}" : itemName,
                            Count = r.Count,
                            Exp = r.Exp,
                            AttrText = MetaAttrText(r.Attr)
                        });
                    }

                    foreach (var skill in ship.Skills)
                    {
                        if (!metaSkillTasks.TryGetValue(skill.Id, out var tasks)) continue;
                        foreach (var t in tasks)
                        {
                            meta.SkillTasks.Add(new MetaSkillTaskRow
                            {
                                SkillName = skill.Name.GetLocalized(),
                                Level = t.Level,
                                NeedExp = t.NeedExp,
                                UnlockCount = t.UnlockCount,
                                LevelupCount = t.LevelupCount
                            });
                        }
                    }

                    if (meta.Breakouts.Count > 0 || meta.RepairItems.Count > 0 || meta.SkillTasks.Count > 0)
                        ship.Meta = meta;
                }

                ships.Add(ship);
            }

            return ships;
        }

        /// <summary>秒数 → 「立即 / X 天 X 小时」。</summary>
        private static string DescribeSeconds(int seconds)
        {
            if (seconds <= 0) return "立即";
            int days = seconds / 86400;
            int hours = seconds % 86400 / 3600;
            if (days > 0) return hours > 0 ? $"{days} 天 {hours} 小时" : $"{days} 天";
            int minutes = seconds % 3600 / 60;
            return hours > 0 ? (minutes > 0 ? $"{hours} 小时 {minutes} 分" : $"{hours} 小时") : $"{minutes} 分";
        }

        /// <summary>「材料名 ×数量」。</summary>
        private static string ItemWithCount(Ctx ctx, int itemGameId, int count)
        {
            string name = ItemNameOf(ctx, itemGameId);
            if (string.IsNullOrEmpty(name)) name = $"#{itemGameId}";
            return count > 1 ? $"{name} ×{count}" : name;
        }

        /// <summary>META 强化属性 JSON（如 ["air", 2]）→ 「航空 +2」。</summary>
        private static string MetaAttrText(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "";
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array || doc.RootElement.GetArrayLength() < 2)
                    return "";
                string code = doc.RootElement[0].GetString() ?? "";
                double value = doc.RootElement[1].GetDouble();
                return $"{StatCodeName(code)} +{value:0.##}";
            }
            catch { return ""; }
        }

        /// <summary>按技能行 id（DB skills.id）取技能名。</summary>
        private static string SkillNameByRowId(Ctx ctx, Dictionary<long, (string NameKey, string DescKey, int TypeId, int MaxLevel, long GameId)> skillRows, long rowId)
        {
            if (skillRows.TryGetValue(rowId, out var row))
                return ctx.Loc(row.NameKey).GetLocalized();
            return $"#{rowId}";
        }

        // ================= 映射辅助 =================

        private static ShipCategory CategoryFromCodex(string code) => code switch
        {
            "collab" => ShipCategory.Collab,
            "plan" => ShipCategory.Research,
            "meta" => ShipCategory.META,
            _ => ShipCategory.Normal
        };

        private static Faction CodeToFaction(string code) => (code ?? "").ToLowerInvariant() switch
        {
            "universal" => Faction.Universal,
            "eagle_union" => Faction.EagleUnion,
            "royal_navy" => Faction.RoyalNavy,
            "sakura_island" => Faction.SakuraEmpire,
            "iron_blood" => Faction.IronBlood,
            "dragon_empery" => Faction.DragonEmpery,
            "sardegna_empire" => Faction.Sardegna,
            "northern_parliament" => Faction.NorthernUnion,
            "iris_libre" => Faction.FreeFrench,
            "vichya_dominion" => Faction.Vichya,
            "kindom_of_tulipa" => Faction.Tulip,
            "liga_de_pedreria" => Faction.CrystalLeague,
            "meta_general" or "meta" => Faction.META,
            "meta_flame" => Faction.Meta_Flame,
            "meta_core" => Faction.Meta_Core,
            "meta_reason" => Faction.Meta_Reason,
            "meta_light" => Faction.Meta_Light,
            "meta_fire" => Faction.Meta_Fire,
            "tempesta" => Faction.Tempesta,
            "council" => Faction.Council,
            "x" => Faction.X,
            "siren" => Faction.Siren,
            "collab_nep" => Faction.Collab_Nep,
            "collab_hyperdimension" => Faction.Collab_Nep,      // 新版库：超次元游戏（Neptunia）阵营代码
            "collab_bilibili" => Faction.Collab_Bilibili,
            "collab_utawarerumono" => Faction.Collab_Utawarerumono,
            "collab_kizunaai" => Faction.Collab_KizunaAI,
            "collab_hololive" => Faction.Collab_Hololive,
            "collab_doaxvv" => Faction.Collab_DoAXVV,
            "collab_idolmaster" => Faction.Collab_Idolmaster,
            "collab_ssss" => Faction.Collab_SSSS,
            "collab_atelier" => Faction.Collab_Ryza,            // 新版库：炼金工房（Atelier）
            "collab_ryza" => Faction.Collab_Ryza,               // 旧版库代码，保留兼容
            "collab_senran" => Faction.Collab_Senran,
            "collab_toloveru" => Faction.Collab_Toloveru,
            "collab_brs" => Faction.Collab_BRS,
            "collab_danmachi" => Faction.Collab_Danmachi,
            "collab_yumia" => Faction.Collab_Yumia,
            "collab_dal" => Faction.Collab_DAL,
            "collab_nier" => Faction.Collab_NieR,
            "other_special" => Faction.Universal,               // 新版库：通用/特殊（布里等）
            "other" => Faction.Other,
            _ => Faction.Other
        };

        private static Rarity CodeToRarity(string code) => (code ?? "").ToUpperInvariant() switch
        {
            "N" => Rarity.N,
            "R" => Rarity.R,
            "SR" => Rarity.SR,
            "SSR" => Rarity.SSR,
            "UR" => Rarity.UR,
            "PRY" => Rarity.Decisive,
            "DR" => Rarity.Ultimate,
            _ => Rarity.Unknown
        };

        /// <summary>DB 舰种 base_code → 数据源 hull 代码（项目 ShipType 经 DataCodeMaps 转换）。</summary>
        /// <summary>
        /// 技能名降级候选：尾部罗马数字逐级降一级（II→I、III→II、IV→III），
        /// 最后去掉序号得到基础名（用于「强化版技能沿用未强化版图标」）。中文序号（一/二/三）同理。
        /// </summary>
        private static IEnumerable<string> EnumerateDowngradeNames(string name)
        {
            string text = name.Trim().Replace("Ⅱ", "II").Replace("Ⅲ", "III").Replace("Ⅰ", "I");
            var numeral = new System.Text.RegularExpressions.Regex(@"(I{1,3}|IV|V)$");
            var match = numeral.Match(text);
            if (match.Success)
            {
                string prefix = text.Substring(0, match.Index).TrimEnd();
                var levels = new List<string> { "I", "II", "III", "IV", "V" };
                int index = levels.IndexOf(match.Value.ToUpperInvariant());
                for (int i = index - 1; i >= 0; i--)
                {
                    yield return prefix + levels[i];
                    yield return prefix + " " + levels[i];
                }
                if (!string.IsNullOrEmpty(prefix)) yield return prefix;
            }
            else
            {
                var zh = new System.Text.RegularExpressions.Regex(@"(十[一二三四]?|[一二三四])$");
                var zhMatch = zh.Match(text);
                if (zhMatch.Success)
                {
                    string prefix = text.Substring(0, zhMatch.Index).TrimEnd();
                    string[] zhLevels = { "一", "二", "三", "四" };
                    int index = Array.IndexOf(zhLevels, zhMatch.Value);
                    if (index < 0) index = zhLevels.Length - 1;
                    for (int i = index - 1; i >= 0; i--)
                    {
                        yield return prefix + zhLevels[i];
                        yield return prefix + "·" + zhLevels[i];
                    }
                    if (!string.IsNullOrEmpty(prefix)) yield return prefix;
                }
            }
        }

        private static ShipType? BaseCodeToHull(string baseCode) => (baseCode ?? "").ToUpperInvariant() switch
        {
            "DD" => ShipType.DD,
            "DDG" => ShipType.DDG,
            "CL" => ShipType.CL,
            "CA" => ShipType.CA,
            "CB" => ShipType.CB,
            "BM" => ShipType.BM,
            "BC" => ShipType.BC,
            "BB" => ShipType.BB,
            "BBV" => ShipType.BBV,
            "CV" => ShipType.CV,
            "CVL" => ShipType.CVL,
            "AR" => ShipType.AR,
            "SS" => ShipType.SS,
            "SSV" => ShipType.SSV,
            "SAIL" => ShipType.Sail,
            "AP" => ShipType.AE,
            _ => null
        };

        /// <summary>base_code → hull 代码数值（舰队科技加成目标舰种用 HullToShipType 反查）。</summary>
        private static int BaseCodeToHullCode(string baseCode) => (baseCode ?? "").ToUpperInvariant() switch
        {
            "DD" => 1,
            "CL" => 2,
            "CA" => 3,
            "BC" => 4,
            "BB" => 5,
            "CVL" => 6,
            "CV" => 7,
            "SS" => 8,
            "BBV" => 10,
            "AR" => 12,
            "BM" => 13,
            "SSV" => 17,
            "CB" => 18,
            "AE" => 19,
            "DDG" => 20,
            _ => 0
        };

        private static GameSkinKind SkinKindFromCode(Ctx ctx, long skinTypeId) => ctx.SkinTypeCode.TryGetValue(skinTypeId, out var code)
            ? (code ?? "").ToLowerInvariant() switch
            {
                "default" => GameSkinKind.Default,
                "oath" => GameSkinKind.Oath,
                "retrofit" => GameSkinKind.Retrofit,
                "initial_art" => GameSkinKind.Original,
                "growth_plan" => GameSkinKind.FormAlt,
                _ => GameSkinKind.Normal
            }
            : GameSkinKind.Normal;

        private static GameSkillKind SkillKindFromCode(Ctx ctx, long typeId) => ctx.SkillTypeCode.TryGetValue(typeId, out var code)
            ? (code ?? "").ToLowerInvariant() switch
            {
                "attack" => GameSkillKind.Offensive,
                "defense" => GameSkillKind.Defensive,
                "auxiliary" => GameSkillKind.Support,
                _ => GameSkillKind.Equip
            }
            : GameSkillKind.Equip;

        private static string StatIdToKey(Ctx ctx, long statId)
        {
            if (!ctx.StatCode.TryGetValue(statId, out var code)) return "";
            return (code ?? "").ToLowerInvariant() switch
            {
                "durability" => "hp",
                "cannon" or "firepower" => "fp",
                "torpedo" => "trp",
                "antiaircraft" or "aa" or "anti_air" => "aa",
                "air" or "aviation" => "avi",
                "reload" => "rld",
                "hit" or "accuracy" => "hit",
                "dodge" or "evasion" => "eva",
                "antisub" or "asw" => "asw",
                "speed" => "spd",
                "luck" => "luck",
                _ => ""   // oxy_max / ammo 等项目模型无对应属性键
            };
        }

        private static TechTaskBonus MakeTechBonus(Ctx ctx, (int Pts, int StatId, int Value, int Target) row)
        {
            var bonus = new TechTaskBonus
            {
                Pts = row.Pts,
                Stat = StatIdToKey(ctx, row.StatId),
                Value = row.Value
            };
            if (row.Target > 0 && ctx.ShipTypeInfo.TryGetValue(row.Target, out var st))
            {
                int hull = BaseCodeToHullCode(st.BaseCode);
                if (hull > 0) bonus.Hulls.Add(hull);
            }
            return bonus;
        }

        /// <summary>
        /// 图鉴编号：`codex_series.prefix` + 系列内序号（补零到 `padding` 位）。
        /// 系列内序号：normal 直接用 codex_no；collab/plan/meta 的 codex_no 首位是系列代码（1/2/3），取后 4 位。
        /// </summary>
        private static string MakeWikiNumber(Ctx ctx, long codexId, string? codexNo)
        {
            if (string.IsNullOrWhiteSpace(codexNo)) return "";
            if (!int.TryParse(codexNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw) || raw <= 0)
                return "";

            string prefix = "NO.";
            string code = "";
            int padding = 3;
            if (ctx.CodexSeries.TryGetValue(codexId, out var series))
            {
                if (!string.IsNullOrEmpty(series.Prefix)) prefix = series.Prefix;
                code = (series.Code ?? "").ToLowerInvariant();
                if (series.Padding > 0) padding = series.Padding;
            }

            int index = code == "normal" || code.Length == 0 ? raw : raw % 10000;
            if (index <= 0) return "";
            return prefix + index.ToString("D" + padding, CultureInfo.InvariantCulture);
        }

        /// <summary>专属兵装适用范围：spweapon_types.ship_types 是**游戏 type 编码**（不是 ship_types.id）。</summary>
        private static string ShipTypesText(Dictionary<long, List<int>> map, long typeId)
        {
            if (typeId <= 0 || !map.TryGetValue(typeId, out var ids) || ids.Count == 0) return "";
            var names = new List<string>();
            foreach (int id in ids)
            {
                string name = GameShipTypeName(id);
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }
            return string.Join("、", names);
        }

        /// <summary>游戏 type 编码 → 舰种中文名（DB `ship_types.id` 与游戏 type 不是同一套编码）。</summary>
        private static string GameShipTypeName(int gameType) => gameType switch
        {
            1 => "驱逐",
            2 => "轻巡",
            3 => "重巡",
            4 => "战巡",
            5 => "战列",
            6 => "轻航",
            7 => "航母",
            8 => "潜艇",
            10 => "航战",
            11 => "轻航",
            12 => "维修",
            13 => "重炮",
            17 => "潜母",
            18 => "超巡",
            19 => "运输",
            20 => "导驱前排",
            21 => "导驱后排",
            22 => "风帆·前锋",
            23 => "风帆·主力",
            24 => "风帆·潜艇",
            _ => ""
        };

        /// <summary>属性代码（DB `stat_ids.code`）→ 中文名（DB i18n 未收录 stat_* 文案）。</summary>
        private static string StatCodeName(string code) => (code ?? "").Trim().ToLowerInvariant() switch
        {
            "durability" => "耐久",
            "cannon" or "firepower" => "炮击",
            "torpedo" => "雷击",
            "antiaircraft" or "aa" => "防空",
            "air" or "aviation" => "航空",
            "reload" => "装填",
            "hit" or "accuracy" => "命中",
            "dodge" or "evasion" => "机动",
            "antisub" or "asw" => "反潜",
            "speed" => "航速",
            "luck" => "幸运",
            "oxy_max" => "氧气",
            "ammo" => "弹药",
            _ => code ?? ""
        };

        /// <summary>道具名（items.game_id → name_key → i18n）；查不到返回空串。</summary>
        private static string ItemNameOf(Ctx ctx, int itemGameId)
        {
            if (itemGameId <= 0) return "";
            return ctx.ItemNameKey.TryGetValue(itemGameId, out var key) ? ctx.Text(key) : "";
        }

        /// <summary>道具图标相对路径（items.icon_path 解析，装载期已核实文件存在）；无图返回空串。</summary>
        private static string ItemIconRelOf(Ctx ctx, int itemGameId)
            => itemGameId > 0 && ctx.ItemIconRel.TryGetValue(itemGameId, out var rel) ? rel : "";

        /// <summary>道具品质（items.rarity）；查不到返回 0。</summary>
        private static int ItemRarityOf(Ctx ctx, int itemGameId)
            => itemGameId > 0 && ctx.ItemRarity.TryGetValue(itemGameId, out int rarity) ? rarity : 0;

        /// <summary>技能名（DB `skills.id`/`game_id` → i18n）；id 无效返回空对象。</summary>
        private static LocalizedString SkillNameOf(Ctx ctx, long skillId)
        {
            if (skillId <= 0) return new LocalizedString();
            return ctx.SkillNameRows.TryGetValue(skillId, out var key) ? ctx.Loc(key) : new LocalizedString();
        }

        /// <summary>
        /// `items.icon_path`（形如 `itemicon/Props/18001`）→ Assets 相对路径。
        /// 第二段决定目录：Equips 走 `icon/item/equips`，其余（Props）走 `icon/item/props`；末段是文件名。
        /// </summary>
        private static string ResolveItemIconRel(string? iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath)) return "";
            string[] parts = iconPath.Trim().Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "";
            string name = parts[^1].Trim();
            if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
            if (name.Length == 0) return "";
            bool equips = parts.Length >= 2 && parts[^2].Equals("Equips", StringComparison.OrdinalIgnoreCase);
            string rel = $"icon/item/{(equips ? "equips" : "props")}/{name}.png";
            return AssetExists(rel) ? rel : "icon/item/unkown.png";
        }

        /// <summary>改造节点图标：`transform_data_templates.icon`（如 hp_1 / skill_red）→ `icon/refit/{code}.png`。</summary>
        private static string RefitIconRel(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            string rel = "icon/refit/" + code.Trim() + ".png";
            return AssetExists(rel) ? rel : "";
        }

        /// <summary>
        /// 判断 Assets 相对路径的文件是否真的存在。
        /// 坑：XAML `ms-appx` 取不到图时不会报错、只显示空白，因此图标路径一律先核实。
        /// </summary>
        private static bool AssetExists(string relPath)
        {
            try
            {
                return File.Exists(Path.Combine(AppContext.BaseDirectory, "Assets",
                    relPath.Replace('/', Path.DirectorySeparatorChar)));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>兵装属性加成：属性代码（stat_ids.code）→ 中文属性名 + 数值。</summary>
        private static void AddAugmentAttribute(UniqueAugmentInfo aug, string? code, int value, int randomMax)
        {
            if (string.IsNullOrWhiteSpace(code) || (value == 0 && randomMax == 0)) return;
            string statCode = (code ?? "").Trim().ToLowerInvariant();
            string name = StatCodeName(statCode);
            aug.Attributes.Add(new AugmentAttribute
            {
                Code = statCode,
                Name = name,
                Value = value,
                RandomMax = randomMax
            });
        }

        /// <summary>
        /// 改造步骤 effect（JSON，如 [{"durability":45},{"equipment_proficiency_1":0.05}]）→ 中文文本。
        /// 装备效率类键（equipment_proficiency_N）换算为百分比。
        /// </summary>
        /// <summary>
        /// 改造步骤 effect（JSON，如 [{"durability":45},{"equipment_proficiency_1":0.05}]）→ 中文文本。
        /// 装备效率类键（equipment_proficiency_N）换算为百分比；skill_id 走 skills 表查技能名
        /// （<paramref name="fallbackSkillId"/> 是模板 skill_id，effect 自身没写时用它）。
        /// </summary>
        private static string EffectJsonToText(string json, long fallbackSkillId, Ctx ctx)
        {
            if (string.IsNullOrWhiteSpace(json)) return "";
            var parts = new List<string>();
            bool hasSkill = false;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return "";
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.Object) continue;
                    foreach (var prop in el.EnumerateObject())
                    {
                        string key = prop.Name.ToLowerInvariant();
                        double val = prop.Value.ValueKind == JsonValueKind.Number ? prop.Value.GetDouble() : 0;
                        if (key == "skill_id")
                        {
                            hasSkill = true;
                            string name = SkillNameOf(ctx, (long)val).GetLocalized();
                            if (!string.IsNullOrEmpty(name)) parts.Add($"习得技能【{name}】");
                            continue;
                        }
                        if (key.StartsWith("equipment_proficiency", StringComparison.Ordinal))
                        {
                            parts.Add($"装备效率+{val * 100:0.#}%");
                            continue;
                        }
                        string statName = StatCodeName(key);
                        parts.Add(val == Math.Floor(val) ? $"{statName}+{(int)val}" : $"{statName}+{val:0.##}");
                    }
                }
            }
            catch { /* 忽略无法解析的 effect */ }

            if (!hasSkill && fallbackSkillId > 0)
            {
                string name = SkillNameOf(ctx, fallbackSkillId).GetLocalized();
                if (!string.IsNullOrEmpty(name)) parts.Add($"习得技能【{name}】");
            }
            return string.Join("、", parts);
        }

        /// <summary>去掉 DB 文案里的富文本标记（&lt;color=#xxx&gt;…&lt;/color&gt;）。</summary>
        private static string StripColorTags(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return Regex.Replace(text, "<[^>]+>", "").Trim();
        }

        /// <summary>JSON 数组元素个数（解析失败按 0）。</summary>
        /// <summary>商店渠道代码 → 展示名。</summary>
        private static string ShopWarpName(string warp) => (warp ?? "").Trim().ToLowerInvariant() switch
        {
            "medal" => "勋章商店",
            "supplies" => "军需商店",
            "fragment" => "核心数据商店",
            "guild" => "舰队商店",
            "meta" => "META 兑换",
            "prototype" => "实验室兑换",
            _ => ""
        };

        /// <summary>主线关卡名（如 map_7_2_h）→ 关卡号；无法解析返回 0。</summary>
        private static int StageNumber(string nameKey)
        {
            if (string.IsNullOrEmpty(nameKey)) return 0;
            foreach (var token in nameKey.Split('_').Reverse())
            {
                if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
                    return n;
            }
            return 0;
        }

        /// <summary>map_drops.source_type → 展示来源（主线 / 活动 / 档案 / 彩蛋）。</summary>
        private static string DropSourceKind(string source, string typeCode)
        {
            switch ((source ?? "").Trim().ToLowerInvariant())
            {
                case "main": return "主线";
                case "archive": return "档案";
                case "activity":
                case "rerun": return "活动";
                case "egg":
                case "easter_egg": return "彩蛋";
            }
            return (typeCode ?? "").Equals("archive", StringComparison.OrdinalIgnoreCase) ? "档案" : "活动";
        }

        /// <summary>是否困难图：图类型 hard 或图名以 `_h` 结尾（困难图在 maps 里没有 i18n 文案）。</summary>
        private static bool IsHardMap(string typeCode, string nameKey)
            => (typeCode ?? "").Equals("hard", StringComparison.OrdinalIgnoreCase)
               || (nameKey ?? "").EndsWith("_h", StringComparison.OrdinalIgnoreCase);

        /// <summary>关卡显示名：i18n 图名优先；困难图无文案时按「章-关」还原（map_7_2_h → 7-2）。</summary>
        private static string MapDisplayName(Ctx ctx, string nameKey)
        {
            if (string.IsNullOrEmpty(nameKey)) return "—";
            string text = ctx.Loc(nameKey).GetLocalized();
            if (!string.IsNullOrEmpty(text)) return text;

            string[] parts = nameKey.Split('_');
            if (parts.Length >= 3
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int chapter)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int stage))
                return $"{chapter}-{stage}";
            return nameKey;
        }

        /// <summary>活动普通关卡（A1/A2/C3）→ 归一化键；SP/EX 或无法解析返回 null。</summary>
        private static string? NormalStageKey(string nameKey)
        {
            if (string.IsNullOrEmpty(nameKey)) return null;
            var last = nameKey.Split('_').LastOrDefault() ?? "";
            if (last.Length < 2) return null;
            char letter = last[0];
            if (letter is < 'a' or > 'd') return null;
            string digits = last[1..];
            if (!int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return null;
            if (n is < 1 or > 3) return null;
            return letter.ToString().ToUpperInvariant() + n;
        }

        /// <summary>活动 SP/EX 关卡 → 「SP1」「EX2」；其他返回 null。</summary>
        private static string? ExtraStageKey(string nameKey)
        {
            if (string.IsNullOrEmpty(nameKey)) return null;
            var last = nameKey.Split('_').LastOrDefault() ?? "";
            string upper = last.ToUpperInvariant();
            if (upper.StartsWith("SP")) return upper;
            if (upper.StartsWith("EX")) return upper;
            return null;
        }

        private static int CountJsonArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return 0;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : 0;
            }
            catch { return 0; }
        }

        /// <summary>map_drop 的 cost_key（如 {"chapterid": 1303}）→ 章节/掉落点。</summary>
        private static void AddMapDropFromCost(AcquisitionData acq, string costKey)
        {
            if (string.IsNullOrWhiteSpace(costKey)) return;
            try
            {
                using var doc = JsonDocument.Parse(costKey);
                if (!doc.RootElement.TryGetProperty("chapterid", out var el) || el.ValueKind != JsonValueKind.Number) return;
                if (!el.TryGetInt32(out int chapterId) || chapterId <= 0) return;
                int chapter = chapterId / 100;
                int point = chapterId % 100;
                var drop = acq.MapDrops.FirstOrDefault(c => c.Chapter == chapter);
                if (drop == null)
                {
                    drop = new ChapterDrop { Chapter = chapter };
                    acq.MapDrops.Add(drop);
                }
                if (drop.Points.All(p => p.Map != point))
                    drop.Points.Add(new MapDropPoint { Map = point, IsBoss = false });
            }
            catch { /* 忽略无法解析的 cost_key */ }
        }

        /// <summary>invite 的 cost_key（可含 projectName=light/heavy/special）→ 建造池标记；返回是否命中建造池。</summary>
        private static bool AddPoolFromCost(AcquisitionData acq, string costKey)
        {
            if (string.IsNullOrWhiteSpace(costKey)) return false;
            try
            {
                using var doc = JsonDocument.Parse(costKey);
                if (!doc.RootElement.TryGetProperty("projectName", out var el) || el.ValueKind != JsonValueKind.String) return false;
                switch ((el.GetString() ?? "").ToLowerInvariant())
                {
                    case "light": acq.Light = true; return true;
                    case "heavy": acq.Heavy = true; return true;
                    case "special": acq.Special = true; return true;
                }
            }
            catch { /* 忽略 */ }
            return false;
        }

        /// <summary>cost_key 中某个字符串属性（如 warp / shop）的取值集合。</summary>
        /// <summary>从 JSON 对象取字符串属性（保留原文大小写）；缺键/解析失败返回 null。</summary>
        private static string? JsonStringProperty(string json, string property)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                if (doc.RootElement.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String)
                    return el.GetString();
            }
            catch { /* 忽略无法解析的 cost_key */ }
            return null;
        }

        private static List<string> ParseStringValues(string json, string property)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(json)) return list;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(property, out var el) && el.ValueKind == JsonValueKind.String)
                {
                    string? v = el.GetString();
                    if (!string.IsNullOrEmpty(v)) list.Add(v.ToLowerInvariant());
                }
            }
            catch { /* 忽略 */ }
            return list;
        }

        /// <summary>商店 warp 代码 → UnlockType。</summary>
        private static UnlockType? ShopWarpToUnlock(string? warp) => (warp ?? "").ToLowerInvariant() switch
        {
            "medal" => UnlockType.MedalShop,
            "supplies" or "guild" => UnlockType.GuildShop,
            "fragment" => UnlockType.PrototypeShop,
            "core" => UnlockType.CoreDataShop,
            "meta" => UnlockType.MetaShop,
            _ => null
        };

        private static void AddUnlockType(AcquisitionData acq, UnlockType? type)
        {
            if (type == null || acq.UnlockTypes.Contains(type.Value)) return;
            acq.UnlockTypes.Add(type.Value);
        }

        private static List<int> ParseIntArray(string json)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(json)) return list;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return list;
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out int v)) list.Add(v);
                    else if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out int sv)) list.Add(sv);
                    else list.Add(0);
                }
            }
            catch { /* 忽略 */ }
            return list;
        }
    }
}
