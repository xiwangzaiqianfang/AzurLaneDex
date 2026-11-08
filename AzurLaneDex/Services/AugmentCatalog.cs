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
    /// 特殊兵装目录（DB `spweapons` 中 1010100~1080220 的通用特殊兵装）：
    /// 舰种适用范围来自 `spweapon_types.ship_types`（DB 舰种 id 数组），
    /// 图标 <c>Assets/icon/item/spweapon/{Id}.png</c>，名称取自 i18n。
    /// </summary>
    public static class AugmentCatalog
    {
        /// <summary>特殊兵装 id 区间（含）。</summary>
        public const int MinSpecialId = 1010100;
        public const int MaxSpecialId = 1080220;

        /// <summary>一条特殊兵装。</summary>
        public sealed class SpecialAugment
        {
            public int Id { get; set; }
            public LocalizedString Name { get; set; } = new();
            public int Rarity { get; set; }
            /// <summary>图标 id（取自 icon_path，如 spweaponicon/1010100 → 1010100）。</summary>
            public int IconId { get; set; }
            /// <summary>适用舰种（DB ship_types.id 集合）。</summary>
            public HashSet<int> ShipTypes { get; set; } = new();
        }

        private static List<SpecialAugment>? _cache;
        private static Dictionary<int, int>? _iconMap;   // 兵装 id → 图标 id（含非特殊区间）
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static bool _loadFailed;

        /// <summary>已加载的特殊兵装（未加载返回空）。</summary>
        public static IReadOnlyList<SpecialAugment> All => (IReadOnlyList<SpecialAugment>?)_cache ?? Array.Empty<SpecialAugment>();

        /// <summary>指定舰种可用的特殊兵装。</summary>
        public static IEnumerable<SpecialAugment> ForShipType(int shipTypeId)
            => shipTypeId <= 0 ? Enumerable.Empty<SpecialAugment>()
                               : All.Where(a => a.ShipTypes.Contains(shipTypeId));

        /// <summary>按 id 查特殊兵装名称（不适用/未加载返回空串）。</summary>
        public static string NameOf(int id)
            => _cache?.FirstOrDefault(a => a.Id == id)?.Name.GetLocalized() ?? "";

        /// <summary>按兵装 id 取图标 id（来自 icon_path；未知时回退 id）。</summary>
        public static int IconIdOf(int id)
            => _iconMap != null && _iconMap.TryGetValue(id, out var iconId) && iconId > 0 ? iconId : id;

        /// <summary>确保已装载（失败静默）。</summary>
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

                (_cache, _iconMap) = await Task.Run(() => LoadFromDatabase(dbPath)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogService.Warning("特殊兵装目录装载失败：" + ex.Message, nameof(AugmentCatalog));
                _loadFailed = true;
            }
            finally
            {
                Gate.Release();
            }
        }

        private static (List<SpecialAugment>, Dictionary<int, int>) LoadFromDatabase(string dbPath)
        {
            var list = new List<SpecialAugment>();
            var icons = new Dictionary<int, int>();
            using var db = SqliteDatabase.OpenReadOnly(dbPath);
            using var q = db.Query(
                "SELECT w.game_id, w.rarity_id, t.ship_types, w.icon_path, " +
                "  (SELECT value FROM i18n WHERE key = w.name_key AND lang = 'zh'), " +
                "  (SELECT value FROM i18n WHERE key = w.name_key AND lang = 'en') " +
                "FROM spweapons w LEFT JOIN spweapon_types t ON t.id = w.spweapon_type_id");

            while (q.Read())
            {
                string idText = q.Text(0) ?? "";
                if (!int.TryParse(idText, out int id) || id <= 0) continue;

                int iconId = ParseIconId(q.Text(3), id);
                icons[id] = iconId;

                var name = new LocalizedString();
                string? zh = q.Text(4);
                string? en = q.Text(5);
                if (!string.IsNullOrEmpty(zh))
                {
                    name["zh-Hans"] = zh;
                    name["zh-Hant"] = zh;
                }
                if (!string.IsNullOrEmpty(en)) name["en"] = en;

                var types = new HashSet<int>();
                string? typesJson = q.Text(2);
                if (!string.IsNullOrEmpty(typesJson))
                {
                    try
                    {
                        foreach (var t in System.Text.Json.JsonSerializer.Deserialize<List<int>>(typesJson) ?? new List<int>())
                            types.Add(t);
                    }
                    catch { /* 忽略非法 JSON */ }
                }

                if (id < MinSpecialId || id > MaxSpecialId) continue;   // 只收特殊兵装区间
                list.Add(new SpecialAugment
                {
                    Id = id,
                    Name = name,
                    Rarity = q.Int(1),
                    IconId = iconId,
                    ShipTypes = types
                });
            }

            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return (list, icons);
        }

        /// <summary>从 icon_path（`spweaponicon/1010100`）解析图标 id；失败回退 game_id。</summary>
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
    }
}
