using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using Windows.ApplicationModel.Resources;

namespace AzurLaneDex.Views
{
    public sealed partial class AddShipDialog : ContentDialog
    {
        private ShipStatic _editingShip;
        private bool _isEditMode;
        private bool _isLoadedInitialized = false;
        private int _initRetryCount = 0;
        private const int MaxInitRetries = 5;

        // 列表绑定集合
        public ObservableCollection<AcquisitionMethod> AcquisitionMethods { get; set; } = new();
        public ObservableCollection<EquipmentSlot> EquipmentSlots { get; set; } = new();
        public ObservableCollection<InitialEquipment> InitialEquipmentItems { get; set; } = new();
        public ObservableCollection<Skill> Skills { get; set; } = new();
        public ObservableCollection<Skin> Skins { get; set; } = new();
        public ObservableCollection<VoiceLine> Lines { get; set; } = new();
        public ObservableCollection<GiftPreference> GiftPreferences { get; set; } = new();

        private List<ComboBoxItem> _normalFactionItems;
        private List<ComboBoxItem> _collabFactionItems;
        private List<ComboBoxItem> _metaFactionItems;
        private List<ComboBoxItem> _researchFactionItems;
        private List<ComboBoxItem> _normalRarityItems;
        private List<ComboBoxItem> _researchRarityItems;
        public List<AcquisitionMethodType> TypeOptions { get; } = Enum.GetValues(typeof(AcquisitionMethodType)).Cast<AcquisitionMethodType>().ToList();
        public List<ConstructionPool> PoolOptions { get; } = Enum.GetValues(typeof(ConstructionPool)).Cast<ConstructionPool>().ToList();
        public List<ExchangeShop> ShopOptions { get; } = Enum.GetValues(typeof(ExchangeShop)).Cast<ExchangeShop>().ToList();
        public List<Faction> FactionOptions { get; } = Enum.GetValues(typeof(Faction)).Cast<Faction>().ToList();

        private void SubscribeMethod(AcquisitionMethod method)
        {
            method.PropertyChanged += OnMethodPropertyChanged;
        }

        private void UnsubscribeMethod(AcquisitionMethod method)
        {
            method.PropertyChanged -= OnMethodPropertyChanged;
        }

        private void OnMethodPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AcquisitionMethod.Type))
            {
                var method = sender as AcquisitionMethod;
                if (method != null && AcquisitionMethods.Contains(method))
                {
                    // 重新插入以强制刷新 UI
                    int index = AcquisitionMethods.IndexOf(method);
                    AcquisitionMethods.RemoveAt(index);
                    AcquisitionMethods.Insert(index, method);
                }
            }
        }
        private LocalizedString StringToLocalizedString(string value)
        {
            var result = new LocalizedString();
            if (!string.IsNullOrEmpty(value))
            {
                var parts = value.Split('|');
                if (parts.Length > 0) result["zh-Hans"] = parts[0].Trim();
                if (parts.Length > 1) result["zh-Hant"] = parts[1].Trim();
                if (parts.Length > 2) result["en"] = parts[2].Trim();
                if (parts.Length > 3) result["ja"] = parts[3].Trim();
            }
            return result;
        }

        private string LocalizedStringToString(LocalizedString loc)
        {
            if (loc == null || loc.Count == 0) return "";
            return string.Join("|", loc.Values);
        }

        public AddShipDialog(ShipStatic editShip = null)
        {
            this.InitializeComponent();
            _editingShip = editShip;
            _isEditMode = editShip != null;

            this.Title = _isEditMode ? "编辑舰船" : "新建舰船";

            // 绑定列表控件
            AcquisitionMethodsItemsControl.ItemsSource = AcquisitionMethods;
            EquipmentSlotsItemsControl.ItemsSource = EquipmentSlots;
            InitialEquipmentItemsControl.ItemsSource = InitialEquipmentItems;
            SkillsItemsControl.ItemsSource = Skills;
            SkinsItemsControl.ItemsSource = Skins;
            LinesItemsControl.ItemsSource = Lines;
            GiftPreferencesItemsControl.ItemsSource = GiftPreferences;

            CategoryCombo.SelectionChanged += CategoryCombo_SelectionChanged;
            this.Loaded += OnLoaded1;
        }

        private void OnLoaded1(object sender, RoutedEventArgs e)
        {
            if (FactionCombo == null || RarityCombo == null || ShipTypeCombo == null)
            {
                _initRetryCount++;
                if (_initRetryCount <= MaxInitRetries)
                {
                    var dispatcher = DispatcherQueue.GetForCurrentThread();
                    dispatcher.TryEnqueue(() => OnLoaded1(sender, e));
                    return;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("警告：关键 ComboBox 未加载，使用默认值。");
                    CreateFallbackComboBoxItems();
                    if (_isEditMode)
                        LoadShipData(_editingShip);
                    else
                        SetDefaultValues();
                    return;
                }
            }

            _initRetryCount = 0;
            InitializeComboBoxes();

            if (_isEditMode)
                LoadShipData(_editingShip);
            else
                SetDefaultValues();
        }

        private void CreateFallbackComboBoxItems()
        {
            // 用有效枚举值创建默认项
            var defaultFaction = new ComboBoxItem { Content = "其他", Tag = Faction.Other.ToString() };
            var defaultRarity = new ComboBoxItem { Content = "普通", Tag = Rarity.N.ToString() };
            var defaultType = new ComboBoxItem { Content = "未知", Tag = ShipType.UNKNOWN.ToString() };

            _normalFactionItems = new List<ComboBoxItem> { defaultFaction };
            _collabFactionItems = new List<ComboBoxItem> { defaultFaction };
            _metaFactionItems = new List<ComboBoxItem> { defaultFaction };
            _researchFactionItems = new List<ComboBoxItem> { defaultFaction };

            FactionCombo.ItemsSource = _normalFactionItems;
            FactionCombo.SelectedIndex = 0;

            RarityCombo.ItemsSource = new List<ComboBoxItem> { defaultRarity };
            RarityCombo.SelectedIndex = 0;

            ShipTypeCombo.ItemsSource = new List<ComboBoxItem> { defaultType };
            ShipTypeCombo.SelectedIndex = 0;

            // 同样处理 TargetShipTypeCombo
            TargetShipTypeCombo.ItemsSource = new List<ComboBoxItem> { defaultType };
            TargetShipTypeCombo.SelectedIndex = 0;
        }
        private void InitializeComboBoxes()
        {
            if (FactionCombo == null || RarityCombo == null || ShipTypeCombo == null)
            {
                System.Diagnostics.Debug.WriteLine("One or more combo boxes are null, skipping initialization.");
                return;
            }
            _normalFactionItems = new List<ComboBoxItem>();
            _collabFactionItems = new List<ComboBoxItem>();
            _metaFactionItems = new List<ComboBoxItem>();
            _researchFactionItems = new List<ComboBoxItem>();
            try
            {

                // ====== 动态加载阵营（Faction） ======
                foreach (Faction faction in Enum.GetValues(typeof(Faction)))
                {
                    if (faction == Faction.Universal) continue;
                    int id = (int)faction;

                    // 跳过非正式范围
                    if (id >= 400 && id < 1000) continue;

                    var item = new ComboBoxItem();
                    try
                    {
                        item.Content = LocalizationHelper.GetEnumString("Faction", id);
                    }
                    catch
                    {
                        item.Content = faction.ToString(); // 降级显示
                    }
                    item.Tag = faction.ToString();

                    if (id >= 1 && id < 100) // 普通阵营 1-99
                    {
                        _normalFactionItems.Add(item);
                        _researchFactionItems.Add(item); // 科研也使用普通阵营
                    }
                    else if (id >= 100 && id < 200) // 联动 100-199
                    {
                        _collabFactionItems.Add(item);
                    }
                    else if (id >= 200 && id < 300) // META 200-299
                    {
                        _metaFactionItems.Add(item);
                    }
                }

                FactionCombo.ItemsSource = _normalFactionItems;
                if (_normalFactionItems.Any())
                    FactionCombo.SelectedIndex = 0;

                // ====== 动态加载稀有度（Rarity） ======
                var rarityItems = new List<ComboBoxItem>();
                foreach (Rarity rarity in Enum.GetValues(typeof(Rarity)))
                {
                    if (rarity == Rarity.T1) continue;
                    if (rarity == Rarity.Unknown) continue;

                    var item = new ComboBoxItem();
                    item.Content = LocalizationHelper.GetEnumString("Rarity", (int)rarity);
                    item.Tag = rarity.ToString();
                    rarityItems.Add(item);
                }
                RarityCombo.ItemsSource = rarityItems;
                if (rarityItems.Any())
                    RarityCombo.SelectedIndex = 0;


                // ====== 动态加载舰种（ShipType） ======
                var shipTypeItems = new List<ComboBoxItem>();
                foreach (ShipType shipType in Enum.GetValues(typeof(ShipType)))
                {
                    if (shipType == ShipType.UNKNOWN) continue;

                    var item = new ComboBoxItem();
                    try
                    {
                        item.Content = LocalizationHelper.GetEnumString("ShipType", (int)shipType);
                    }
                    catch
                    {
                        item.Content = shipType.ToString();
                    }
                    item.Tag = shipType.ToString();
                    shipTypeItems.Add(item);
                }
                ShipTypeCombo.ItemsSource = shipTypeItems;
                if (shipTypeItems.Any())
                    ShipTypeCombo.SelectedIndex = 0;

                var targetShipTypeItems = new List<ComboBoxItem>();
                foreach (ShipType shipType in Enum.GetValues(typeof(ShipType)))
                {
                    if (shipType == ShipType.UNKNOWN) continue;
                    var item = new ComboBoxItem();
                    item.Content = LocalizationHelper.GetEnumString("ShipType", (int)shipType);
                    item.Tag = shipType.ToString();
                    targetShipTypeItems.Add(item);
                }
                TargetShipTypeCombo.ItemsSource = targetShipTypeItems;
                if (targetShipTypeItems.Any())
                    TargetShipTypeCombo.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeComboBoxes 异常: {ex.Message}");
                // 确保列表不为 null 且至少有一个默认项
                var defaultItem = new ComboBoxItem { Content = "其他", Tag = Faction.Other.ToString() };
                _normalFactionItems = new List<ComboBoxItem> { defaultItem };
                _collabFactionItems = new List<ComboBoxItem> { defaultItem };
                _metaFactionItems = new List<ComboBoxItem> { defaultItem };
                _researchFactionItems = new List<ComboBoxItem> { defaultItem };
                FactionCombo.ItemsSource = _normalFactionItems;
                FactionCombo.SelectedIndex = 0;
            }
        }

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = CategoryCombo.SelectedItem as ComboBoxItem;
            if (selected?.Tag is string tag && Enum.TryParse<ShipCategory>(tag, out var category))
            {
                UpdateCategoryDependentControls(category);
            }
        }

        private void UpdateCategoryDependentControls(ShipCategory category)
        {
            if (_normalFactionItems == null)
            {
                InitializeComboBoxes();
                // 如果仍然为 null，则直接返回（避免崩溃）
                if (_normalFactionItems == null) return;
            }

            if (FactionCombo == null) return;

            switch (category)
            {
                case ShipCategory.Normal:
                    FactionCombo.ItemsSource = _normalFactionItems;
                    break;
                case ShipCategory.Collab:
                    FactionCombo.ItemsSource = _collabFactionItems;
                    break;
                case ShipCategory.META:
                    FactionCombo.ItemsSource = _metaFactionItems;
                    break;
                case ShipCategory.Research:
                    FactionCombo.ItemsSource = _researchFactionItems;
                    break;
                default:
                    FactionCombo.ItemsSource = _normalFactionItems;
                    break;
            }

            // 确保选中第一项（如果当前没有有效选中）
            if (FactionCombo.SelectedIndex < 0 && FactionCombo.ItemsSource is IEnumerable<ComboBoxItem> items && items.Any())
            {
                FactionCombo.SelectedIndex = 0;
            }
        }

        private void SetDefaultValues()
        {
            GlobalNameBox.Text = "";
            ReleaseDatePicker.Date = DateTimeOffset.Now;
            RetrofitDatePicker.Date = DateTimeOffset.Now;
            GearDatePicker.Date = DateTimeOffset.Now;
            AcquisitionMethods.Clear();
            var method = new AcquisitionMethod
            {
                Type = AcquisitionMethodType.Construction,
                Pool = ConstructionPool.Light,
                CostCube = 1,
                CostGold = 600,
                BuildTime = "00:30:00",
                IsPrimary = true
            };
            SubscribeMethod(method);
            AcquisitionMethods.Add(method);
        }

        private void LoadShipData(ShipStatic ship)
        {
            CategoryCombo.SelectionChanged -= CategoryCombo_SelectionChanged;

            CategoryCombo.SelectedItem = CategoryCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Tag?.ToString() == ship.Category.ToString());

            UpdateCategoryDependentControls(ship.Category);

            CategoryCombo.SelectionChanged += CategoryCombo_SelectionChanged;

            var categoryItem = CategoryCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Tag?.ToString() == ship.Category.ToString());
            if (categoryItem != null)
                CategoryCombo.SelectedItem = categoryItem;
            else
                CategoryCombo.SelectedIndex = 0;

            // 基本信息
            IdBox.Value = ship.Id;
            GameOrderBox.Value = ship.GameOrder;
            GlobalNameBox.Text = ship.GlobalName;
            NameZhBox.Text = ship.Name.GetValueOrDefault("zh-Hans");
            NameZhHantBox.Text = ship.Name.GetValueOrDefault("zh-Hant");
            NameEnBox.Text = ship.Name.GetValueOrDefault("en");
            NameJaBox.Text = ship.Name.GetValueOrDefault("ja");

            AltNameBox.Text = ship.AltName.GetValueOrDefault("zh-Hans");
            AliasBox.Text = ship.Alias;
            ClassZhBox.Text = ship.Class.GetValueOrDefault("zh-Hans");
            ClassZhHantBox.Text = ship.Class.GetValueOrDefault("zh-Hant");
            ClassEnBox.Text = ship.Class.GetValueOrDefault("en");
            ClassJaBox.Text = ship.Class.GetValueOrDefault("ja");

            CategoryCombo.SelectedItem = CategoryCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Tag?.ToString() == ship.Category.ToString());
            UpdateCategoryDependentControls(ship.Category);

            ShipTypeCombo.SelectedItem = ShipTypeCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Tag?.ToString() == ship.Type.ToString());
            RarityCombo.SelectedItem = RarityCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Tag?.ToString() == ship.Rarity.ToString());
            FactionCombo.SelectedItem = FactionCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Tag?.ToString() == ship.Faction.ToString());

            if (!string.IsNullOrEmpty(ship.ReleaseDate) && DateTime.TryParse(ship.ReleaseDate, out var rd))
                ReleaseDatePicker.Date = rd;

            IsPermanentCheckBox.IsChecked = ship.IsPermanent;

            CvZhBox.Text = ship.CV.GetValueOrDefault("zh-Hans");
            CvZhHantBox.Text = ship.CV.GetValueOrDefault("zh-Hant");
            CvEnBox.Text = ship.CV.GetValueOrDefault("en");
            CvJaBox.Text = ship.CV.GetValueOrDefault("ja");
            ArtistBox.Text = ship.Artist;

            // 改造日期
            if (!string.IsNullOrEmpty(ship.Retrofit?.RetrofitReleaseDate) && DateTime.TryParse(ship.Retrofit.RetrofitReleaseDate, out var rrd))
                RetrofitDatePicker.Date = rrd;

            // 相关活动
            RelatedEventBox.Text = ship.RelatedEvent;

            // 属性
            HpBox.Value = ship.Stats.Hp;
            ArmorCombo.SelectedItem = ArmorCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Tag?.ToString() == ship.Stats.Armor.ToString());
            FpBox.Value = ship.Stats.Fp;
            TrpBox.Value = ship.Stats.Trp;
            AaBox.Value = ship.Stats.Aa;
            AviBox.Value = ship.Stats.Avi;
            HitBox.Value = ship.Stats.Hit;
            EvaBox.Value = ship.Stats.Eva;
            AswBox.Value = ship.Stats.Asw;
            LuckBox.Value = ship.Stats.Luck;
            OilBox.Value = ship.Stats.Oil;
            SpeedBox.Value = ship.Stats.Speed;

            // 性能评级
            HpGradeCombo.SelectedItem = HpGradeCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Content?.ToString() == ship.Performance.Hp.ToString());
            AaGradeCombo.SelectedItem = AaGradeCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Content?.ToString() == ship.Performance.Aa.ToString());
            EvaGradeCombo.SelectedItem = EvaGradeCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Content?.ToString() == ship.Performance.Eva.ToString());
            AviGradeCombo.SelectedItem = AviGradeCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Content?.ToString() == ship.Performance.Avi.ToString());
            TrpGradeCombo.SelectedItem = TrpGradeCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Content?.ToString() == ship.Performance.Trp.ToString());
            FpGradeCombo.SelectedItem = FpGradeCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Content?.ToString() == ship.Performance.Fp.ToString());

            // 舰队科技
            CollectPointsBox.Value = ship.FleetTech.CollectPoints;
            LimitBreakPointsBox.Value = ship.FleetTech.LimitBreakPoints;
            Level120PointsBox.Value = ship.FleetTech.Level120Points;

            // 获取方式
            foreach (var method in AcquisitionMethods)
            {
                UnsubscribeMethod(method);
            }
            AcquisitionMethods.Clear();
            foreach (var method in ship.Acquisition.Methods)
            {
                SubscribeMethod(method);
                AcquisitionMethods.Add(method);
            }

            // 装备槽
            EquipmentSlots.Clear();
            foreach (var slot in ship.EquipmentSlots)
                EquipmentSlots.Add(slot);

            // 初始装备
            InitialEquipmentItems.Clear();
            foreach (var ie in ship.InitialEquipment)
                InitialEquipmentItems.Add(ie);

            // 专属兵装
            if (ship.SpecialGear != null)
            {
                HasSpecialGearCheck.IsChecked = true;
                GearNameZhBox.Text = ship.SpecialGear.Name.GetValueOrDefault("zh-Hans");
                GearNameZhHantBox.Text = ship.SpecialGear.Name.GetValueOrDefault("zh-Hant");
                GearNameEnBox.Text = ship.SpecialGear.Name.GetValueOrDefault("en");
                GearNameJaBox.Text = ship.SpecialGear.Name.GetValueOrDefault("ja");
                if (!string.IsNullOrEmpty(ship.SpecialGear.ReleaseDate) && DateTime.TryParse(ship.SpecialGear.ReleaseDate, out var gd))
                    GearDatePicker.Date = gd;
                GearIdBox.Value = ship.SpecialGear.Id;
                GearAcquireBox.Text = ship.SpecialGear.AcquisitionMethod;
            }
            else
            {
                HasSpecialGearCheck.IsChecked = false;
            }

            // 技能
            Skills.Clear();
            foreach (var skill in ship.Skills)
            {
                // 确保 Name、Description、LevelValues 不为 null
                if (skill.Name == null) skill.Name = new LocalizedString();
                if (skill.Description == null) skill.Description = new LocalizedString();
                if (skill.LevelValues == null) skill.LevelValues = new List<List<string>>();
                Skills.Add(skill);
            }

            // 改造
            CanRetrofitCheck.IsChecked = ship.Retrofit?.CanRetrofit ?? false;
            ShipTypeChangedCheck.IsChecked = ship.Retrofit?.ShipTypeChanged ?? false;
            TargetShipTypeCombo.SelectedItem = TargetShipTypeCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Tag?.ToString() == ship.Retrofit?.TargetShipType.ToString());
            RetrofitNodesBox.Text = ship.Retrofit != null && ship.Retrofit.Nodes.Any()
                ? string.Join("\n", ship.Retrofit.Nodes.Select(n =>
                    $"{n.Name.GetLocalized()}|{string.Join(",", n.AttributeBonus.Select(kv => $"{kv.Key}={kv.Value}"))}|{string.Join(",", n.RequiredItems)}|{n.RequiredCoins}|{n.RequiredLevel}|{n.RequiredStars}"))
                : "";

            // 科研
            PreReqFactionsBox.Text = ship.Research.PreRequisiteFactions != null ? string.Join(";", ship.Research.PreRequisiteFactions) : "";
            TechPointsBox.Value = ship.Research.TechPoints;
            ResearchTasksBox.Text = ship.Research.Tasks != null ? string.Join("\n", ship.Research.Tasks.Select(t => $"{t.Name}|{t.Description}|{t.Requirement}")) : "";
            BlueprintRequiredBox.Value = ship.Research.BlueprintRequired;
            DevelopBonusBox.Text = ship.Research.DevelopBonus != null ? string.Join("\n", ship.Research.DevelopBonus.Select(kv => $"{kv.Key}={kv.Value}")) : "";
            DevelopBlueprintRequiredBox.Value = ship.Research.DevelopBlueprintRequired;
            HasFateSimCheck.IsChecked = ship.Research.HasFateSimulation;
            if (ship.Research.HasFateSimulation && ship.Research.FateSim != null)
            {
                FateLevelBox.Value = ship.Research.FateSim.Level;
                FateDescBox.Text = ship.Research.FateSim.Description;
                FateBlueprintRequiredBox.Value = ship.Research.FateSim.BlueprintRequired;
            }

            // 皮肤
            Skins.Clear();
            foreach (var skin in ship.Skins)
            {
                if (skin.Name == null) skin.Name = new LocalizedString();
                Skins.Add(skin);
            }

            // 台词
            Lines.Clear();
            foreach (var line in ship.Lines)
            {
                if (line.Content == null) line.Content = new LocalizedString();
                Lines.Add(line);
            }

            // 礼物偏好
            GiftPreferences.Clear();
            foreach (var gp in ship.GiftPreferences)
                GiftPreferences.Add(gp);

            // 强化/退役
            CanBeEnhanceMaterialCheck.IsChecked = ship.CanBeEnhanceMaterial;
            EnhanceValueBox.Value = ship.EnhanceValue;
            CanRetireCheck.IsChecked = ship.CanRetire;
            RetirementRewardBox.Text = ship.RetirementReward;
            EnhanceExpFp.Value = ship.EnhanceExp.Fp;
            EnhanceExpTrp.Value = ship.EnhanceExp.Trp;
            EnhanceExpAvi.Value = ship.EnhanceExp.Avi;
            EnhanceExpRld.Value = ship.EnhanceExp.Rld;
            EnhanceItemsBox.Text = ship.EnhanceItems != null ? string.Join(";", ship.EnhanceItems) : "";
            ExtraEnhanceBox.Text = ship.ExtraEnhance;

            // 备注与引用
            RemarksBox.Text = ship.Remarks;
            NotesBox.Text = ship.Notes;
            ReferenceMarkdownBox.Text = ship.ReferenceMarkdown;

            // 重新设置阵营和稀有度的选中项
            if (!string.IsNullOrEmpty(ship.Faction.ToString()))
            {
                var factionItem = FactionCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Tag?.ToString() == ship.Faction.ToString());
                if (factionItem != null)
                    FactionCombo.SelectedItem = factionItem;
            }
            if (!string.IsNullOrEmpty(ship.Rarity.ToString()))
            {
                var rarityItem = RarityCombo.Items.FirstOrDefault(c => (c as ComboBoxItem)?.Tag?.ToString() == ship.Rarity.ToString());
                if (rarityItem != null)
                    RarityCombo.SelectedItem = rarityItem;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) { }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(NameZhBox.Text))
            {
                args.Cancel = true;
                ShowError("简体中文名称不能为空");
            }
        }

        // ====== 获取方式列表操作 ======
        private void AddAcquisitionMethod_Click(object sender, RoutedEventArgs e)
        {
            var method = new AcquisitionMethod
            {
                Type = AcquisitionMethodType.Construction,
                Pool = ConstructionPool.Light,
                CostCube = 1,
                CostGold = 600,
                BuildTime = "00:30:00",
                IsPrimary = !AcquisitionMethods.Any(m => m.IsPrimary)
            };
            SubscribeMethod(method);
            AcquisitionMethods.Add(method);
        }

        private void RemoveMethod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AcquisitionMethod method)
            {
                UnsubscribeMethod(method);
                AcquisitionMethods.Remove(method);
                // 如果删除的是主方法，则将第一个设为 true
                if (!AcquisitionMethods.Any(m => m.IsPrimary) && AcquisitionMethods.Count > 0)
                    AcquisitionMethods[0].IsPrimary = true;
            }
        }

        private void OnMethodTypeChanged(object sender, SelectionChangedEventArgs e)
        {
            /*
            var combo = sender as ComboBox;
            if (combo?.DataContext is AcquisitionMethod method && combo.SelectedItem is ComboBoxItem item)
            {
                if (Enum.TryParse<AcquisitionMethodType>(item.Tag?.ToString(), out var newType))
                    method.Type = newType;
            }
            */
        }

        private void AddDropLocation_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is AcquisitionMethod method)
            {
                if (method.Locations == null) method.Locations = new List<DropLocation>();
                method.Locations.Add(new DropLocation { Map = "" });
                RefreshMethod(method);
            }
        }

        private void RemoveDropLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DropLocation loc)
            {
                var parentMethod = AcquisitionMethods.FirstOrDefault(m => m.Locations?.Contains(loc) == true);
                if (parentMethod != null)
                {
                    parentMethod.Locations.Remove(loc);
                    RefreshMethod(parentMethod);
                }
            }
        }

        private void AddExchangeEntry_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = sender as Button;
                if (button?.DataContext is AcquisitionMethod method)
                {
                    if (method.Shops == null)
                        method.Shops = new List<ExchangeEntry>();

                    method.Shops.Add(new ExchangeEntry());
                    RefreshMethod(method);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddExchangeEntry_Click 异常: {ex.Message}");
                ShowError("添加兑换项失败，请检查输入格式。");
            }
        }

        private void RemoveExchangeEntry_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button btn && btn.Tag is ExchangeEntry entry)
                {
                    var parentMethod = AcquisitionMethods.FirstOrDefault(m => m.Shops?.Contains(entry) == true);
                    if (parentMethod != null)
                    {
                        parentMethod.Shops.Remove(entry);
                        RefreshMethod(parentMethod);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoveExchangeEntry_Click 异常: {ex.Message}");
                ShowError("删除兑换项失败。");
            }
        }

        // 辅助刷新（因为 ObservableCollection 不会自动刷新嵌套属性）
        private void RefreshMethod(AcquisitionMethod method)
        {
            try
            {
                int index = AcquisitionMethods.IndexOf(method);
                if (index >= 0)
                {
                    AcquisitionMethods.RemoveAt(index);
                    AcquisitionMethods.Insert(index, method);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshMethod 异常: {ex.Message}");
                // 强制刷新整个集合
                var list = AcquisitionMethods.ToList();
                AcquisitionMethods.Clear();
                foreach (var m in list) AcquisitionMethods.Add(m);
            }
        }

        private async void ShowError(string message)
        {
            try
            {
                if (this.XamlRoot == null)
                {
                    System.Diagnostics.Debug.WriteLine($"错误（XamlRoot为空）: {message}");
                    return;
                }
                var dialog = new ContentDialog
                {
                    Title = "输入错误",
                    Content = message,
                    CloseButtonText = "确定",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示错误对话框失败: {ex.Message}");
            }
        }

        // ========== 列表操作 ==========
        private void AddEquipmentSlot_Click(object sender, RoutedEventArgs e) =>
            EquipmentSlots.Add(new EquipmentSlot());

        private void RemoveEquipmentSlot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is EquipmentSlot slot)
                EquipmentSlots.Remove(slot);
        }

        private void AddInitialEquipment_Click(object sender, RoutedEventArgs e) =>
            InitialEquipmentItems.Add(new InitialEquipment());

        private void RemoveInitialEquipment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is InitialEquipment ie)
                InitialEquipmentItems.Remove(ie);
        }

        private void AddSkill_Click(object sender, RoutedEventArgs e)
        {
            Skills.Add(new Skill
            {
                Id = Skills.Any() ? Skills.Max(s => s.Id) + 1 : 1,
                Name = new LocalizedString(),
                Description = new LocalizedString(),
                LevelValues = new List<List<string>>()
            });
        }

        private void RemoveSkill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Skill skill)
                Skills.Remove(skill);
        }

        private void AddSkin_Click(object sender, RoutedEventArgs e)
        {
            Skins.Add(new Skin
            {
                Name = new LocalizedString(),
                Id = Skins.Any() ? Skins.Max(s => s.Id) + 1 : 1,
                Type = SkinType.Static,
                IsOathEnabled = false
            });
        }

        private void RemoveSkin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is Skin skin)
                Skins.Remove(skin);
        }

        private void AddLine_Click(object sender, RoutedEventArgs e)
        {
            Lines.Add(new VoiceLine
            {
                Name = "登录",
                Content = new LocalizedString(),
                TriggerCondition = ""
            });
        }

        private void RemoveLine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is VoiceLine line)
                Lines.Remove(line);
        }

        private void AddGiftPreference_Click(object sender, RoutedEventArgs e) =>
            GiftPreferences.Add(new GiftPreference());

        private void RemoveGiftPreference_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GiftPreference gp)
                GiftPreferences.Remove(gp);
        }

        private void OnPrimaryCheckChanged(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox?.DataContext is AcquisitionMethod method && method.IsPrimary)
            {
                // 取消其他方法的 IsPrimary
                foreach (var other in AcquisitionMethods.Where(m => m != method))
                    other.IsPrimary = false;
            }
        }

        // ========== 获取 ShipStatic ==========
        public ShipStatic GetShip()
        {
            var ship = new ShipStatic
            {
                Id = (int)IdBox.Value,
                GameOrder = (int)GameOrderBox.Value,
                GlobalName = GlobalNameBox.Text.Trim(),
                Name = new LocalizedString
                {
                    ["zh-Hans"] = NameZhBox.Text,
                    ["zh-Hant"] = NameZhHantBox.Text,
                    ["en"] = NameEnBox.Text,
                    ["ja"] = NameJaBox.Text
                },
                AltName = new LocalizedString { ["zh-Hans"] = AltNameBox.Text },
                Alias = AliasBox.Text,
                Class = new LocalizedString
                {
                    ["zh-Hans"] = ClassZhBox.Text,
                    ["zh-Hant"] = ClassZhHantBox.Text,
                    ["en"] = ClassEnBox.Text,
                    ["ja"] = ClassJaBox.Text
                },
                Category = (ShipCategory)Enum.Parse(typeof(ShipCategory), (CategoryCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Normal"),
                Type = (ShipType)Enum.Parse(typeof(ShipType), (ShipTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "UNKNOWN"),
                Rarity = (Rarity)Enum.Parse(typeof(Rarity), (RarityCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "N"),
                Faction = (Faction)Enum.Parse(typeof(Faction), (FactionCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Other"),
                ReleaseDate = ReleaseDatePicker.Date.ToString("yyyy-MM-dd"),
                IsPermanent = IsPermanentCheckBox.IsChecked ?? false,
                CV = new LocalizedString
                {
                    ["zh-Hans"] = CvZhBox.Text,
                    ["zh-Hant"] = CvZhHantBox.Text,
                    ["en"] = CvEnBox.Text,
                    ["ja"] = CvJaBox.Text
                },
                Artist = ArtistBox.Text,
                RelatedEvent = RelatedEventBox.Text,

                Stats = new ShipStats
                {
                    Hp = (int)HpBox.Value,
                    Armor = (ArmorType)Enum.Parse(typeof(ArmorType), (ArmorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Light"),
                    Fp = (int)FpBox.Value,
                    Trp = (int)TrpBox.Value,
                    Aa = (int)AaBox.Value,
                    Avi = (int)AviBox.Value,
                    Hit = (int)HitBox.Value,
                    Eva = (int)EvaBox.Value,
                    Asw = (int)AswBox.Value,
                    Luck = (int)LuckBox.Value,
                    Oil = (int)OilBox.Value,
                    Speed = (double)SpeedBox.Value
                },

                Performance = new PerformanceRating
                {
                    Hp = (PerformanceGrade)Enum.Parse(typeof(PerformanceGrade), (HpGradeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "D"),
                    Aa = (PerformanceGrade)Enum.Parse(typeof(PerformanceGrade), (AaGradeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "D"),
                    Eva = (PerformanceGrade)Enum.Parse(typeof(PerformanceGrade), (EvaGradeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "D"),
                    Avi = (PerformanceGrade)Enum.Parse(typeof(PerformanceGrade), (AviGradeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "D"),
                    Trp = (PerformanceGrade)Enum.Parse(typeof(PerformanceGrade), (TrpGradeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "D"),
                    Fp = (PerformanceGrade)Enum.Parse(typeof(PerformanceGrade), (FpGradeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "D")
                },

                FleetTech = new FleetTech
                {
                    CollectPoints = (int)CollectPointsBox.Value,
                    LimitBreakPoints = (int)LimitBreakPointsBox.Value,
                    Level120Points = (int)Level120PointsBox.Value
                },

                // ===== 新获取方式 =====
                Acquisition = new AcquisitionData { Methods = AcquisitionMethods.ToList() },

                EquipmentSlots = EquipmentSlots.ToList(),
                InitialEquipment = InitialEquipmentItems.ToList(),

                SpecialGear = (HasSpecialGearCheck.IsChecked == true) ? new SpecialGear
                {
                    Name = new LocalizedString
                    {
                        ["zh-Hans"] = GearNameZhBox.Text,
                        ["zh-Hant"] = GearNameZhHantBox.Text,
                        ["en"] = GearNameEnBox.Text,
                        ["ja"] = GearNameJaBox.Text
                    },
                    ReleaseDate = GearDatePicker.Date.ToString("yyyy-MM-dd"),
                    Id = (int)GearIdBox.Value,
                    AcquisitionMethod = GearAcquireBox.Text
                } : null,

                Skills = Skills.ToList(),

                Retrofit = new RetrofitData
                {
                    CanRetrofit = CanRetrofitCheck.IsChecked ?? false,
                    RetrofitReleaseDate = (CanRetrofitCheck.IsChecked == true) ? RetrofitDatePicker.Date.ToString("yyyy-MM-dd") : "",
                    ShipTypeChanged = ShipTypeChangedCheck.IsChecked ?? false,
                    TargetShipType = (ShipType)Enum.Parse(typeof(ShipType), (TargetShipTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "UNKNOWN"),
                    Nodes = RetrofitNodesBox.Text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Split('|'))
                        .Where(a => a.Length >= 1)
                        .Select(a => {
                            try
                            {
                                return new RetrofitNode
                                {
                                    Name = new LocalizedString { ["zh-Hans"] = a[0] },
                                    AttributeBonus = a.Length > 1 ? a[1].Split(',').Select(kv => kv.Split('=')).Where(k => k.Length == 2).ToDictionary(k => k[0], k => int.Parse(k[1])) : new Dictionary<string, int>(),
                                    RequiredItems = a.Length > 2 ? a[2].Split(',').Select(int.Parse).ToList() : new List<int>(),
                                    RequiredCoins = a.Length > 3 ? int.Parse(a[3]) : 0,
                                    RequiredLevel = a.Length > 4 ? int.Parse(a[4]) : 0,
                                    RequiredStars = a.Length > 5 ? int.Parse(a[5]) : 0
                                };
                            }
                            catch { return null; }
                        })
                        .Where(n => n != null)
                        .ToList()
                },

                Research = new ResearchData
                {
                    PreRequisiteFactions = PreReqFactionsBox.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => (Faction)Enum.Parse(typeof(Faction), s)).ToList(),
                    TechPoints = (int)TechPointsBox.Value,
                    Tasks = ResearchTasksBox.Text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Split('|'))
                        .Where(a => a.Length >= 3)
                        .Select(a => new ResearchTask
                        {
                            Name = a[0],
                            Description = a[1],
                            Requirement = a[2]
                        }).ToList(),
                    BlueprintRequired = (int)BlueprintRequiredBox.Value,
                    DevelopBonus = DevelopBonusBox.Text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Split('='))
                        .Where(a => a.Length == 2)
                        .ToDictionary(a => int.Parse(a[0]), a => a[1]),
                    DevelopBlueprintRequired = (int)DevelopBlueprintRequiredBox.Value,
                    HasFateSimulation = HasFateSimCheck.IsChecked ?? false,
                    FateSim = (HasFateSimCheck.IsChecked == true) ? new FateSimulation
                    {
                        Level = (int)FateLevelBox.Value,
                        Description = FateDescBox.Text,
                        BlueprintRequired = (int)FateBlueprintRequiredBox.Value
                    } : null
                },

                Skins = Skins.ToList(),
                Lines = Lines.ToList(),
                GiftPreferences = GiftPreferences.ToList(),

                CanBeEnhanceMaterial = CanBeEnhanceMaterialCheck.IsChecked ?? false,
                EnhanceValue = (int)EnhanceValueBox.Value,
                CanRetire = CanRetireCheck.IsChecked ?? false,
                RetirementReward = RetirementRewardBox.Text,
                EnhanceExp = new EnhanceExp
                {
                    Fp = (int)EnhanceExpFp.Value,
                    Trp = (int)EnhanceExpTrp.Value,
                    Avi = (int)EnhanceExpAvi.Value,
                    Rld = (int)EnhanceExpRld.Value
                },
                EnhanceItems = EnhanceItemsBox.Text.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
                    .Where(id => id.HasValue)
                    .Select(id => id.Value)
                    .ToList(),
                ExtraEnhance = ExtraEnhanceBox.Text,

                Remarks = RemarksBox.Text,
                Notes = NotesBox.Text,
                ReferenceMarkdown = ReferenceMarkdownBox.Text
            };

            return ship;
        }
    }
}