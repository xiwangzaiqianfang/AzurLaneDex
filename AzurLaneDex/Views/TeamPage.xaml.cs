using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using AzurLaneDex.Services;
using AzurLaneDex.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace AzurLaneDex.Views
{
    /// <summary>
    /// 配队管理页（多舰队）：
    /// 1~3 支水面舰队（各 3 前排 + 3 后排）+ 1 支水下舰队（3 潜艇）+ 1 支支援舰队（3 槽，航母/潜艇两种）。
    /// 每个槽位显示头像、名称、舰种/稀有度，可切换改造形态并编辑配装；配装按舰队分别保存。
    /// 分享：导出图片（分组渲染，预加载头像/图标）与分享码（ALD1: 前缀）。
    /// </summary>
    public sealed partial class TeamPage : Page
    {
        private App? _app;
        private Team? _draft;
        private readonly List<SlotUi> _slots = new();
        private readonly List<TextBox> _fleetNameBoxes = new();
        private bool _suppressSupportKindEvent;

        /// <summary>前排可选舰种（DDG 另有谓词区分先锋/主力）。</summary>
        private static readonly ShipType[] FrontTypes = { ShipType.DD, ShipType.CL, ShipType.CA, ShipType.CB, ShipType.DDG };
        /// <summary>后排可选舰种。</summary>
        private static readonly ShipType[] BackTypes = { ShipType.CV, ShipType.CVL, ShipType.BM, ShipType.BB, ShipType.BC, ShipType.BBV, ShipType.AR, ShipType.DDG };
        private static readonly ShipType[] SubmarineTypes = { ShipType.SS, ShipType.SSV };
        private static readonly ShipType[] CarrierSupportTypes = { ShipType.CV, ShipType.CVL };

        private static string L(string key, string fallback)
        {
            string text = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse().GetString(key);
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        private static Brush? ResBrush(string key)
        {
            try
            {
                if (Application.Current.Resources.TryGetValue(key, out var value)) return value as Brush;
            }
            catch { }
            try { return Application.Current.Resources[key] as Brush; } catch { return null; }
        }

        private static Brush? SecondaryBrush => ResBrush("TextFillColorSecondaryBrush");

        /// <summary>槽位控件集合。</summary>
        private sealed class SlotUi
        {
            public Fleet Fleet = null!;
            public int Index;
            public Button PickerButton = null!;
            public Image AvatarImage = null!;
            public Image AvatarBgImage = null!;
            public TextBlock NameText = null!;
            public TextBlock InfoText = null!;
            public ToggleButton RetrofitToggle = null!;
            public Grid EquipBar = null!;
        }

        public TeamPage()
        {
            this.InitializeComponent();
            _app = Application.Current as App;
            this.Loaded += TeamPage_Loaded;
            _ = EquipmentCatalog.EnsureLoadedAsync();
            _ = AugmentCatalog.EnsureLoadedAsync();
        }

        private void TeamPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_app?.ShipManager == null) return;
            _app.ShipManager.TeamsChanged += OnTeamsChanged;
            RefreshTeamList();
            BuildSupportKindCombo();
            StartNewDraft();
        }

        private void OnTeamsChanged()
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                RefreshTeamList();
                if (TeamListView.SelectedItem == null) StartNewDraft();
            });
        }

        private void RefreshTeamList()
        {
            if (_app?.ShipManager == null) return;
            TeamListView.ItemsSource = _app.ShipManager.Teams;
        }

        private ShipViewModel? FindVm(int shipId)
            => _app?.ShipManager?.Ships.FirstOrDefault(s => s.Id == shipId);

        // ================= 支援舰队类型 =================

        private void BuildSupportKindCombo()
        {
            _suppressSupportKindEvent = true;
            SupportKindCombo.ItemsSource = new List<string>
            {
                L("TeamPage_SupportCarrier", "航母支援（CV/CVL）"),
                L("TeamPage_SupportSubmarine", "潜艇支援（SS/SSV）")
            };
            SupportKindCombo.SelectedIndex = 0;
            _suppressSupportKindEvent = false;
        }

        private async void SupportKind_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSupportKindEvent || _draft == null) return;
            if (SupportKindCombo.SelectedIndex < 0) return;

            var newKind = SupportKindCombo.SelectedIndex == 1 ? SupportFleetKind.Submarine : SupportFleetKind.Carrier;
            if (newKind == _draft.SupportKindValue) return;

            var invalid = new List<int>();
            for (int i = 0; i < _draft.Support.ShipIds.Count; i++)
            {
                int id = _draft.Support.ShipIds[i];
                if (id <= 0) continue;
                var vm = FindVm(id);
                if (vm == null || !IsAllowedInSupport(vm, newKind)) invalid.Add(i);
            }

            if (invalid.Count > 0)
            {
                var dialog = new ContentDialog
                {
                    Title = L("TeamPage_SupportSwitchTitle", "切换支援类型"),
                    Content = string.Format(
                        L("TeamPage_SupportSwitchMessage", "当前支援舰队中有 {0} 个槽位的舰船不符合新类型。\n\n选择「回退切换」保持原类型；选择「清空切换」则清空这些槽位后切换。"),
                        invalid.Count),
                    PrimaryButtonText = L("TeamPage_SupportSwitchRevert", "回退切换"),
                    SecondaryButtonText = L("TeamPage_SupportSwitchClear", "清空切换"),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };

                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Secondary)
                {
                    // 回退：恢复下拉到原类型
                    _suppressSupportKindEvent = true;
                    SupportKindCombo.SelectedIndex = _draft.SupportKindValue == SupportFleetKind.Submarine ? 1 : 0;
                    _suppressSupportKindEvent = false;
                    return;
                }

                foreach (int i in invalid)
                {
                    _draft.Support.ShipIds[i] = 0;
                    if (_draft.Support.Equips != null) _draft.Support.Equips[i] = null;
                    if (_draft.Support.Retrofits != null) _draft.Support.Retrofits[i] = false;
                }
            }

            _draft.SupportKind = newKind == SupportFleetKind.Submarine ? "submarine" : "carrier";
            RebuildEditor();
        }

        // ================= 舰队槽位规则 =================

        private static bool IsAllowedInSupport(ShipViewModel vm, SupportFleetKind kind)
            => kind == SupportFleetKind.Carrier
                ? vm.ShipTypeEnum is ShipType.CV or ShipType.CVL
                : vm.ShipTypeEnum is ShipType.SS or ShipType.SSV;

        /// <summary>DDG 拆分：先锋（DB 舰种 id=2）只进前排，主力（3）只进后排。</summary>
        private static Func<ShipViewModel, bool>? DdgPredicate(bool vanguard)
            => vm => vm.ShipTypeEnum != ShipType.DDG || vm.ShipTypeId == (vanguard ? 2 : 3);

        private static string FleetTitle(Fleet fleet) => string.IsNullOrWhiteSpace(fleet.Name) ? "舰队" : fleet.Name;

        // ================= 编辑器构建 =================

        private void RebuildEditor()
        {
            if (_draft == null) return;
            _draft.Normalize();

            SurfaceFleetHost.Children.Clear();
            SubmarineHost.Children.Clear();
            SupportHost.Children.Clear();
            _slots.Clear();
            _fleetNameBoxes.Clear();

            for (int i = 0; i < _draft.Surface.Count; i++)
                SurfaceFleetHost.Children.Add(BuildSurfaceFleetBlock(_draft.Surface[i], i));

            SubmarineHost.Children.Add(BuildSmallFleetBlock(_draft.Submarine, SubmarineTypes, null, "水下舰队", slotCount: 3));
            SupportHost.Children.Add(BuildSmallFleetBlock(
                _draft.Support,
                _draft.SupportKindValue == SupportFleetKind.Carrier ? CarrierSupportTypes : SubmarineTypes,
                null,
                "支援舰队",
                slotCount: 3));

            _suppressSupportKindEvent = true;
            SupportKindCombo.SelectedIndex = _draft.SupportKindValue == SupportFleetKind.Submarine ? 1 : 0;
            _suppressSupportKindEvent = false;

            AddSurfaceFleetButton.IsEnabled = _draft.Surface.Count < Team.MaxSurfaceFleets;
            AddSurfaceFleetButton.Visibility = _draft.Surface.Count < Team.MaxSurfaceFleets ? Visibility.Visible : Visibility.Collapsed;

            RefreshAllSlots();
        }

        private Border BuildSurfaceFleetBlock(Fleet fleet, int fleetIndex)
        {
            var block = new Border
            {
                BorderBrush = ResBrush("CardStrokeColorDefaultBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel { Spacing = 6 };
            var header = new Grid { ColumnSpacing = 8 };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameBox = new TextBox
            {
                Text = string.IsNullOrEmpty(fleet.Name)
                    ? new[] { "一队", "二队", "三队" }[Math.Min(fleetIndex, 2)]
                    : fleet.Name,
                PlaceholderText = "舰队名称",
                Width = 220,
                HorizontalAlignment = HorizontalAlignment.Left,
                Tag = fleet
            };
            nameBox.TextChanged += (s, e) =>
            {
                if (((TextBox)s).Tag is Fleet f) f.Name = ((TextBox)s).Text;
            };
            _fleetNameBoxes.Add(nameBox);
            Grid.SetColumn(nameBox, 0);
            header.Children.Add(nameBox);

            var delete = new Button { Content = "删除该舰队", Tag = fleetIndex, VerticalAlignment = VerticalAlignment.Bottom };
            delete.Click += (s, e) => DeleteSurfaceFleet((int)((Button)s).Tag);
            Grid.SetColumn(delete, 1);
            header.Children.Add(delete);

            stack.Children.Add(header);
            stack.Children.Add(BuildSlotRowCaption("前排（先锋）"));
            stack.Children.Add(BuildSlotRow(fleet, 0, 3, FrontTypes, DdgPredicate(true)));
            stack.Children.Add(BuildSlotRowCaption("后排（主力）"));
            stack.Children.Add(BuildSlotRow(fleet, 3, 3, BackTypes, DdgPredicate(false)));

            block.Child = stack;
            return block;
        }

        private Border BuildSmallFleetBlock(Fleet fleet, ShipType[] allowed, Func<ShipViewModel, bool>? predicate,
                                            string caption, int slotCount)
        {
            var block = new Border
            {
                BorderBrush = ResBrush("CardStrokeColorDefaultBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10)
            };

            var stack = new StackPanel { Spacing = 6 };
            stack.Children.Add(BuildSlotRowCaption(caption));
            stack.Children.Add(BuildSlotRow(fleet, 0, slotCount, allowed, predicate));
            block.Child = stack;
            return block;
        }

        private static TextBlock BuildSlotRowCaption(string text)
            => new()
            {
                Text = text,
                FontSize = 12,
                Foreground = SecondaryBrush,
                Margin = new Thickness(0, 2, 0, 0)
            };

        private StackPanel BuildSlotRow(Fleet fleet, int startIndex, int count, ShipType[] allowed,
                                        Func<ShipViewModel, bool>? predicate)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            for (int i = 0; i < count; i++)
                row.Children.Add(BuildSlotCard(fleet, startIndex + i, allowed, predicate));
            return row;
        }

        private Border BuildSlotCard(Fleet fleet, int slotIndex, ShipType[] allowed,
                                     Func<ShipViewModel, bool>? predicate)
        {
            var card = new Border
            {
                Width = 220,
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = ResBrush("CardStrokeColorDefaultBrush"),
                Background = ResBrush("CardBackgroundFillColorDefaultBrush")
            };

            var stack = new StackPanel { Spacing = 6 };
            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var picker = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 4, 6, 4)
            };
            var content = new Grid { ColumnSpacing = 8 };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarHost = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(4) };
            var avatarGrid = new Grid();
            var bgImage = new Image { Stretch = Stretch.UniformToFill };
            var fgImage = new Image { Stretch = Stretch.UniformToFill };
            avatarGrid.Children.Add(bgImage);
            avatarGrid.Children.Add(fgImage);
            avatarHost.Child = avatarGrid;
            Grid.SetColumn(avatarHost, 0);
            content.Children.Add(avatarHost);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            var nameText = new TextBlock { FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            var infoText = new TextBlock { FontSize = 11, Foreground = SecondaryBrush, TextTrimming = TextTrimming.CharacterEllipsis };
            textStack.Children.Add(nameText);
            textStack.Children.Add(infoText);
            Grid.SetColumn(textStack, 1);
            content.Children.Add(textStack);
            picker.Content = content;
            Grid.SetColumn(picker, 0);
            row.Children.Add(picker);

            var retrofit = new ToggleButton { Content = L("TeamPage_Retrofit", "改造"), MinWidth = 52, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(retrofit, 1);
            row.Children.Add(retrofit);

            var clear = new Button { Content = "✕", VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(clear, 2);
            row.Children.Add(clear);

            stack.Children.Add(row);

            var equipBar = new Grid { ColumnSpacing = 4, RowSpacing = 4 };
            for (int c = 0; c < 3; c++) equipBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            stack.Children.Add(equipBar);

            card.Child = stack;

            var ui = new SlotUi
            {
                Fleet = fleet,
                Index = slotIndex,
                PickerButton = picker,
                AvatarImage = fgImage,
                AvatarBgImage = bgImage,
                NameText = nameText,
                InfoText = infoText,
                RetrofitToggle = retrofit,
                EquipBar = equipBar
            };
            _slots.Add(ui);

            picker.Click += async (s, e) => await OpenShipPickerAsync(ui, allowed, predicate);
            retrofit.Click += (s, e) =>
            {
                fleet.Normalize(fleet.ShipIds.Count);
                fleet.Retrofits![slotIndex] = retrofit.IsChecked == true;
                RefreshSlot(ui);
            };
            clear.Click += (s, e) => ClearSlot(ui);

            return card;
        }

        // ================= 槽位刷新 =================

        private void RefreshAllSlots()
        {
            foreach (var ui in _slots) RefreshSlot(ui);
        }

        private void RefreshSlot(SlotUi ui)
        {
            var fleet = ui.Fleet;
            fleet.Normalize(fleet.ShipIds.Count);
            int id = ui.Index < fleet.ShipIds.Count ? fleet.ShipIds[ui.Index] : 0;
            var vm = id > 0 ? FindVm(id) : null;

            if (vm == null)
            {
                ui.AvatarImage.Source = null;
                ui.AvatarBgImage.Source = null;
                ui.NameText.Text = string.Format(L("TeamPage_SlotEmptyButton", "位置 {0}（点击选择）"), ui.Index + 1);
                ui.InfoText.Text = L("TeamPage_SlotEmptyHint", "未编入舰船");
                ui.RetrofitToggle.IsEnabled = false;
                ui.RetrofitToggle.IsChecked = false;
                ui.EquipBar.Children.Clear();
                return;
            }

            bool canRetrofit = vm.Retrofit?.CanRetrofit == true;
            bool retro = canRetrofit && ui.Index < fleet.Retrofits!.Count && fleet.Retrofits[ui.Index];
            ui.RetrofitToggle.IsChecked = retro;
            ui.RetrofitToggle.IsEnabled = canRetrofit;

            ui.NameText.Text = vm.DisplayName;
            string type = LocalizationHelper.GetEnumString("ShipType", (int)vm.ShipTypeEnum);
            string rarity = RarityText(vm.RarityFor(retro));
            ui.InfoText.Text = $"{vm.DisplayId} ｜ {type} ｜ {rarity}" + (retro ? " ｜ " + L("TeamPage_RetrofitTag", "已改造") : "");

            try
            {
                string fg = vm.AvatarUriFor(retro);
                ui.AvatarImage.Source = string.IsNullOrEmpty(fg) ? null : new BitmapImage(new Uri(fg));
                string bg = vm.AvatarBgUriFor(retro);
                ui.AvatarBgImage.Source = string.IsNullOrEmpty(bg) ? null : new BitmapImage(new Uri(bg));
            }
            catch
            {
                ui.AvatarImage.Source = null;
                ui.AvatarBgImage.Source = null;
            }

            RefreshEquipBar(ui, vm);
        }

        private static string RarityText(Rarity rarity)
            => LocalizationHelper.GetEnumString("Rarity", (int)rarity);

        private void RefreshEquipBar(SlotUi ui, ShipViewModel vm)
        {
            var grid = ui.EquipBar;
            grid.Children.Clear();
            grid.RowDefinitions.Clear();
            for (int c = 0; c < 3; c++)
            {
                if (grid.ColumnDefinitions.Count <= c)
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            int cell = 0;
            void Place(FrameworkElement element)
            {
                int rowIndex = cell / 3, colIndex = cell % 3;
                while (grid.RowDefinitions.Count <= rowIndex)
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Grid.SetRow(element, rowIndex);
                Grid.SetColumn(element, colIndex);
                grid.Children.Add(element);
                cell++;
            }

            var row = vm.SlotLevels?.FirstOrDefault();
            var fleet = ui.Fleet;
            fleet.Normalize(fleet.ShipIds.Count);
            var map = fleet.EquipMap(ui.Index);

            if (row != null)
            {
                for (int pos = 0; pos < row.Count; pos++)
                {
                    int posCopy = pos;
                    int stored = map.TryGetValue(pos, out var v) ? v : 0;
                    var slotInfo = row[pos];

                    var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                    if (stored > 0)
                    {
                        int iconId = EquipmentCatalog.IconIdOf(stored);
                        if (iconId <= 0) iconId = stored;
                        var icon = new Image { Width = 22, Height = 22, Stretch = Stretch.Uniform };
                        try { icon.Source = new BitmapImage(new Uri($"ms-appx:///Assets/icon/item/equips/{iconId}.png")); } catch { }
                        content.Children.Add(icon);
                    }

                    string label = stored > 0
                        ? (string.IsNullOrEmpty(EquipmentCatalog.NameOf(stored)) ? $"#{stored}" : EquipmentCatalog.NameOf(stored))
                        : (slotInfo.Types != null && slotInfo.Types.Count > 0
                            ? string.Join("/", slotInfo.Types.Select(EquipTypeName))
                            : "—");
                    content.Children.Add(new TextBlock
                    {
                        Text = label,
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis
                    });

                    var btn = new Button
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        Padding = new Thickness(6, 2, 6, 2),
                        Content = content
                    };
                    ToolTipService.SetToolTip(btn, label);
                    btn.Click += async (s, e) => await OpenEquipPickerAsync(ui, posCopy);
                    Place(btn);
                }
            }

            // 专属兵装槽（每艘船单独选择；无兵装时禁用）
            var augContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            int augmentId = fleet.Augments != null && ui.Index < fleet.Augments.Count ? fleet.Augments[ui.Index] : 0;
            string augmentName = augmentId > 0 ? ResolveAugmentName(vm, augmentId) : "";
            string augmentRarity = augmentId > 0 ? ResolveAugmentRarity(vm, augmentId) : "";
            if (augmentId > 0)
            {
                var icon = new Image { Width = 22, Height = 22, Stretch = Stretch.Uniform };
                try { icon.Source = new BitmapImage(new Uri($"ms-appx:///Assets/icon/item/spweapon/{AugmentCatalog.IconIdOf(augmentId)}.png")); } catch { }
                augContent.Children.Add(icon);
                augContent.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrEmpty(augmentName)
                        ? $"#{augmentId}"
                        : (string.IsNullOrEmpty(augmentRarity) ? augmentName : $"{augmentRarity} · {augmentName}"),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });
            }
            else
            {
                augContent.Children.Add(new TextBlock
                {
                    Text = L("TeamPage_AugmentSlot", "兵装"),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = SecondaryBrush
                });
            }

            var augButton = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 2, 6, 2),
                Content = augContent,
                IsEnabled = true   // 无专属兵装的船也可选特殊兵装
            };
            ToolTipService.SetToolTip(augButton,
                string.IsNullOrEmpty(augmentName) ? L("TeamPage_AugmentSlot", "兵装")
                : (string.IsNullOrEmpty(augmentRarity) ? augmentName : $"{augmentRarity} · {augmentName}"));
            augButton.Click += (s, e) => ShowAugmentMenu(ui, vm, augButton);
            Place(augButton);
        }

        /// <summary>兵装品质文本（稀有度枚举名；越界回退 T 级）。</summary>
        private static string AugmentRarityText(int rarityId)
        {
            if (rarityId <= 0) return "";
            string text = LocalizationHelper.GetEnumString("Rarity", rarityId);
            return text == $"Rarity_{rarityId}" ? $"T{rarityId}" : text;
        }

        /// <summary>已选兵装的品质文本（专属兵装用其稀有度枚举，特殊兵装用 rarity_id）。</summary>
        private static string ResolveAugmentRarity(ShipViewModel vm, int augmentId)
        {
            var unique = vm.Augments?.FirstOrDefault(a => a.Id == augmentId);
            if (unique != null) return AugmentRarityText((int)unique.Rarity);
            var special = AugmentCatalog.All.FirstOrDefault(a => a.Id == augmentId);
            return special != null ? AugmentRarityText(special.Rarity) : "";
        }

        /// <summary>兵装菜单项：带图标与品质前缀，便于区分。</summary>
        private static MenuFlyoutItem BuildAugmentItem(ShipViewModel vm, int augmentId, string name, string rarityText, Action onClick)
        {
            var item = new MenuFlyoutItem
            {
                Text = string.IsNullOrEmpty(rarityText) ? name : $"{rarityText} · {name}"
            };
            try
            {
                item.Icon = new ImageIcon
                {
                    Source = new BitmapImage(new Uri($"ms-appx:///Assets/icon/item/spweapon/{AugmentCatalog.IconIdOf(augmentId)}.png"))
                };
            }
            catch { }
            item.Click += (s, e) => onClick();
            return item;
        }

        /// <summary>兵装名称：先查专属兵装，再查特殊兵装目录。</summary>
        private static string ResolveAugmentName(ShipViewModel vm, int augmentId)
        {
            var unique = vm.Augments?.FirstOrDefault(a => a.Id == augmentId);
            if (unique != null) return unique.Name.GetLocalized();
            return AugmentCatalog.NameOf(augmentId);
        }

        /// <summary>兵装选择菜单：该船专属兵装 + 按舰种开放的特殊兵装（无专属兵装时也可选）。</summary>
        private void ShowAugmentMenu(SlotUi ui, ShipViewModel vm, FrameworkElement target)
        {
            var fleet = ui.Fleet;
            fleet.Normalize(fleet.ShipIds.Count);

            var flyout = new MenuFlyout();

            var uniques = vm.Augments ?? Array.Empty<UniqueAugmentInfo>();
            if (uniques.Count > 0)
            {
                flyout.Items.Add(new MenuFlyoutItem { Text = L("TeamPage_AugmentUniqueHeader", "专属兵装"), IsEnabled = false });
                foreach (var augment in uniques)
                {
                    int augmentId = augment.Id;
                    flyout.Items.Add(BuildAugmentItem(vm, augmentId, augment.Name.GetLocalized(),
                        AugmentRarityText((int)augment.Rarity),
                        () => { fleet.Augments![ui.Index] = augmentId; RefreshSlot(ui); }));
                }
            }

            var specials = AugmentCatalog.ForShipType(vm.ShipTypeId).ToList();
            if (specials.Count > 0)
            {
                if (flyout.Items.Count > 0) flyout.Items.Add(new MenuFlyoutSeparator());
                flyout.Items.Add(new MenuFlyoutItem { Text = L("TeamPage_AugmentSpecialHeader", "特殊兵装"), IsEnabled = false });
                foreach (var special in specials)
                {
                    int augmentId = special.Id;
                    flyout.Items.Add(BuildAugmentItem(vm, augmentId, special.Name.GetLocalized(),
                        AugmentRarityText(special.Rarity),
                        () => { fleet.Augments![ui.Index] = augmentId; RefreshSlot(ui); }));
                }
            }

            if (flyout.Items.Count == 0)
                flyout.Items.Add(new MenuFlyoutItem { Text = L("TeamPage_AugmentNone", "该舰种没有可用兵装"), IsEnabled = false });

            flyout.Items.Add(new MenuFlyoutSeparator());
            var remove = new MenuFlyoutItem { Text = L("TeamPage_AugmentRemove", "移除兵装") };
            remove.Click += (s, e) => { fleet.Augments![ui.Index] = 0; RefreshSlot(ui); };
            flyout.Items.Add(remove);

            flyout.ShowAt(target);
        }

        // ================= 选择器 =================

        private async Task OpenShipPickerAsync(SlotUi ui, ShipType[] allowed, Func<ShipViewModel, bool>? predicate)
        {
            var mgr = _app?.ShipManager;
            if (mgr == null || _draft == null) return;

            var dialog = new TeamShipPickerDialog(mgr.Ships, allowed, predicate)
            {
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
            if (dialog.SelectedShip == null) return;

            int id = dialog.SelectedShip.Id;
            if (_draft.AllShipIds.Contains(id))
            {
                TeamStatusText.Text = L("TeamPage_Error_Duplicate", "同一舰船不能重复编入多个位置。");
                return;
            }

            ui.Fleet.Normalize(ui.Fleet.ShipIds.Count);
            ui.Fleet.ShipIds[ui.Index] = id;
            if (ui.Fleet.Equips != null) ui.Fleet.Equips[ui.Index] = null;
            if (ui.Fleet.Retrofits != null) ui.Fleet.Retrofits[ui.Index] = false;
            RefreshSlot(ui);
        }

        private async Task OpenEquipPickerAsync(SlotUi ui, int equipPos)
        {
            if (_draft == null) return;
            var vm = ui.Index < ui.Fleet.ShipIds.Count ? FindVm(ui.Fleet.ShipIds[ui.Index]) : null;
            var row = vm?.SlotLevels?.FirstOrDefault();
            if (vm == null || row == null || equipPos >= row.Count) return;

            await EquipmentCatalog.EnsureLoadedAsync();
            var dialog = new EquipPickerDialog(row[equipPos].Types)
            {
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
            if (dialog.SelectedId == null) return;

            var map = ui.Fleet.EquipMap(ui.Index);
            if (dialog.SelectedId == -1) map.Remove(equipPos);
            else map[equipPos] = dialog.SelectedId.Value;
            RefreshEquipBar(ui, vm);
        }

        private void ClearSlot(SlotUi ui)
        {
            ui.Fleet.Normalize(ui.Fleet.ShipIds.Count);
            ui.Fleet.ShipIds[ui.Index] = 0;
            if (ui.Fleet.Equips != null) ui.Fleet.Equips[ui.Index] = null;
            if (ui.Fleet.Retrofits != null) ui.Fleet.Retrofits[ui.Index] = false;
            RefreshSlot(ui);
        }

        // ================= 舰队增删 =================

        private void AddSurfaceFleet_Click(object sender, RoutedEventArgs e)
        {
            if (_draft == null || _draft.Surface.Count >= Team.MaxSurfaceFleets) return;
            string[] names = { "一队", "二队", "三队" };
            _draft.Surface.Add(new Fleet(names[Math.Min(_draft.Surface.Count, names.Length - 1)], Fleet.SurfaceSlots));
            RebuildEditor();
        }

        private void DeleteSurfaceFleet(int index)
        {
            if (_draft == null || index < 0 || index >= _draft.Surface.Count) return;
            if (_draft.Surface.Count <= 1)
            {
                TeamStatusText.Text = L("TeamPage_Error_KeepOneFleet", "至少保留 1 支水面舰队。");
                return;
            }
            _draft.Surface.RemoveAt(index);
            RebuildEditor();
        }

        // ================= 新建 / 装载 / 保存 / 删除 =================

        private void StartNewDraft()
        {
            _draft = new Team
            {
                Surface = new List<Fleet> { new("一队", Fleet.SurfaceSlots) },
                Submarine = new Fleet("水下舰队", Fleet.SmallSlots),
                Support = new Fleet("支援舰队", Fleet.SmallSlots),
                SupportKind = "carrier"
            };
            _draft.Normalize();
            TeamNameBox.Text = "";
            TeamDescBox.Text = "";
            TeamStatusText.Text = "";
            SaveTeamButton.Content = L("TeamPage_SaveButton.Content", "保存配队");
            DeleteTeamButton.IsEnabled = false;
            RebuildEditor();
        }

        private void LoadDraft(Team team)
        {
            _draft = team;
            _draft.Normalize();
            TeamNameBox.Text = team.Name;
            TeamDescBox.Text = team.Description;
            TeamStatusText.Text = "";
            SaveTeamButton.Content = L("TeamPage_SaveChanges", "保存修改");
            DeleteTeamButton.IsEnabled = true;
            RebuildEditor();
        }

        private void NewTeamButton_Click(object sender, RoutedEventArgs e)
        {
            TeamListView.SelectedItem = null;
            StartNewDraft();
        }

        private void TeamListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TeamListView.SelectedItem is Team team) LoadDraft(team);
        }

        private async void SaveTeamButton_Click(object sender, RoutedEventArgs e)
        {
            var mgr = _app?.ShipManager;
            if (mgr == null || _draft == null) return;

            string name = TeamNameBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(name))
            {
                TeamStatusText.Text = L("TeamPage_Error_NameEmpty", "队伍名称不能为空。");
                return;
            }

            _draft.Normalize();
            var ids = _draft.AllShipIds.ToList();
            if (ids.Count == 0)
            {
                TeamStatusText.Text = L("TeamPage_Error_NoShips", "请至少选择一个舰船位置。");
                return;
            }
            if (ids.Distinct().Count() != ids.Count)
            {
                TeamStatusText.Text = L("TeamPage_Error_Duplicate", "同一舰船不能重复编入多个位置。");
                return;
            }

            _draft.Name = name;
            _draft.Description = TeamDescBox.Text ?? "";

            try
            {
                if (_draft.Id == 0) await mgr.AddTeamAsync(_draft);
                else await mgr.UpdateTeamAsync(_draft);

                TeamStatusText.Text = L("TeamPage_SaveOk", "已保存。");
                TeamListView.SelectedItem = _draft;
                RefreshTeamList();
            }
            catch (Exception ex)
            {
                TeamStatusText.Text = string.Format(L("TeamPage_SaveFail", "保存失败：{0}"), ex.Message);
            }
        }

        private async void DeleteTeamButton_Click(object sender, RoutedEventArgs e)
        {
            var mgr = _app?.ShipManager;
            if (mgr == null || _draft == null || _draft.Id == 0) return;

            var confirm = new ContentDialog
            {
                Title = L("TeamPage_DeleteTitle", "删除配队"),
                Content = string.Format(L("TeamPage_DeleteMessage", "确认删除配队“{0}”？"), _draft.Name),
                PrimaryButtonText = L("TeamPage_DeletePrimary", "删除"),
                CloseButtonText = L("TeamPage_Cancel", "取消"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await mgr.DeleteTeamAsync(_draft.Id);
                TeamListView.SelectedItem = null;
                StartNewDraft();
            }
            catch (Exception ex)
            {
                TeamStatusText.Text = string.Format(L("TeamPage_DeleteFail", "删除失败：{0}"), ex.Message);
            }
        }

        // ================= 分享：分享码 / 导入 =================

        private async void ShareCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_draft == null) return;
            string code = TeamShareCode.Encode(BuildShareableTeam());
            try
            {
                var package = new DataPackage();
                package.SetText(code);
                Clipboard.SetContent(package);
            }
            catch { }

            var box = new TextBox
            {
                Text = code,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                FontFamily = new FontFamily("Consolas"),
                Height = 140
            };
            var dialog = new ContentDialog
            {
                Title = L("TeamPage_ShareCodeTitle", "分享码（已复制）"),
                Content = box,
                PrimaryButtonText = L("TeamPage_Close", "关闭"),
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
            TeamStatusText.Text = L("TeamPage_ShareCodeDone", "分享码已生成并复制到剪贴板。");
        }

        private async void ImportCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var input = new TextBox
            {
                PlaceholderText = L("TeamPage_ImportPlaceholder", "粘贴分享码"),
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 140,
                FontFamily = new FontFamily("Consolas")
            };
            var dialog = new ContentDialog
            {
                Title = L("TeamPage_ImportTitle", "从分享码导入"),
                Content = input,
                PrimaryButtonText = L("TeamPage_ImportButton", "导入"),
                CloseButtonText = L("TeamPage_Cancel", "取消"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var team = TeamShareCode.Decode(input.Text ?? "");
            if (team == null)
            {
                TeamStatusText.Text = L("TeamPage_ImportInvalid", "分享码无效或已损坏。");
                return;
            }

            team.Normalize();
            int missing = 0;
            foreach (var fleet in team.AllFleets)
            {
                for (int i = 0; i < fleet.ShipIds.Count; i++)
                {
                    if (fleet.ShipIds[i] <= 0) continue;
                    if (FindVm(fleet.ShipIds[i]) != null) continue;
                    fleet.ShipIds[i] = 0;
                    if (fleet.Equips != null) fleet.Equips[i] = null;
                    if (fleet.Retrofits != null) fleet.Retrofits[i] = false;
                    missing++;
                }
            }
            if (missing > 0)
                TeamStatusText.Text = string.Format(L("TeamPage_ImportMissing", "分享码中有 {0} 个槽位的舰船在当前数据库里找不到，已清空。"), missing);

            team.Id = 0;
            TeamListView.SelectedItem = null;
            LoadDraft(team);
            TeamNameBox.Text = string.IsNullOrWhiteSpace(team.Name) ? L("TeamPage_ImportDefaultName", "导入的配队") : team.Name;
            SaveTeamButton.Content = L("TeamPage_SaveButton.Content", "保存配队");
            DeleteTeamButton.IsEnabled = false;
        }

        /// <summary>用于分享的冻结副本（含当前编辑中的名称）。</summary>
        private Team BuildShareableTeam()
        {
            _draft!.Normalize();
            var team = _draft.Clone();
            team.Name = TeamNameBox.Text?.Trim() is { Length: > 0 } n ? n : _draft.Name;
            team.Description = TeamDescBox.Text ?? "";
            return team;
        }

        // ================= 分享图片 =================

        private async void ShareImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_draft == null) return;
            try
            {
                var images = new List<BitmapImage>();
                var panel = BuildShareCard(images);
                ShareRenderHost.Children.Clear();
                ShareRenderHost.Children.Add(panel);
                ShareRenderHost.UpdateLayout();

                await WaitForImagesAsync(images);

                var bitmap = new RenderTargetBitmap();
                await bitmap.RenderAsync(panel);

                var picker = new FileSavePicker();
                var window = _app?.GetMainWindow();
                if (window != null) InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
                picker.FileTypeChoices.Add("PNG 图片", new[] { ".png" });
                picker.SuggestedFileName = string.IsNullOrWhiteSpace(_draft.Name) ? "team" : _draft.Name;
                var file = await picker.PickSaveFileAsync();
                if (file == null) return;

                var buffer = await bitmap.GetPixelsAsync();
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                        (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, buffer.ToArray());
                    await encoder.FlushAsync();
                }

                try
                {
                    var dataPackage = new DataPackage();
                    dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
                    Clipboard.SetContent(dataPackage);
                }
                catch { }

                TeamStatusText.Text = string.Format(L("TeamPage_ShareImageDone", "已导出图片并复制到剪贴板：{0}"), file.Path);
            }
            catch (Exception ex)
            {
                TeamStatusText.Text = string.Format(L("TeamPage_ShareImageFail", "导出图片失败：{0}"), ex.Message);
            }
            finally
            {
                ShareRenderHost.Children.Clear();
            }
        }

        /// <summary>等待所有 BitmapImage 解码完成（1.5s 超时），否则 RenderTargetBitmap 会渲染出空白。</summary>
        private static async Task WaitForImagesAsync(List<BitmapImage> images)
        {
            var tasks = new List<Task>();
            foreach (var img in images)
            {
                var tcs = new TaskCompletionSource<bool>();
                img.ImageOpened += (s, e) => tcs.TrySetResult(true);
                img.ImageFailed += (s, e) => tcs.TrySetResult(false);
                tasks.Add(tcs.Task);
            }
            if (tasks.Count == 0) return;
            await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(1500));
        }

        private StackPanel BuildShareCard(List<BitmapImage> images)
        {
            var panel = new StackPanel
            {
                Width = 760,
                Padding = new Thickness(24),
                Spacing = 10,
                Background = ResBrush("SolidBackgroundFillColorBaseBrush")
            };

            panel.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(TeamNameBox.Text) ? "AzurLaneDex" : TeamNameBox.Text,
                FontSize = 22,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            if (!string.IsNullOrWhiteSpace(TeamDescBox.Text))
                panel.Children.Add(new TextBlock { Text = TeamDescBox.Text, FontSize = 12, Foreground = SecondaryBrush, TextWrapping = TextWrapping.Wrap });

            var team = BuildShareableTeam();
            for (int f = 0; f < team.Surface.Count; f++)
                panel.Children.Add(BuildShareFleetGroup(team.Surface[f], $"水面舰队 · {FleetTitle(team.Surface[f])}", images));

            panel.Children.Add(BuildShareFleetGroup(team.Submarine, "水下舰队", images));
            panel.Children.Add(BuildShareFleetGroup(team.Support,
                team.SupportKindValue == SupportFleetKind.Carrier ? "支援舰队 · 航母支援" : "支援舰队 · 潜艇支援", images));

            panel.Children.Add(new TextBlock
            {
                Text = "AzurLaneDex 配队分享",
                FontSize = 11,
                Foreground = SecondaryBrush,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return panel;
        }

        private StackPanel BuildShareFleetGroup(Fleet fleet, string title, List<BitmapImage> images)
        {
            var group = new StackPanel { Spacing = 6 };
            group.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 6, 0, 0)
            });

            bool any = false;
            for (int i = 0; i < fleet.ShipIds.Count; i++)
            {
                int id = fleet.ShipIds[i];
                if (id <= 0) continue;
                var vm = FindVm(id);
                if (vm == null) continue;
                any = true;
                bool retro = fleet.Retrofits != null && i < fleet.Retrofits.Count && fleet.Retrofits[i];
                group.Children.Add(BuildShareShipRow(vm, retro, fleet, i, images));
            }
            if (!any)
                group.Children.Add(new TextBlock { Text = L("TeamPage_ShareNoShip", "（空）"), FontSize = 12, Foreground = SecondaryBrush });
            return group;
        }

        private Grid BuildShareShipRow(ShipViewModel vm, bool retro, Fleet fleet, int slotIndex, List<BitmapImage> images)
        {
            var row = new Grid { ColumnSpacing = 10 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var avatarHost = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(4) };
            var avatarGrid = new Grid();
            AddShareImage(avatarGrid, SafeUriOf(() => vm.AvatarBgUriFor(retro)), images);
            AddShareImage(avatarGrid, SafeUriOf(() => vm.AvatarUriFor(retro)), images);
            avatarHost.Child = avatarGrid;
            Grid.SetColumn(avatarHost, 0);
            row.Children.Add(avatarHost);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 3 };
            text.Children.Add(new TextBlock
            {
                Text = vm.DisplayName + (retro ? "（改造）" : ""),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });

            var equipRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var map = fleet.Equips != null && slotIndex < fleet.Equips.Count ? fleet.Equips[slotIndex] : null;
            if (map != null && map.Count > 0)
            {
                foreach (var kv in map.OrderBy(k => k.Key))
                {
                    int equipId = kv.Value;
                    int iconId = EquipmentCatalog.IconIdOf(equipId);
                    if (iconId <= 0) iconId = equipId;

                    var icon = new Image { Width = 22, Height = 22, Stretch = Stretch.Uniform };
                    var bitmap = new BitmapImage();
                    try
                    {
                        bitmap.UriSource = new Uri($"ms-appx:///Assets/icon/item/equips/{iconId}.png");
                        images.Add(bitmap);
                        icon.Source = bitmap;
                    }
                    catch { }
                    equipRow.Children.Add(icon);
                    equipRow.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrEmpty(EquipmentCatalog.NameOf(equipId)) ? $"#{equipId}" : EquipmentCatalog.NameOf(equipId),
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = SecondaryBrush
                    });
                }
            }
            else
            {
                equipRow.Children.Add(new TextBlock { Text = L("TeamPage_ShareNoEquip", "未配装"), FontSize = 11, Foreground = SecondaryBrush });
            }
            text.Children.Add(equipRow);

            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            return row;
        }

        private void AddShareImage(Grid host, string? uri, List<BitmapImage> images)
        {
            if (string.IsNullOrEmpty(uri)) return;
            try
            {
                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(uri);
                images.Add(bitmap);
                host.Children.Add(new Image { Source = bitmap, Stretch = Stretch.UniformToFill });
            }
            catch { }
        }

        private static string? SafeUriOf(Func<string> getter)
        {
            try { return getter(); } catch { return null; }
        }

        // ================= 辅助 =================

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