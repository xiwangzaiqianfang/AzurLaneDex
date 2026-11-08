using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using AzurLaneDex.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AzurLaneDex.Views
{
    /// <summary>
    /// 配队舰船选择弹窗：网格展示舰船（头像+名称+稀有度/舰种）。
    /// 过滤条件 = 调用方给定的舰种集合 + 可选谓词（用于区分 DDG 先锋/主力等需要 DB 舰种 id 的场合）。
    /// 性能：条目（含头像 URI）构造时一次性生成；搜索直接过滤。
    /// </summary>
    public sealed partial class TeamShipPickerDialog : ContentDialog
    {
        private static string L(string key, string fallback)
        {
            string text = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse().GetString(key);
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        private readonly IReadOnlyCollection<ShipViewModel> _allShips;
        private readonly HashSet<ShipType> _allowedTypes;
        private readonly Func<ShipViewModel, bool>? _extraFilter;

        private readonly List<PickerItem> _items = new();
        private bool _ascending = true;
        private readonly List<PickerItem> _current = new();

        /// <summary>用户选中的舰船（关闭后读取；null=未选择）。</summary>
        public ShipViewModel? SelectedShip { get; private set; }

        /// <summary>网格卡片包装（含内部筛选/排序键）。</summary>
        public sealed class PickerItem
        {
            public string AvatarUri { get; set; } = "";
            public string BgUri { get; set; } = "";
            public string Display { get; set; } = "";
            public string InfoLine { get; set; } = "";
            public string Rarity { get; set; } = "";
            public string IdText { get; set; } = "";
            public ShipViewModel Vm { get; set; } = null!;

            internal bool Allowed { get; set; }
            internal ShipCategory Category { get; set; }
            internal Faction Faction { get; set; }
            internal ShipType ShipType { get; set; }
            internal Rarity RarityEnum { get; set; }
            internal bool Owned { get; set; }
            internal int GameOrder { get; set; }
            internal int Breakthrough { get; set; }
            internal bool Oath { get; set; }
            internal bool Level120 { get; set; }
            internal string SearchText { get; set; } = "";
        }

        /// <summary>可选项包装。</summary>
        public sealed class ComboEntry
        {
            public string Text { get; set; } = "";
            public object? Tag { get; set; }
            public override string ToString() => Text;
        }

        public TeamShipPickerDialog(IEnumerable<ShipViewModel> ships,
                                    IReadOnlyCollection<ShipType> allowedTypes,
                                    Func<ShipViewModel, bool>? extraFilter = null)
        {
            this.InitializeComponent();
            _allShips = ships.ToList();
            _allowedTypes = allowedTypes.Count > 0
                ? new HashSet<ShipType>(allowedTypes)
                : new HashSet<ShipType>(Enum.GetValues(typeof(ShipType)).Cast<ShipType>());
            _extraFilter = extraFilter;

            this.Title = L("TeamPicker_Title", "选择舰船");
            this.PrimaryButtonText = L("TeamPicker_Select", "选择");
            this.CloseButtonText = L("TeamPage_Cancel", "取消");
            SearchBox.PlaceholderText = L("TeamPicker_Search", "搜索舰船名称/编号");
            EmptyText.Text = L("TeamPicker_Empty", "没有符合条件的舰船");

            BuildItems();
            BuildFilters();
            ApplyFilters();
        }

        private void BuildItems()
        {
            foreach (var s in _allShips)
            {
                bool allowed = _allowedTypes.Contains(s.ShipTypeEnum) &&
                               (_extraFilter == null || _extraFilter(s));
                string display = $"{s.DisplayId} {s.DisplayName}";
                _items.Add(new PickerItem
                {
                    Vm = s,
                    AvatarUri = SafeUri(() => s.AvatarUri),
                    BgUri = SafeUri(() => s.AvatarBgUri),
                    Display = display,
                    InfoLine = $"{s.Rarity} ｜ {s.ShipType} ｜ {ArmorName(s)}",
                    Rarity = s.Rarity,
                    IdText = s.DisplayId,
                    Allowed = allowed,
                    Category = s.CategoryEnum,
                    Faction = s.FactionEnum,
                    ShipType = s.ShipTypeEnum,
                    RarityEnum = s.RarityEnum,
                    Owned = s.Owned,
                    GameOrder = s.GameOrder,
                    Breakthrough = s.Breakthrough,
                    Oath = s.Oath,
                    Level120 = s.Level120,
                    SearchText = $"{s.DisplayName}\u0001{s.RawName}\u0001{s.AliasesText}\u0001{s.DisplayId}"
                });
            }
        }

        private static string SafeUri(Func<string> getter)
        {
            try { return getter() ?? ""; }
            catch { return ""; }
        }

        private void BuildFilters()
        {
            CategoryFilter.ItemsSource = new List<ComboEntry>
            {
                new ComboEntry { Text = L("TeamPicker_All", "全部"), Tag = null },
                new ComboEntry { Text = LocalizationHelper.GetEnumString("ShipCategory", (int)ShipCategory.Normal), Tag = ShipCategory.Normal },
                new ComboEntry { Text = LocalizationHelper.GetEnumString("ShipCategory", (int)ShipCategory.Collab), Tag = ShipCategory.Collab },
                new ComboEntry { Text = LocalizationHelper.GetEnumString("ShipCategory", (int)ShipCategory.Research), Tag = ShipCategory.Research },
                new ComboEntry { Text = LocalizationHelper.GetEnumString("ShipCategory", (int)ShipCategory.META), Tag = ShipCategory.META }
            };
            CategoryFilter.SelectedIndex = 0;

            var factions = _items.Where(x => x.Allowed).Select(x => x.Faction).Distinct().OrderBy(f => (int)f).ToList();
            var factionItems = new List<ComboEntry> { new ComboEntry { Text = L("TeamPicker_All", "全部"), Tag = null } };
            factionItems.AddRange(factions.Select(f => new ComboEntry { Text = LocalizationHelper.GetEnumString("Faction", (int)f), Tag = f }));
            FactionFilter.ItemsSource = factionItems;
            FactionFilter.SelectedIndex = 0;

            var typeItems = new List<ComboEntry> { new ComboEntry { Text = L("TeamPicker_All", "全部"), Tag = null } };
            foreach (var t in _allowedTypes.OrderBy(t => (int)t))
                typeItems.Add(new ComboEntry { Text = LocalizationHelper.GetEnumString("ShipType", (int)t), Tag = t });
            ShipTypeFilter.ItemsSource = typeItems;
            ShipTypeFilter.SelectedIndex = 0;

            var rarityItems = new List<ComboEntry> { new ComboEntry { Text = L("TeamPicker_All", "全部"), Tag = null } };
            foreach (var r in new[] { Rarity.N, Rarity.R, Rarity.SR, Rarity.SSR, Rarity.UR })
                rarityItems.Add(new ComboEntry { Text = LocalizationHelper.GetEnumString("Rarity", (int)r), Tag = r });
            RarityFilter.ItemsSource = rarityItems;
            RarityFilter.SelectedIndex = 0;

            // 与舰船列表（MainPage）的排序选项保持一致
            var sortItems = new List<ComboEntry>
            {
                new ComboEntry { Text = "按编号排序", Tag = 0 },
                new ComboEntry { Text = "按图鉴排序", Tag = 1 },
                new ComboEntry { Text = "按名称排序", Tag = 2 },
                new ComboEntry { Text = "按稀有度排序", Tag = 3 },
            };
            SortCombo.ItemsSource = sortItems;
            SortCombo.SelectedIndex = 0;
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilters();

        private void SortOrderToggle_Checked(object sender, RoutedEventArgs e)
        {
            _ascending = false;
            if (SortOrderToggle != null) SortOrderToggle.Content = "降序";
            ApplyFilters();
        }

        private void SortOrderToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _ascending = true;
            if (SortOrderToggle != null) SortOrderToggle.Content = "升序";
            ApplyFilters();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

        private void ApplyFilters()
        {
            var query = _items.Where(x => x.Allowed);

            if ((CategoryFilter.SelectedItem as ComboEntry)?.Tag is ShipCategory cat)
                query = query.Where(x => x.Category == cat);
            if ((FactionFilter.SelectedItem as ComboEntry)?.Tag is Faction fac)
                query = query.Where(x => x.Faction == fac);
            if ((ShipTypeFilter.SelectedItem as ComboEntry)?.Tag is ShipType type)
                query = query.Where(x => x.ShipType == type);
            if ((RarityFilter.SelectedItem as ComboEntry)?.Tag is Rarity rarity)
                query = query.Where(x => x.RarityEnum == rarity);

            string keyword = SearchBox.Text?.Trim() ?? "";
            if (keyword.Length > 0)
                query = query.Where(x => x.SearchText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            int sort = (SortCombo.SelectedItem as ComboEntry)?.Tag is int si ? si : 0;
            IOrderedEnumerable<PickerItem> ordered = sort switch
            {
                1 => query.OrderBy(x => x.GameOrder).ThenBy(x => x.Vm.Id),
                2 => query.OrderBy(x => x.Vm.RawName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Vm.Id),
                3 => query.OrderBy(x => x.RarityEnum).ThenBy(x => x.Vm.Id),
                _ => query.OrderBy(x => x.Vm.Id)
            };
            query = _ascending ? ordered : ordered.Reverse();

            // 必须每次赋新实例：同一个 List 实例重复赋给 ItemsSource 时 WinUI 不会刷新列表
            var list = query.ToList();
            _current.Clear();
            _current.AddRange(list);
            PickerGrid.ItemsSource = null;
            PickerGrid.ItemsSource = list;
            EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string ArmorName(ShipViewModel vm)
        {
            var stats = vm.StatsLevels?.FirstOrDefault();
            if (stats == null) return "—";
            return stats.Armor switch
            {
                Models.ArmorType.Light => "轻甲",
                Models.ArmorType.Medium => "中甲",
                Models.ArmorType.Heavy => "重甲",
                _ => stats.Armor.ToString()
            };
        }

        private void PickerGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is PickerItem item)
            {
                SelectedShip = item.Vm;
                Hide();
            }
        }
    }
}
