using AzurLaneDex.Models;
using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzurLaneDex.Views
{
    /// <summary>
    /// 配装装备选择弹窗：图标含稀有度背景（Assets/icon/item/equips/{IconId}.png），
    /// 名称/类型/稀有度来自数据库 equipments 表（EquipmentCatalog 会话内缓存）。
    /// 过滤：槽位可装类型 + 排除占位/展示类装备（类型 99、模板、序章、默认武器）；
    /// 搜索直接匹配（全部语言名称 + 编号），条目一次性构建后仅做内存过滤。
    /// </summary>
    public sealed partial class EquipPickerDialog : ContentDialog
    {
        private static string L(string key, string fallback)
        {
            string text = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse().GetString(key);
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        private readonly IReadOnlyCollection<int> _allowedTypes;

        /// <summary>一次性构建的条目（含图标 URI）。</summary>
        private readonly List<Item> _items = new();
        private bool _loaded;

        /// <summary>选中的装备 id；-1 = 清除该位；null = 未操作。</summary>
        public int? SelectedId { get; private set; }

        /// <summary>列表项包装。</summary>
        public sealed class Item
        {
            public string IconUri { get; set; } = "";
            public string BgUri { get; set; } = "";
            public bool HasBg { get; set; }
            public string Name { get; set; } = "";
            public string Sub { get; set; } = "";
            public int Id { get; set; }
            public int Type { get; set; }
            public int Rarity { get; set; }
            /// <summary>全部语言名称 + 编号（搜索用）。</summary>
            public string SearchText { get; set; } = "";
        }

        /// <summary>可选项包装。</summary>
        public sealed class ComboEntry
        {
            public string Text { get; set; } = "";
            public object? Tag { get; set; }
            public override string ToString() => Text;
        }

        public EquipPickerDialog(IReadOnlyCollection<int> allowedTypes)
        {
            this.InitializeComponent();
            _allowedTypes = allowedTypes ?? Array.Empty<int>();
            this.Title = L("EquipPicker_Title", "选择装备");
            this.PrimaryButtonText = L("EquipPicker_Clear", "清除该位装备");
            this.CloseButtonText = L("TeamPage_Cancel", "取消");
            SearchBox.PlaceholderText = L("EquipPicker_Search", "搜索装备名称/编号");
            EmptyText.Text = L("EquipPicker_Empty", "没有符合条件的装备");
            this.Loaded += async (s, e) => await LoadAsync();
        }

        private async Task LoadAsync()
        {
            if (_loaded) return;
            _loaded = true;

            await EquipmentCatalog.EnsureLoadedAsync();

            foreach (var e in EquipmentCatalog.All)
            {
                if (e.IsPlaceholder) continue;                       // 占位/展示类不进选择器
                if (!IsSelectableId(e.Id)) continue;                 // 编队装备 id 区间过滤
                if (_allowedTypes.Count > 0 && !_allowedTypes.Contains(e.Type)) continue;

                var search = new StringBuilder();
                foreach (var kv in e.Name) search.Append(kv.Value).Append('\u0001');
                search.Append(e.Id);

                _items.Add(new Item
                {
                    Id = e.Id,
                    IconUri = $"ms-appx:///Assets/icon/item/equips/{e.IconId}.png",
                    BgUri = RarityBackground.EquipUri(e.Rarity),
                    HasBg = e.Rarity >= 1,
                    Name = e.DisplayName,
                    Sub = $"#{e.Id} ｜ {EquipTypeName(e.Type)} ｜ {(e.Rarity >= 1 ? $"T{e.Rarity}" : "—")}",
                    Type = e.Type,
                    Rarity = e.Rarity,
                    SearchText = search.ToString()
                });
            }

            BuildFilters();
            ApplyFilter();
        }

        private void BuildFilters()
        {
            string all = L("TeamPicker_All", "全部");

            var typeItems = new List<ComboEntry> { new ComboEntry { Text = all, Tag = null } };
            foreach (var t in DistinctTypes()) typeItems.Add(new ComboEntry { Text = EquipTypeName(t), Tag = t });
            TypeFilter.ItemsSource = typeItems;
            TypeFilter.SelectedIndex = 0;

            var rarityItems = new List<ComboEntry> { new ComboEntry { Text = all, Tag = null } };
            for (int r = 1; r <= 6; r++) rarityItems.Add(new ComboEntry { Text = $"T{r}", Tag = r });
            RarityFilter.ItemsSource = rarityItems;
            RarityFilter.SelectedIndex = 0;

            var sortItems = new List<ComboEntry>
            {
                new ComboEntry { Text = L("TeamPicker_SortId", "按编号"), Tag = 0 },
                new ComboEntry { Text = L("TeamPicker_SortName", "按名称"), Tag = 1 },
                new ComboEntry { Text = L("TeamPicker_SortRarity", "按稀有度"), Tag = 2 }
            };
            SortCombo.ItemsSource = sortItems;
            SortCombo.SelectedIndex = 0;
        }

        private IEnumerable<int> DistinctTypes()
        {
            var seen = new HashSet<int>();
            foreach (var item in _items)
                if (item.Type > 0 && seen.Add(item.Type)) yield return item.Type;
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

        private void ApplyFilter()
        {
            if (!_loaded)
            {
                EquipGrid.ItemsSource = null;
                return;
            }

            IEnumerable<Item> q = _items;

            if ((TypeFilter.SelectedItem as ComboEntry)?.Tag is int type)
                q = q.Where(x => x.Type == type);
            if ((RarityFilter.SelectedItem as ComboEntry)?.Tag is int rarity)
                q = q.Where(x => x.Rarity == rarity);

            string keyword = SearchBox.Text?.Trim() ?? "";
            if (keyword.Length > 0)
                q = q.Where(x => x.SearchText.Contains(keyword, StringComparison.OrdinalIgnoreCase));

            int sort = (SortCombo.SelectedItem as ComboEntry)?.Tag is int si ? si : 0;
            q = sort switch
            {
                1 => q.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id),
                2 => q.OrderByDescending(x => x.Rarity).ThenBy(x => x.Id),
                _ => q.OrderBy(x => x.Id)
            };

            var list = q.ToList();
            EquipGrid.ItemsSource = list;
            EmptyText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void EquipGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Item item)
            {
                SelectedId = item.Id;
                Hide();
            }
        }

        private void OnClearEquip(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SelectedId = -1;
        }

        /// <summary>
        /// 编队装备可用 id 区间：排除 1~499、6000~15000、60000~84999、170000~999999
        /// （这些是教学/占位/未实装分组；保留 500~5999、15001~59999、85000~169999 与 ≥1000000）。
        /// </summary>
        private static bool IsSelectableId(int id)
        {
            if (id <= 0) return false;
            if (id >= 1 && id <= 499) return false;
            if (id >= 6000 && id <= 15000) return false;
            if (id >= 60000 && id <= 84999) return false;
            if (id >= 170000 && id <= 999999) return false;
            return true;
        }

        private static string EquipTypeName(int code) => code switch
        {
            1 => "驱逐主炮", 2 => "轻巡主炮", 3 => "重巡主炮", 4 => "战列主炮",
            5 => "鱼雷", 6 => "防空炮", 7 => "战斗机", 8 => "鱼雷机", 9 => "轰炸机",
            10 => "设备", 11 => "超巡主炮", 12 => "水上机", 13 => "潜艇鱼雷", 14 => "深水炸弹",
            15 => "反潜机", 17 => "反潜直升机", 18 => "货物", 20 => "导弹", 21 => "近炸防空炮",
            _ => $"类型{code}"
        };
    }
}
