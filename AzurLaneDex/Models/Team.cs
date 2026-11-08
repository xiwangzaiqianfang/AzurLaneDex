using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace AzurLaneDex.Models
{
    /// <summary>
    /// 单支舰队（水面 / 水下 / 支援通用）：定长槽位，<see cref="ShipIds"/> 中 0 表示空槽。
    /// 水面舰队 6 槽（0-2 前排、3-5 后排）；水下与支援 3 槽。
    /// 配装与改造形态按**舰队**分别保存。
    /// </summary>
    public class Fleet
    {
        /// <summary>水面舰队槽位数（0-2 前排、3-5 后排）。</summary>
        public const int SurfaceSlots = 6;

        /// <summary>水下 / 支援舰队槽位数。</summary>
        public const int SmallSlots = 3;

        /// <summary>舰队名称（如「一队」「水下舰队」「支援舰队」）。</summary>
        [JsonPropertyName("name")] public string Name { get; set; } = "";

        /// <summary>槽位舰船 id（定长；0 = 空槽）。</summary>
        [JsonPropertyName("ship_ids")] public List<int> ShipIds { get; set; } = new();

        /// <summary>各槽位配装（槽位内「装备位序号 → 装备 id」）；与 <see cref="ShipIds"/> 等长。</summary>
        [JsonPropertyName("equips")] public List<Dictionary<int, int>?>? Equips { get; set; }

        /// <summary>各槽位改造形态（true=改造后立绘）；与 <see cref="ShipIds"/> 等长。</summary>
        [JsonPropertyName("retrofits")] public List<bool>? Retrofits { get; set; }

        /// <summary>各槽位专属兵装（spweapon game_id；0 = 未装备）；与 <see cref="ShipIds"/> 等长。</summary>
        [JsonPropertyName("augments")] public List<int>? Augments { get; set; }

        public Fleet() { }

        public Fleet(string name, int slotCount)
        {
            Name = name;
            Normalize(slotCount);
        }

        /// <summary>把三个列表补齐 / 裁剪到指定槽位数。</summary>
        public void Normalize(int slotCount)
        {
            ShipIds ??= new List<int>();
            while (ShipIds.Count < slotCount) ShipIds.Add(0);
            if (ShipIds.Count > slotCount) ShipIds = ShipIds.Take(slotCount).ToList();

            Equips ??= new List<Dictionary<int, int>?>();
            while (Equips.Count < slotCount) Equips.Add(null);
            if (Equips.Count > slotCount) Equips = Equips.Take(slotCount).ToList();

            Retrofits ??= new List<bool>();
            while (Retrofits.Count < slotCount) Retrofits.Add(false);
            if (Retrofits.Count > slotCount) Retrofits = Retrofits.Take(slotCount).ToList();

            Augments ??= new List<int>();
            while (Augments.Count < slotCount) Augments.Add(0);
            if (Augments.Count > slotCount) Augments = Augments.Take(slotCount).ToList();
        }

        /// <summary>本舰队是否没有任何舰船。</summary>
        public bool IsEmpty => ShipIds.All(id => id <= 0);

        /// <summary>深拷贝（用于分享时冻结当前编辑内容）。</summary>
        public Fleet Clone()
        {
            Normalize(ShipIds.Count);
            return new Fleet
            {
                Name = Name,
                ShipIds = new List<int>(ShipIds),
                Retrofits = new List<bool>(Retrofits ?? new List<bool>()),
                Augments = new List<int>(Augments ?? new List<int>()),
                Equips = (Equips ?? new List<Dictionary<int, int>?>())
                    .Select(d => d == null ? null : new Dictionary<int, int>(d))
                    .ToList()
            };
        }

        /// <summary>指定槽位的配装字典（不存在则创建）。</summary>
        public Dictionary<int, int> EquipMap(int slotIndex)
        {
            Normalize(ShipIds.Count);
            var map = Equips![slotIndex];
            if (map == null)
            {
                map = new Dictionary<int, int>();
                Equips[slotIndex] = map;
            }
            return map;
        }
    }

    /// <summary>支援舰队类型。</summary>
    public enum SupportFleetKind
    {
        /// <summary>航母支援（CV / CVL）。</summary>
        Carrier = 0,
        /// <summary>潜艇支援（SS / SSV）。</summary>
        Submarine = 1
    }

    /// <summary>
    /// 配队（用户创建的作战编队组）：1~3 支水面舰队 + 1 支水下舰队 + 1 支支援舰队。
    /// 全局功能，与账户绑定存储（users/{account}/teams.json），不进入 ShipState。
    /// </summary>
    public class Team
    {
        /// <summary>水面舰队数量上限。</summary>
        public const int MaxSurfaceFleets = 3;

        [JsonPropertyName("id")] public int Id { get; set; }

        [JsonPropertyName("name")] public string Name { get; set; } = "";

        [JsonPropertyName("description")] public string Description { get; set; } = "";

        /// <summary>水面舰队（1~3 支，默认 1 支）。</summary>
        [JsonPropertyName("surface")] public List<Fleet> Surface { get; set; } = new();

        /// <summary>水下舰队（固定 3 槽，只允许 SS / SSV）。</summary>
        [JsonPropertyName("submarine")] public Fleet Submarine { get; set; } = new("水下舰队", Fleet.SmallSlots);

        /// <summary>支援舰队（3 槽，类型由 <see cref="SupportKind"/> 决定）。</summary>
        [JsonPropertyName("support")] public Fleet Support { get; set; } = new("支援舰队", Fleet.SmallSlots);

        /// <summary>支援舰队类型（carrier / submarine）。</summary>
        [JsonPropertyName("support_kind")] public string SupportKind { get; set; } = "carrier";

        [JsonIgnore]
        public SupportFleetKind SupportKindValue =>
            SupportKind == "submarine" ? SupportFleetKind.Submarine : SupportFleetKind.Carrier;

        /// <summary>把结构补齐（至少 1 支水面舰队、各舰队槽位数正确）。</summary>
        public void Normalize()
        {
            Surface ??= new List<Fleet>();
            if (Surface.Count == 0) Surface.Add(new Fleet("一队", Fleet.SurfaceSlots));
            if (Surface.Count > MaxSurfaceFleets) Surface = Surface.Take(MaxSurfaceFleets).ToList();
            foreach (var fleet in Surface) fleet.Normalize(Fleet.SurfaceSlots);

            Submarine ??= new Fleet("水下舰队", Fleet.SmallSlots);
            Submarine.Normalize(Fleet.SmallSlots);

            Support ??= new Fleet("支援舰队", Fleet.SmallSlots);
            Support.Normalize(Fleet.SmallSlots);

            if (string.IsNullOrEmpty(SupportKind)) SupportKind = "carrier";
        }

        /// <summary>本次配队里的全部舰船 id（水面 + 水下 + 支援），用于查重。</summary>
        public IEnumerable<int> AllShipIds =>
            Surface.SelectMany(f => f.ShipIds)
                   .Concat(Submarine?.ShipIds ?? new List<int>())
                   .Concat(Support?.ShipIds ?? new List<int>())
                   .Where(id => id > 0);

        /// <summary>全部舰队（水面 + 水下 + 支援），用于分享与图片渲染。</summary>
        public IEnumerable<Fleet> AllFleets =>
            Surface.Concat(new[] { Submarine, Support }.Where(f => f != null));

        /// <summary>深拷贝（分享用）。</summary>
        public Team Clone() => new()
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Surface = Surface.Select(f => f.Clone()).ToList(),
            Submarine = Submarine?.Clone() ?? new Fleet("水下舰队", Fleet.SmallSlots),
            Support = Support?.Clone() ?? new Fleet("支援舰队", Fleet.SmallSlots),
            SupportKind = SupportKind
        };
    }

    /// <summary>账户配队列表容器（序列化用）。</summary>
    public class TeamList
    {
        [JsonPropertyName("account")] public string AccountName { get; set; } = "";
        [JsonPropertyName("teams")] public List<Team> Teams { get; set; } = new();
    }
}
