using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using AzurLaneDex.Views.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Windows.ApplicationModel.Resources;
using static AzurLaneDex.Models.ShipStatic;

namespace AzurLaneDex.Views
{
    public sealed partial class AddShipDialog : ContentDialog
    {
        private int _editingShipId = 0;
        private readonly ResourceLoader _loader = ResourceLoader.GetForViewIndependentUse();
        private bool _isLoadingShipData = false;

        // 动态数据源（根据舰船类别切换）
        private List<KeyValuePair<int, string>> _normalFactionList;
        private List<KeyValuePair<int, string>> _collabFactionList;
        private List<KeyValuePair<int, string>> _metaFactionList;
        private List<KeyValuePair<int, string>> _normalRarityList;
        private List<KeyValuePair<int, string>> _researchRarityList;

        // 固定数据源
        private List<KeyValuePair<int, string>> _shipClassList;
        private List<KeyValuePair<int, string>> _attributeList;

        public ObservableCollection<AcquireEntry> AcquireEntries { get; set; } = new();

        public AddShipDialog(ShipStatic editShip = null)
        {
            this.InitializeComponent();

            // 先初始化动态数据源（阵营、稀有度分段）
            InitializeDynamicComboBoxes();
            // 再初始化固定数据源（舰种、属性）以及默认下拉选项
            InitializeComboBoxes();

            AcquireEntriesItemsControl.ItemsSource = AcquireEntries;
            AddAcquireEntryButton.Click += AddAcquireEntry_Click;

            if (editShip != null)
            {
                _editingShipId = editShip.Id;
                LoadShipData(editShip);
                this.Title = _loader.GetString("EditShipDialog_Title");

                foreach (var entry in editShip.AcquireEntries)
                {
                    var customCopy = new LocalizedString();
                    foreach (var kv in entry.CustomText)
                        customCopy[kv.Key] = kv.Value;

                    var copy = new AcquireEntry
                    {
                        Tag = entry.Tag,
                        Parameters = new List<string>(entry.Parameters),
                        CustomText = customCopy
                    };
                    AcquireEntries.Add(copy);
                }
            }
            else
            {
                this.Title = _loader.GetString("AddShipDialog_Title");
                // 默认今天日期
                ReleaseDatePicker.Date = DateTimeOffset.Now;
                SpecialGearDatePicker.Date = DateTimeOffset.Now;
                RemodelDatePicker.Date = DateTimeOffset.Now;
                // 舰种默认选中第一个
                if (ShipClassCombo.Items.Count > 0)
                    ShipClassCombo.SelectedIndex = 0;
                // 属性加成默认选中“无”
                if (ObtainBonusAttrCombo.Items.Count > 0)
                    ObtainBonusAttrCombo.SelectedIndex = 0;
                if (Level120BonusAttrCombo.Items.Count > 0)
                    Level120BonusAttrCombo.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// 初始化动态数据源（阵营按数值范围区分，稀有度按普通/科研区分）
        /// </summary>
        private void InitializeDynamicComboBoxes()
        {
            // 获取所有阵营枚举
            var allFactions = Enum.GetValues(typeof(Faction))
                .Cast<Faction>()
                .Where(f => (int)f != 0)
                .Select(f => new KeyValuePair<int, string>((int)f, LocalizationHelper.GetEnumString("Faction", (int)f)))
                .ToList();

            _normalFactionList = allFactions.Where(kv => kv.Key >= 1 && kv.Key < 100).ToList();   // 普通阵营 1-99
            _collabFactionList = allFactions.Where(kv => kv.Key >= 100 && kv.Key < 200).ToList(); // 联动阵营 100-199
            _metaFactionList = allFactions.Where(kv => kv.Key >= 200 && kv.Key < 300).ToList();   // META阵营 200-299

            // 获取所有稀有度枚举
            var allRarities = Enum.GetValues(typeof(Rarity))
                .Cast<Rarity>()
                .Where(r => (int)r != 0)
                .Select(r => new KeyValuePair<int, string>((int)r, LocalizationHelper.GetEnumString("Rarity", (int)r)))
                .ToList();

            _normalRarityList = allRarities.Where(kv => kv.Key >= 1 && kv.Key <= 5).ToList();     // N,R,SR,SSR,UR
            _researchRarityList = allRarities.Where(kv => kv.Key == 6 || kv.Key == 7).ToList();   // Decisive,Ultimate
        }

        /// <summary>
        /// 初始化固定数据源（舰种、属性），并设置默认的阵营/稀有度数据源（普通类别）
        /// </summary>
        private void InitializeComboBoxes()
        {
            // 舰种列表
            _shipClassList = Enum.GetValues(typeof(ShipClass))
                .Cast<ShipClass>()
                .Select(sc => new KeyValuePair<int, string>((int)sc, LocalizationHelper.GetEnumString("ShipClass", (int)sc)))
                .ToList();
            ShipClassCombo.ItemsSource = _shipClassList;
            ShipClassCombo.DisplayMemberPath = "Value";
            ShipClassCombo.SelectedValuePath = "Key";

            // 属性列表（获得时加成和120级加成共用）
            _attributeList = Enum.GetValues(typeof(AttributeType))
                .Cast<AttributeType>()
                .Select(a => new KeyValuePair<int, string>((int)a, LocalizationHelper.GetEnumString("Attr", (int)a)))
                .ToList();
            ObtainBonusAttrCombo.ItemsSource = _attributeList;
            ObtainBonusAttrCombo.DisplayMemberPath = "Value";
            ObtainBonusAttrCombo.SelectedValuePath = "Key";
            Level120BonusAttrCombo.ItemsSource = _attributeList;
            Level120BonusAttrCombo.DisplayMemberPath = "Value";
            Level120BonusAttrCombo.SelectedValuePath = "Key";

            // 阵营、稀有度默认使用普通类别的数据源（Category默认为Normal）
            FactionCombo.ItemsSource = _normalFactionList;
            FactionCombo.DisplayMemberPath = "Value";
            FactionCombo.SelectedValuePath = "Key";
            RarityCombo.ItemsSource = _normalRarityList;
            RarityCombo.DisplayMemberPath = "Value";
            RarityCombo.SelectedValuePath = "Key";

            // 默认选中第一项
            if (FactionCombo.Items.Count > 0)
                FactionCombo.SelectedIndex = 0;
            if (RarityCombo.Items.Count > 0)
                RarityCombo.SelectedIndex = 0;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            CanSpecialGearCheckBox.Checked += (s, args) => UpdateSpecialGearControlsEnabled();
            CanSpecialGearCheckBox.Unchecked += (s, args) => UpdateSpecialGearControlsEnabled();
            UpdateSpecialGearControlsEnabled();
        }

        private void UpdateSpecialGearControlsEnabled()
        {
            bool enabled = CanSpecialGearCheckBox.IsChecked ?? false;
            SpecialGearNameZhBox.IsEnabled = enabled;
            SpecialGearNameZhHantBox.IsEnabled = enabled;
            SpecialGearNameEnBox.IsEnabled = enabled;
            SpecialGearNameJaBox.IsEnabled = enabled;
            SpecialGearDatePicker.IsEnabled = enabled;
            SpecialGearTypeCombo.IsEnabled = enabled;
            Param1Box.IsEnabled = enabled;
            Param2Box.IsEnabled = enabled;

            CustomTextZhBox.IsEnabled = enabled;
            CustomTextZhHantBox.IsEnabled = enabled;
            CustomTextEnBox.IsEnabled = enabled;
            CustomTextJaBox.IsEnabled = enabled;

            if (!enabled)
            {
                DynamicParamPanel.Visibility = Visibility.Collapsed;
                CustomTextPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void SpecialGearTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SpecialGearTypeCombo.SelectedItem is ComboBoxItem selected)
            {
                string tag = selected.Tag as string;
                if (tag == "gear_custom")
                {
                    DynamicParamPanel.Visibility = Visibility.Collapsed;
                    CustomTextPanel.Visibility = Visibility.Visible;
                }
                else if (tag == "gear_2" || tag == "gear_4")
                {
                    DynamicParamPanel.Visibility = Visibility.Visible;
                    CustomTextPanel.Visibility = Visibility.Collapsed;
                    if (tag == "gear_2")
                    {
                        Param1Box.Header = "活动名称";
                        Param2Box.Visibility = Visibility.Collapsed;
                    }
                    else if (tag == "gear_4")
                    {
                        Param1Box.Header = "商店名称";
                        Param2Box.Header = "PT 数值";
                        Param2Box.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    DynamicParamPanel.Visibility = Visibility.Collapsed;
                    CustomTextPanel.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(NameZhBox.Text))
            {
                args.Cancel = true;
                ShowError(_loader.GetString("ShipNameEmpty_Message"));
                return;
            }
            if (FactionCombo.SelectedValue == null)
            {
                args.Cancel = true;
                ShowError(_loader.GetString("PleaseSelectFaction_Message"));
                return;
            }
            if (ShipClassCombo.SelectedValue == null)
            {
                args.Cancel = true;
                ShowError(_loader.GetString("PleaseSelectShipClass_Message"));
                return;
            }
            if (RarityCombo.SelectedValue == null)
            {
                args.Cancel = true;
                ShowError(_loader.GetString("PleaseSelectRarity_Message"));
                return;
            }
        }

        // 根据舰种自动勾选适用舰种（获得时 / 120级）
        private void ShipClassCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingShipData) return;

            var selected = ShipClassCombo.SelectedItem as KeyValuePair<int, string>?;
            if (selected == null) return;
            string selectedClassName = GetShipClassChineseName(selected.Value.Key);
            if (string.IsNullOrEmpty(selectedClassName)) return;

            var obtainMap = new Dictionary<string, List<string>>()
            {
                ["驱逐"] = new List<string> { "驱逐" },
                ["轻巡"] = new List<string> { "轻巡" },
                ["重巡"] = new List<string> { "重巡", "超巡", "重炮" },
                ["超巡"] = new List<string> { "重巡", "超巡", "重炮" },
                ["重炮"] = new List<string> { "重巡", "超巡", "重炮" },
                ["战巡"] = new List<string> { "战巡", "战列", "航战" },
                ["战列"] = new List<string> { "战巡", "战列", "航战" },
                ["航战"] = new List<string> { "战巡", "战列", "航战" },
                ["航母"] = new List<string> { "航母", "轻航" },
                ["轻航"] = new List<string> { "轻航" },
                ["维修"] = new List<string> { "维修" },
                ["潜艇"] = new List<string> { "潜艇", "潜母" },
                ["潜母"] = new List<string> { "潜艇", "潜母" },
                ["运输"] = new List<string> { "运输" },
                ["风帆"] = new List<string> { "风帆" }
            };
            var level120Map = new Dictionary<string, List<string>>()
            {
                ["驱逐"] = new List<string> { "驱逐" },
                ["轻巡"] = new List<string> { "轻巡" },
                ["重巡"] = new List<string> { "重巡", "超巡", "重炮" },
                ["超巡"] = new List<string> { "重巡", "超巡", "重炮" },
                ["重炮"] = new List<string> { "重巡", "超巡", "重炮" },
                ["战巡"] = new List<string> { "战巡" },
                ["战列"] = new List<string> { "战巡", "战列", "航战" },
                ["航战"] = new List<string> { "战巡", "战列", "航战" },
                ["航母"] = new List<string> { "航母", "轻航" },
                ["轻航"] = new List<string> { "航母", "轻航" },
                ["维修"] = new List<string> { "维修" },
                ["潜艇"] = new List<string> { "潜艇", "潜母" },
                ["潜母"] = new List<string> { "潜艇", "潜母" },
                ["运输"] = new List<string> { "运输" },
                ["风帆"] = new List<string> { "风帆" }
            };

            ClearAllCheckboxes("ObtainAffect");
            ClearAllCheckboxes("Level120Affect");

            if (obtainMap.ContainsKey(selectedClassName))
            {
                foreach (var sc in obtainMap[selectedClassName])
                    CheckCheckboxByName($"ObtainAffect{GetCheckboxSuffix(sc)}", true);
            }
            if (level120Map.ContainsKey(selectedClassName))
            {
                foreach (var sc in level120Map[selectedClassName])
                    CheckCheckboxByName($"Level120Affect{GetCheckboxSuffix(sc)}", true);
            }
        }

        private string GetShipClassChineseName(int classId)
        {
            return classId switch
            {
                (int)ShipClass.DD => "驱逐",
                (int)ShipClass.CL => "轻巡",
                (int)ShipClass.CA => "重巡",
                (int)ShipClass.CB => "超巡",
                (int)ShipClass.BM => "重炮",
                (int)ShipClass.BC => "战巡",
                (int)ShipClass.BB => "战列",
                (int)ShipClass.BBV => "航战",
                (int)ShipClass.CV => "航母",
                (int)ShipClass.CVL => "轻航",
                (int)ShipClass.AR => "维修",
                (int)ShipClass.SS => "潜艇",
                (int)ShipClass.SSV => "潜母",
                (int)ShipClass.AE => "运输",
                (int)ShipClass.Sail => "风帆",
                _ => ""
            };
        }

        private string GetCheckboxSuffix(string shipClass)
        {
            return shipClass switch
            {
                "驱逐" => "DD",
                "轻巡" => "CL",
                "重巡" => "CA",
                "超巡" => "CB",
                "重炮" => "CA",
                "战巡" => "BC",
                "战列" => "BB",
                "航战" => "BBV",
                "航母" => "CV",
                "轻航" => "CVL",
                "维修" => "AR",
                "潜艇" => "SS",
                "潜母" => "SSV",
                "运输" => "AE",
                "风帆" => "Sail",
                _ => ""
            };
        }

        private void ClearAllCheckboxes(string prefix)
        {
            var names = new[] { "DD", "CL", "CA", "CB", "BC", "BB", "BBV", "CV", "CVL", "AR", "SS", "SSV", "AE", "Sail" };
            foreach (var suffix in names)
            {
                var cb = FindName($"{prefix}{suffix}") as CheckBox;
                if (cb != null) cb.IsChecked = false;
            }
        }

        private void CheckCheckboxByName(string name, bool isChecked)
        {
            var cb = FindName(name) as CheckBox;
            if (cb != null) cb.IsChecked = isChecked;
        }

        /// <summary>
        /// 根据舰船类别切换阵营和稀有度下拉框的数据源
        /// </summary>
        private void UpdateCategoryDependentControls(ShipCategory category)
        {
            switch (category)
            {
                case ShipCategory.Collab:
                    FactionCombo.ItemsSource = _collabFactionList;
                    RarityCombo.ItemsSource = _normalRarityList;
                    break;
                case ShipCategory.META:
                    FactionCombo.ItemsSource = _metaFactionList;
                    RarityCombo.ItemsSource = _normalRarityList;
                    break;
                case ShipCategory.Research:
                    FactionCombo.ItemsSource = _normalFactionList;
                    RarityCombo.ItemsSource = _researchRarityList;
                    break;
                default: // Normal
                    FactionCombo.ItemsSource = _normalFactionList;
                    RarityCombo.ItemsSource = _normalRarityList;
                    break;
            }

            // 确保当前选中值有效，否则重置为第一项
            if (FactionCombo.SelectedValue != null &&
                !(FactionCombo.ItemsSource as IEnumerable<KeyValuePair<int, string>>).Any(kv => kv.Key == (int)FactionCombo.SelectedValue))
            {
                FactionCombo.SelectedIndex = 0;
            }
            if (RarityCombo.SelectedValue != null &&
                !(RarityCombo.ItemsSource as IEnumerable<KeyValuePair<int, string>>).Any(kv => kv.Key == (int)RarityCombo.SelectedValue))
            {
                RarityCombo.SelectedIndex = 0;
            }
        }

        private void CategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int selectedIndex = CategoryCombo.SelectedIndex;
            bool isNormal = (selectedIndex == 0);
            GameOrderLabel.Visibility = isNormal ? Visibility.Visible : Visibility.Collapsed;
            GameOrderBox.Visibility = isNormal ? Visibility.Visible : Visibility.Collapsed;
            CategoryOrderLabel.Visibility = isNormal ? Visibility.Collapsed : Visibility.Visible;
            CategoryOrderBox.Visibility = isNormal ? Visibility.Collapsed : Visibility.Visible;

            if (_isLoadingShipData) return;

            ShipCategory category = (ShipCategory)selectedIndex;
            UpdateCategoryDependentControls(category);
        }

        private async void ShowError(string message)
        {
            var dialog = new ContentDialog
            {
                Title = _loader.GetString("IncompleteInput_Title"),
                Content = message,
                CloseButtonText = _loader.GetString("Common_Confirm"),
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
        }

        private void LoadShipData(ShipStatic ship)
        {
            _isLoadingShipData = true;
            try
            {
                // 基本信息
                IdBox.Value = ship.Id;
                GameOrderBox.Value = ship.GameOrder;
                CanRemodelCheckBox.IsChecked = ship.CanRemodel;

                // 设置类别（触发数据源切换，但此时 _isLoadingShipData 为 true，事件会跳过）
                CategoryCombo.SelectedIndex = (int)ship.Category;
                // 手动调用数据源切换（不受 _isLoadingShipData 影响，因为直接调用了更新方法）
                UpdateCategoryDependentControls(ship.Category);

                // 多语言名称
                NameZhBox.Text = ship.Name.GetValueOrDefault("zh-Hans");
                NameZhHantBox.Text = ship.Name.GetValueOrDefault("zh-Hant");
                NameEnBox.Text = ship.Name.GetValueOrDefault("en");
                NameJaBox.Text = ship.Name.GetValueOrDefault("ja");

                AltNameZhBox.Text = ship.AltName.GetValueOrDefault("zh-Hans");
                AltNameZhHantBox.Text = ship.AltName.GetValueOrDefault("zh-Hant");
                AltNameEnBox.Text = ship.AltName.GetValueOrDefault("en");
                AltNameJaBox.Text = ship.AltName.GetValueOrDefault("ja");

                // 下拉框选中 ID
                FactionCombo.SelectedValue = ship.FactionId;
                ShipClassCombo.SelectedValue = ship.ShipClassId;
                RarityCombo.SelectedValue = ship.RarityId;
                ObtainBonusAttrCombo.SelectedValue = ship.ObtainBonusAttrId;
                Level120BonusAttrCombo.SelectedValue = ship.Level120BonusAttrId;

                ObtainBonusValueBox.Value = ship.ObtainBonusValue;
                Level120BonusValueBox.Value = ship.Level120BonusValue;

                // 顺序值
                if (ship.Category == ShipCategory.Normal)
                    GameOrderBox.Value = ship.GameOrder;
                else
                    CategoryOrderBox.Value = ship.CategoryOrder;

                // 改造日期
                if (!string.IsNullOrEmpty(ship.RemodelDate) &&
                    DateTime.TryParseExact(ship.RemodelDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime remodelDate))
                    RemodelDatePicker.Date = remodelDate;

                BuildTimeBox.Text = ship.BuildTime;
                DropLocationsBox.Text = string.Join(";", ship.DropLocations);
                ShopExchangeBox.Text = ship.ShopExchange;
                IsPermanentCheckBox.IsChecked = ship.IsPermanent;

                // 实装活动
                DebutEventZhBox.Text = ship.DebutEvent.GetValueOrDefault("zh-Hans");
                DebutEventZhHantBox.Text = ship.DebutEvent.GetValueOrDefault("zh-Hant");
                DebutEventEnBox.Text = ship.DebutEvent.GetValueOrDefault("en");
                DebutEventJaBox.Text = ship.DebutEvent.GetValueOrDefault("ja");

                if (!string.IsNullOrEmpty(ship.ReleaseDate) &&
                    DateTime.TryParseExact(ship.ReleaseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime releaseDate))
                    ReleaseDatePicker.Date = releaseDate;

                NotesZhBox.Text = ship.Notes.GetValueOrDefault("zh-Hans");
                NotesZhHantBox.Text = ship.Notes.GetValueOrDefault("zh-Hant");
                NotesEnBox.Text = ship.Notes.GetValueOrDefault("en");
                NotesJaBox.Text = ship.Notes.GetValueOrDefault("ja");

                // 适用舰种复选框
                ClearAllCheckboxes("ObtainAffect");
                foreach (var classId in ship.ObtainAffectClassIds)
                {
                    string className = GetShipClassChineseName(classId);
                    string suffix = GetCheckboxSuffix(className);
                    if (!string.IsNullOrEmpty(suffix))
                        CheckCheckboxByName($"ObtainAffect{suffix}", true);
                }
                ClearAllCheckboxes("Level120Affect");
                foreach (var classId in ship.Level120AffectClassIds)
                {
                    string className = GetShipClassChineseName(classId);
                    string suffix = GetCheckboxSuffix(className);
                    if (!string.IsNullOrEmpty(suffix))
                        CheckCheckboxByName($"Level120Affect{suffix}", true);
                }

                // 舰队科技
                TechPointsObtainBox.Value = ship.TechPointsObtain;
                TechPointsMaxBox.Value = ship.TechPointsMax;
                TechPoints120Box.Value = ship.TechPoints120;

                // 特殊兵装
                CanSpecialGearCheckBox.IsChecked = ship.CanSpecialGear;
                SpecialGearNameZhBox.Text = ship.SpecialGearName.GetValueOrDefault("zh-Hans");
                SpecialGearNameZhHantBox.Text = ship.SpecialGearName.GetValueOrDefault("zh-Hant");
                SpecialGearNameEnBox.Text = ship.SpecialGearName.GetValueOrDefault("en");
                SpecialGearNameJaBox.Text = ship.SpecialGearName.GetValueOrDefault("ja");

                if (!string.IsNullOrEmpty(ship.SpecialGearDate) &&
                    DateTime.TryParseExact(ship.SpecialGearDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime spDate))
                    SpecialGearDatePicker.Date = spDate;

                // 加载特殊兵装获取方式
                if (ship.SpecialGearEntries != null && ship.SpecialGearEntries.Any())
                {
                    var entry = ship.SpecialGearEntries.First();
                    foreach (ComboBoxItem item in SpecialGearTypeCombo.Items)
                    {
                        if (item.Tag as string == entry.Tag)
                        {
                            SpecialGearTypeCombo.SelectedItem = item;
                            break;
                        }
                    }
                    if (entry.Tag == "gear_2" && entry.Parameters.Count > 0)
                        Param1Box.Text = entry.Parameters[0];
                    else if (entry.Tag == "gear_4" && entry.Parameters.Count >= 2)
                    {
                        Param1Box.Text = entry.Parameters[0];
                        Param2Box.Text = entry.Parameters[1];
                    }
                    else if (entry.Tag == "gear_custom" && entry.CustomText != null)
                    {
                        CustomTextZhBox.Text = entry.CustomText.GetValueOrDefault("zh-Hans");
                        CustomTextZhHantBox.Text = entry.CustomText.GetValueOrDefault("zh-Hant");
                        CustomTextEnBox.Text = entry.CustomText.GetValueOrDefault("en");
                        CustomTextJaBox.Text = entry.CustomText.GetValueOrDefault("ja");
                    }
                }
                else if (ship.SpecialGearAcquire != null && !string.IsNullOrEmpty(ship.SpecialGearAcquire.GetLocalized()))
                {
                    SpecialGearTypeCombo.SelectedItem = SpecialGearTypeCombo.Items.FirstOrDefault(i => (i as ComboBoxItem)?.Tag as string == "gear_custom");
                    CustomTextZhBox.Text = ship.SpecialGearAcquire.GetValueOrDefault("zh-Hans");
                    CustomTextZhHantBox.Text = ship.SpecialGearAcquire.GetValueOrDefault("zh-Hant");
                    CustomTextEnBox.Text = ship.SpecialGearAcquire.GetValueOrDefault("en");
                    CustomTextJaBox.Text = ship.SpecialGearAcquire.GetValueOrDefault("ja");
                }
                else
                {
                    SpecialGearTypeCombo.SelectedIndex = 0;
                }
                SpecialGearTypeCombo_SelectionChanged(SpecialGearTypeCombo, null);
            }
            finally
            {
                _isLoadingShipData = false;
                UpdateSpecialGearControlsEnabled();
            }
        }

        public ShipStatic GetShip()
        {
            int id = (int)IdBox.Value;
            ShipCategory category = (ShipCategory)CategoryCombo.SelectedIndex;

            int gameOrder = 0, categoryOrder = 0;
            if (category == ShipCategory.Normal)
            {
                gameOrder = (int)GameOrderBox.Value;
                categoryOrder = gameOrder;
            }
            else
            {
                categoryOrder = (int)CategoryOrderBox.Value;
                gameOrder = 0;
            }

            bool canRemodel = CanRemodelCheckBox.IsChecked == true;
            string remodelDate = canRemodel ? RemodelDatePicker.Date.ToString("yyyy-MM-dd") : "";

            var nameLoc = new LocalizedString
            {
                ["zh-Hans"] = NameZhBox.Text.Trim(),
                ["zh-Hant"] = NameZhHantBox.Text.Trim(),
                ["en"] = NameEnBox.Text.Trim(),
                ["ja"] = NameJaBox.Text.Trim()
            };
            var altNameLoc = new LocalizedString
            {
                ["zh-Hans"] = AltNameZhBox.Text.Trim(),
                ["zh-Hant"] = AltNameZhHantBox.Text.Trim(),
                ["en"] = AltNameEnBox.Text.Trim(),
                ["ja"] = AltNameJaBox.Text.Trim()
            };

            var debutEventLoc = new LocalizedString
            {
                ["zh-Hans"] = DebutEventZhBox.Text.Trim(),
                ["zh-Hant"] = DebutEventZhHantBox.Text.Trim(),
                ["en"] = DebutEventEnBox.Text.Trim(),
                ["ja"] = DebutEventJaBox.Text.Trim()
            };
            var notesLoc = new LocalizedString
            {
                ["zh-Hans"] = NotesZhBox.Text.Trim(),
                ["zh-Hant"] = NotesZhHantBox.Text.Trim(),
                ["en"] = NotesEnBox.Text.Trim(),
                ["ja"] = NotesJaBox.Text.Trim()
            };

            var gearNameLoc = new LocalizedString();
            gearNameLoc["zh-Hans"] = SpecialGearNameZhBox.Text.Trim();
            gearNameLoc["zh-Hant"] = SpecialGearNameZhHantBox.Text.Trim();
            gearNameLoc["en"] = SpecialGearNameEnBox.Text.Trim();
            gearNameLoc["ja"] = SpecialGearNameJaBox.Text.Trim();

            List<int> obtainAffectIds = GetSelectedClassIds("ObtainAffect");
            List<int> level120AffectIds = GetSelectedClassIds("Level120Affect");

            bool canSpecialGear = CanSpecialGearCheckBox.IsChecked == true;
            string specialGearDate = canSpecialGear ? SpecialGearDatePicker.Date.ToString("yyyy-MM-dd") : "";
            List<SpecialGearEntry> gearEntries = new List<SpecialGearEntry>();
            if (canSpecialGear)
            {
                var selectedItem = SpecialGearTypeCombo.SelectedItem as ComboBoxItem;
                string tag = selectedItem?.Tag as string ?? "gear_1";
                var entry = new SpecialGearEntry { Tag = tag };
                if (tag == "gear_custom")
                {
                    var custom = new LocalizedString();
                    custom["zh-Hans"] = CustomTextZhBox.Text.Trim();
                    custom["zh-Hant"] = CustomTextZhHantBox.Text.Trim();
                    custom["en"] = CustomTextEnBox.Text.Trim();
                    custom["ja"] = CustomTextJaBox.Text.Trim();
                    entry.CustomText = custom;
                }
                else if (tag == "gear_2")
                {
                    entry.Parameters.Add(Param1Box.Text.Trim());
                }
                else if (tag == "gear_4")
                {
                    entry.Parameters.Add(Param1Box.Text.Trim());
                    entry.Parameters.Add(Param2Box.Text.Trim());
                }
                gearEntries.Add(entry);
            }

            string buildTime = BuildTimeBox.Text.Trim();
            List<string> dropLocations = DropLocationsBox.Text.Split(new[] { ';', '，' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim()).ToList();
            string shopExchange = ShopExchangeBox.Text.Trim();
            bool isPermanent = IsPermanentCheckBox.IsChecked == true;
            string releaseDate = ReleaseDatePicker.Date.ToString("yyyy-MM-dd");

            return new ShipStatic
            {
                Id = id,
                Name = nameLoc,
                AltName = altNameLoc,
                FactionId = (int)FactionCombo.SelectedValue,
                ShipClassId = (int)ShipClassCombo.SelectedValue,
                RarityId = (int)RarityCombo.SelectedValue,
                GameOrder = gameOrder,
                Category = category,
                CategoryOrder = categoryOrder,
                AcquireEntries = AcquireEntries.ToList(),
                BuildTime = buildTime,
                DropLocations = dropLocations,
                ShopExchange = shopExchange,
                IsPermanent = isPermanent,
                DebutEvent = debutEventLoc,
                ReleaseDate = releaseDate,
                Notes = notesLoc,
                CanRemodel = canRemodel,
                RemodelDate = remodelDate,
                CanSpecialGear = canSpecialGear,
                SpecialGearEntries = gearEntries,
                SpecialGearName = gearNameLoc,
                SpecialGearDate = specialGearDate,
                ObtainBonusAttrId = (int)ObtainBonusAttrCombo.SelectedValue,
                ObtainBonusValue = (int)ObtainBonusValueBox.Value,
                ObtainAffectClassIds = obtainAffectIds,
                Level120BonusAttrId = (int)Level120BonusAttrCombo.SelectedValue,
                Level120BonusValue = (int)Level120BonusValueBox.Value,
                Level120AffectClassIds = level120AffectIds,
                TechPointsObtain = (int)TechPointsObtainBox.Value,
                TechPointsMax = (int)TechPointsMaxBox.Value,
                TechPoints120 = (int)TechPoints120Box.Value
            };
        }

        private void AddAcquireEntry_Click(object sender, RoutedEventArgs e)
        {
            AcquireEntries.Add(new AcquireEntry());
        }

        private void OnEntryDeleteRequested(object sender, RoutedEventArgs e)
        {
            if (sender is AcquireEntryControl control && control.DataContext is AcquireEntry entry)
            {
                AcquireEntries.Remove(entry);
            }
        }

        private List<int> GetSelectedClassIds(string prefix)
        {
            var ids = new List<int>();
            var mapping = new Dictionary<string, int>
            {
                ["DD"] = (int)ShipClass.DD,
                ["CL"] = (int)ShipClass.CL,
                ["CA"] = (int)ShipClass.CA,
                ["CB"] = (int)ShipClass.CB,
                ["BC"] = (int)ShipClass.BC,
                ["BB"] = (int)ShipClass.BB,
                ["BBV"] = (int)ShipClass.BBV,
                ["CV"] = (int)ShipClass.CV,
                ["CVL"] = (int)ShipClass.CVL,
                ["AR"] = (int)ShipClass.AR,
                ["SS"] = (int)ShipClass.SS,
                ["SSV"] = (int)ShipClass.SSV,
                ["AE"] = (int)ShipClass.AE,
                ["Sail"] = (int)ShipClass.Sail
            };
            foreach (var kv in mapping)
            {
                var cb = FindName($"{prefix}{kv.Key}") as CheckBox;
                if (cb != null && cb.IsChecked == true)
                    ids.Add(kv.Value);
            }
            return ids;
        }
    }
}