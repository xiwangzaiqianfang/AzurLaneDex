using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// 装备目录服务：从静态数据库（ships_static.db 的 equipments 表 + i18n 名称）惰性装载装备，
    /// 供配队「配装编辑器」与分享文本使用；会话内缓存（只读，库更新后由重启/重新载入刷新）。
    /// 图标：Assets/icon/item/equips/{game_id}.png；稀有度：rarities 对应 T1..T6（用于图标底色）。
    /// </summary>
    public static class EquipmentCatalog
    {
        /// <summary>装备条目。</summary>
        public sealed class Entry
        {
            /// <summary>装备 id（= 数据库 game_id，分享码与配装数据用它）。</summary>
            public int Id { get; set; }
            /// <summary>名称（多语言：zh-Hans / zh-Hant / en / ja，取自 i18n）。</summary>
            public LocalizedString Name { get; set; } = new();
            /// <summary>装备类型（equip_types.id：1 驱逐炮 … 99 技能占位）。</summary>
            public int Type { get; set; }
            /// <summary>稀有度 T 级（1..6），用于图标底色。</summary>
            public int Rarity { get; set; }
            /// <summary>
            /// 图标 id：取自数据库 `icon_path`（形如 `equipicon/23000` → 23000），
            /// 图标文件为 `Assets/icon/item/equips/{IconId}.png`。同型武器会共用同一图标（正常）。
            /// icon_path 缺失时回退为 <see cref="Id"/>。
            /// </summary>
            public int IconId { get; set; }

            // 武器参数（伤害/射角/散布/射程/装填/鱼雷）：null=数据库未提供。
            // 取值来自可选的 `weapons` 表（equipments.weapon_id 关联）；当前库里没有该表，故恒为 null。
            /// <summary>单发伤害。</summary>
            public int? Damage { get; set; }
            /// <summary>射角（度）。</summary>
            public int? Angle { get; set; }
            /// <summary>散布范围。</summary>
            public int? Scatter { get; set; }
            /// <summary>射程。</summary>
            public int? Range { get; set; }
            /// <summary>装填时间（秒）。</summary>
            public double? Reload { get; set; }
            /// <summary>鱼雷（武器）伤害。</summary>
            public int? Torpedo { get; set; }

            /// <summary>
            /// 是否为占位/展示类装备（不应出现在选择器里）：类型 99（技能/航空攻击展示）、
            /// 类型为空的「装备模板」、名称以「序章」「默认」开头的教程与默认武器。
            /// 仍保留在目录中，供已保存配队显示名称与图标。
            /// </summary>
            public bool IsPlaceholder { get; set; }

            /// <summary>按当前界面语言取名称（缺失回退 #id）。</summary>
            public string DisplayName
            {
                get
                {
                    string text = Name.GetLocalized();
                    return string.IsNullOrEmpty(text) ? $"#{Id}" : text;
                }
            }
        }

        private static List<Entry>? _cache;
        private static Dictionary<int, Entry>? _byId;
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static bool _loadFailed;

        /// <summary>已加载的装备列表（未加载返回空）。</summary>
        public static IReadOnlyList<Entry> All => (IReadOnlyList<Entry>?)_cache ?? Array.Empty<Entry>();

        /// <summary>确保目录已加载（失败静默，返回空目录）。</summary>
        public static async Task EnsureLoadedAsync()
        {
            if (_cache != null || _loadFailed) return;
            await Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_cache != null || _loadFailed) return;

                string dbPath = SqliteDataSourceLoader.ResolveDbPath();
                if (!File.Exists(dbPath))
                {
                    _loadFailed = true;
                    return;
                }

                var list = await Task.Run(() => LoadFromDatabase(dbPath)).ConfigureAwait(false);
                _cache = list;
                _byId = list.ToDictionary(x => x.Id);
            }
            catch (Exception ex)
            {
                LogService.Warning("装备目录装载失败：" + ex.Message, nameof(EquipmentCatalog));
                _loadFailed = true;
            }
            finally
            {
                Gate.Release();
            }
        }

        /// <summary>从数据库读取装备（game_id / 类型 / 稀有度 / 三语名称 + 可选的武器参数）。</summary>
        private static List<Entry> LoadFromDatabase(string dbPath)
        {
            var list = new List<Entry>();
            using var db = SqliteDatabase.OpenReadOnly(dbPath);

            // weapons 是「还没有」的联表：存在就联、缺列就给 NULL，保证补表后零改代码即生效
            var equipColumns = InspectColumns(db, "equipments", "weapon_id");
            var weaponColumns = InspectColumns(db, "weapons", "damage", "angle", "scatter", "range", "reload", "torpedo");
            bool hasWeapons = weaponColumns.Count > 0 && equipColumns.Contains("weapon_id");
            string W(string column) => hasWeapons && weaponColumns.Contains(column) ? "w." + column : "NULL";

            using var q = db.Query(
                "SELECT e.game_id, e.equip_type_id, e.rarity_id, e.icon_path, " +
                "  (SELECT value FROM i18n WHERE key = e.name_key AND lang = 'zh'), " +
                "  (SELECT value FROM i18n WHERE key = e.name_key AND lang = 'en'), " +
                "  (SELECT value FROM i18n WHERE key = e.name_key AND lang = 'jp'), " +
                $"  {W("damage")}, {W("angle")}, {W("scatter")}, {W("range")}, {W("reload")}, {W("torpedo")} " +
                "FROM equipments e" + (hasWeapons ? " LEFT JOIN weapons w ON w.id = e.weapon_id" : ""));

            while (q.Read())
            {
                int id = q.Int(0);
                if (id <= 0) continue;

                var name = new LocalizedString();
                string? zh = q.Text(4);
                string? en = q.Text(5);
                string? jp = q.Text(6);
                if (!string.IsNullOrEmpty(zh))
                {
                    name["zh-Hans"] = zh;
                    name["zh-Hant"] = zh;
                }
                if (!string.IsNullOrEmpty(en)) name["en"] = en;
                if (!string.IsNullOrEmpty(jp)) name["ja"] = jp;

                int iconId = ParseIconId(q.Text(3), id);
                int type = q.Int(1);
                list.Add(new Entry
                {
                    Id = id,
                    Name = name,
                    Type = type,
                    Rarity = q.Int(2),
                    IconId = iconId,
                    Damage = NullableInt(q, 7),
                    Angle = NullableInt(q, 8),
                    Scatter = NullableInt(q, 9),
                    Range = NullableInt(q, 10),
                    Reload = q.IsNull(11) ? null : q.Double(11),
                    Torpedo = NullableInt(q, 12),
                    IsPlaceholder = IsPlaceholderEntry(type, zh)
                });
            }

            // 按编号升序（调用方仍可自行排序）
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        /// <summary>取表的列名集合（表不存在或缺少全部目标列时返回空集合）。</summary>
        private static HashSet<string> InspectColumns(SqliteDatabase db, string table, params string[] wanted)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                bool exists = false;
                using (var probe = db.Query("SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = ?", table))
                    exists = probe.Read();
                if (!exists) return result;

                using var info = db.Query($"PRAGMA table_info(\"{table}\")");
                while (info.Read())
                {
                    string? column = info.Text(1);
                    if (!string.IsNullOrEmpty(column) && wanted.Any(w => w.Equals(column, StringComparison.OrdinalIgnoreCase)))
                        result.Add(column);
                }
            }
            catch (Exception ex)
            {
                LogService.Warning($"{table} 表探测失败：{ex.Message}", nameof(EquipmentCatalog));
                result.Clear();
            }
            return result;
        }

        private static int? NullableInt(SqliteQuery q, int column) => q.IsNull(column) ? null : q.Int(column);

        /// <summary>
        /// 占位/展示类装备判定：类型 99（技能/航空攻击展示）、类型为空或无类型的「装备模板」、
        /// 名称以「序章」「默认」开头的教程与默认武器（共约 138 件）。
        /// </summary>
        private static bool IsPlaceholderEntry(int type, string? zhName)
        {
            if (type == 99 || type <= 0) return true;
            if (string.IsNullOrEmpty(zhName)) return false;
            return zhName.StartsWith("序章", StringComparison.Ordinal) ||
                   zhName.StartsWith("默认", StringComparison.Ordinal) ||
                   zhName.StartsWith("装备模板", StringComparison.Ordinal);
        }

        /// <summary>从 icon_path（`equipicon/23000`）解析图标 id；解析失败回退 game_id。</summary>
        private static int ParseIconId(string? iconPath, int fallbackId)
        {
            if (string.IsNullOrWhiteSpace(iconPath)) return fallbackId;
            int end = iconPath.Length - 1;
            while (end >= 0 && !char.IsDigit(iconPath[end])) end--;
            if (end < 0) return fallbackId;

            int start = end;
            while (start >= 0 && char.IsDigit(iconPath[start])) start--;
            string digits = iconPath.Substring(start + 1, end - start);
            return int.TryParse(digits, out int value) && value > 0 ? value : fallbackId;
        }

        /// <summary>同步取装备名（已加载时可用，供 UI 刷新路径避免 await）。</summary>
        public static string NameOf(int id)
            => _byId != null && _byId.TryGetValue(id, out var e) ? e.DisplayName : "";

        /// <summary>同步取图标 id（已加载时可用；未加载返回 0，调用方可回退到装备 id）。</summary>
        public static int IconIdOf(int id)
            => _byId != null && _byId.TryGetValue(id, out var e) ? e.IconId : 0;

        /// <summary>按 id 查条目（未加载或不存在返回 null）。</summary>
        public static Entry? Find(int id)
            => _byId != null && _byId.TryGetValue(id, out var e) ? e : null;
    }
}
