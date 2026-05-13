using AzurLaneDex.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AzurLaneDex.Views
{
    public sealed partial class AddShipDialog : ContentDialog
    {
        private int _editingShipId = 0;
        public AddShipDialog(ShipStatic editShip = null)
        {
            this.InitializeComponent();
            if (editShip != null)
            {
                _editingShipId = editShip.Id;
                LoadShipData(editShip);
                this.Title = "编辑舰船";
                // 可选：禁止修改 ID
                // IdBox.IsEnabled = false;
            }
            else
            {
                this.Title = "新增舰船";
                // 清空默认值（略）
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 初始化日期选择器为今天
            ReleaseDatePicker.Date = DateTimeOffset.Now;
            SpecialGearDatePicker.Date = DateTimeOffset.Now;

            // 监听特殊兵装复选框，启用/禁用相关控件
            CanSpecialGearCheckBox.Checked += (s, args) => UpdateSpecialGearControlsEnabled();
            CanSpecialGearCheckBox.Unchecked += (s, args) => UpdateSpecialGearControlsEnabled();
        }

        private void UpdateSpecialGearControlsEnabled()
        {
            bool enabled = CanSpecialGearCheckBox.IsChecked ?? false;
            SpecialGearNameBox.IsEnabled = enabled;
            SpecialGearDatePicker.IsEnabled = enabled;
            SpecialGearAcquireBox.IsEnabled = enabled;
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 验证必填项（例如名称不能为空）
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                args.Cancel = true;
                ShowError("舰船名称不能为空");
                return;
            }

            if (FactionCombo.SelectedItem == null)
            {
                args.Cancel = true;
                ShowError("请选择阵营");
                return;
            }

            if (ShipClassCombo.SelectedItem == null)
            {
                args.Cancel = true;
                ShowError("请选择舰种");
                return;
            }

            if (RarityCombo.SelectedItem == null)
            {
                args.Cancel = true;
                ShowError("请选择稀有度");
                return;
            }
        }

        private bool _isLoadingShipData = false; // 防止加载数据时触发事件

        private void ShipClassCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingShipData) return;

            var selected = (ShipClassCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (string.IsNullOrEmpty(selected)) return;

            // 获得时映射规则
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

            // 120级映射规则
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

            // 清除所有获得时复选框
            ClearAllCheckboxes("ObtainAffect");
            // 清除所有120级复选框
            ClearAllCheckboxes("Level120Affect");

            // 设置获得时复选框
            if (obtainMap.ContainsKey(selected))
            {
                foreach (var sc in obtainMap[selected])
                {
                    CheckCheckboxByName($"ObtainAffect{GetCheckboxSuffix(sc)}", true);
                }
            }

            // 设置120级复选框
            if (level120Map.ContainsKey(selected))
            {
                foreach (var sc in level120Map[selected])
                {
                    CheckCheckboxByName($"Level120Affect{GetCheckboxSuffix(sc)}", true);
                }
            }
        }
        private void SetComboBoxSelectedItem(ComboBox combo, string value)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Content?.ToString() == value)
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
        }

        private string GetCheckboxSuffix(string shipClass)
        {
            // 根据舰种返回对应的复选框名称后缀（与 XAML 中的 x:Name 后缀一致）
            return shipClass switch
            {
                "驱逐" => "DD",
                "轻巡" => "CL",
                "重巡" => "CA",
                "超巡" => "CB",
                "重炮" => "CA",      // 与重巡共用复选框
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
        private async void ShowError(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "输入不完整",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
        private void LoadShipData(ShipStatic ship)
        {
            _isLoadingShipData = true;
            try
            {
                // 基本信息
                NameBox.Text = ship.Name;
                AltNameBox.Text = ship.AltName;
                SetComboBoxSelectedItem(FactionCombo, ship.Faction);
                SetComboBoxSelectedItem(ShipClassCombo, ship.ShipClass);
                SetComboBoxSelectedItem(RarityCombo, ship.Rarity);
                IdBox.Value = ship.Id;
                GameOrderBox.Value = ship.GameOrder;
                CanRemodelCheckBox.IsChecked = ship.CanRemodel;
                if (!string.IsNullOrEmpty(ship.RemodelDate) && DateTime.TryParseExact(ship.RemodelDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime remodelDate))
                {
                    RemodelDatePicker.Date = remodelDate;
                }

                // 获取方式
                AcquireMainBox.Text = ship.AcquireMain;
                AcquireDetailBox.Text = ship.AcquireDetail;
                BuildTimeBox.Text = ship.BuildTime;
                DropLocationsBox.Text = string.Join(";", ship.DropLocations);
                ShopExchangeBox.Text = ship.ShopExchange;
                IsPermanentCheckBox.IsChecked = ship.IsPermanent;

                // 实装活动
                DebutEventBox.Text = ship.DebutEvent;
                if (!string.IsNullOrEmpty(ship.ReleaseDate))
                {
                    if (DateTime.TryParseExact(ship.ReleaseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime releaseDate))
                        ReleaseDatePicker.Date = releaseDate;
                    // 如果解析失败，可以尝试普通解析作为后备
                    else if (DateTime.TryParse(ship.ReleaseDate, out releaseDate))
                        ReleaseDatePicker.Date = releaseDate;
                }
                NotesBox.Text = ship.Notes;

                // 属性加成
                SetComboBoxSelectedItem(ObtainBonusAttrCombo, ship.ObtainBonusAttr);
                ObtainBonusValueBox.Value = ship.ObtainBonusValue;
                SetComboBoxSelectedItem(Level120BonusAttrCombo, ship.Level120BonusAttr);
                Level120BonusValueBox.Value = ship.Level120BonusValue;

                // 获得时适用舰种（根据存储的列表勾选）
                ClearAllCheckboxes("ObtainAffect");
                foreach (var affect in ship.ObtainAffects)
                {
                    var suffix = GetCheckboxSuffix(affect);
                    if (!string.IsNullOrEmpty(suffix))
                        CheckCheckboxByName($"ObtainAffect{suffix}", true);
                }

                // 120级适用舰种
                ClearAllCheckboxes("Level120Affect");
                foreach (var affect in ship.Level120Affects)
                {
                    var suffix = GetCheckboxSuffix(affect);
                    if (!string.IsNullOrEmpty(suffix))
                        CheckCheckboxByName($"Level120Affect{suffix}", true);
                }

                // 舰队科技
                TechPointsObtainBox.Value = ship.TechPointsObtain;
                TechPointsMaxBox.Value = ship.TechPointsMax;
                TechPoints120Box.Value = ship.TechPoints120;

                // 特殊兵装
                CanSpecialGearCheckBox.IsChecked = ship.CanSpecialGear;
                SpecialGearNameBox.Text = ship.SpecialGearName;
                if (!string.IsNullOrEmpty(ship.SpecialGearDate))
                {
                    if (DateTime.TryParseExact(ship.SpecialGearDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime spDate))
                        SpecialGearDatePicker.Date = spDate;
                    else if (DateTime.TryParse(ship.SpecialGearDate, out spDate))
                        SpecialGearDatePicker.Date = spDate;
                }
                SpecialGearAcquireBox.Text = ship.SpecialGearAcquire;
            }
            finally
            {
                _isLoadingShipData = false;
                UpdateSpecialGearControlsEnabled();
            }
        }

        /// <summary>
        /// 从对话框中收集数据，返回 ShipStatic 对象
        /// </summary>
        public ShipStatic GetShip()
        {
            // 基本信息
            int id = (int)IdBox.Value;
            int gameOrder = (int)GameOrderBox.Value;
            string name = NameBox.Text.Trim();
            string altName = AltNameBox.Text.Trim();
            string faction = (FactionCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string shipClass = (ShipClassCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string rarity = (RarityCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            bool canRemodel = CanRemodelCheckBox.IsChecked == true;
            string remodelDate = canRemodel == true ? RemodelDatePicker.Date.ToString("yyyy-MM-dd") : "";

            // 获取方式
            string acquireMain = AcquireMainBox.Text;
            string acquireDetail = AcquireDetailBox.Text;
            string buildTime = BuildTimeBox.Text;
            List<string> dropLocations = DropLocationsBox.Text.Split(new[] { ';', '，' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(s => s.Trim()).ToList();
            string shopExchange = ShopExchangeBox.Text;
            bool isPermanent = IsPermanentCheckBox.IsChecked == true;

            // 实装活动
            string debutEvent = DebutEventBox.Text;
            string releaseDate = ReleaseDatePicker.Date.ToString("yyyy-MM-dd");
            string notes = NotesBox.Text;

            // 属性加成
            string obtainAttr = GetSelectedComboBoxContent(ObtainBonusAttrCombo);
            int obtainValue = (int)ObtainBonusValueBox.Value;
            string level120Attr = GetSelectedComboBoxContent(Level120BonusAttrCombo);
            int level120Value = (int)Level120BonusValueBox.Value;

            List<string> obtainAffects = new List<string>();
            if (ObtainAffectDD.IsChecked == true) obtainAffects.Add("驱逐");
            if (ObtainAffectCL.IsChecked == true) obtainAffects.Add("轻巡");
            if (ObtainAffectCA.IsChecked == true) obtainAffects.Add("重巡");
            if (ObtainAffectCB.IsChecked == true) obtainAffects.Add("超巡");
            if (ObtainAffectBC.IsChecked == true) obtainAffects.Add("战巡");
            if (ObtainAffectBB.IsChecked == true) obtainAffects.Add("战列");
            if (ObtainAffectBBV.IsChecked == true) obtainAffects.Add("航战");
            if (ObtainAffectCV.IsChecked == true) obtainAffects.Add("航母");
            if (ObtainAffectCVL.IsChecked == true) obtainAffects.Add("轻航");
            if (ObtainAffectAR.IsChecked == true) obtainAffects.Add("维修");
            if (ObtainAffectSS.IsChecked == true) obtainAffects.Add("潜艇");
            if (ObtainAffectSSV.IsChecked == true) obtainAffects.Add("潜母");
            if (ObtainAffectAE.IsChecked == true) obtainAffects.Add("运输");
            if (ObtainAffectSail.IsChecked == true) obtainAffects.Add("风帆");

            // 120级适用舰种
            List<string> level120Affects = new List<string>();
            if (Level120AffectDD.IsChecked == true) level120Affects.Add("驱逐");
            if (Level120AffectCL.IsChecked == true) level120Affects.Add("轻巡");
            if (Level120AffectCA.IsChecked == true) level120Affects.Add("重巡");
            if (Level120AffectCB.IsChecked == true) level120Affects.Add("超巡");
            if (Level120AffectBC.IsChecked == true) level120Affects.Add("战巡");
            if (Level120AffectBB.IsChecked == true) level120Affects.Add("战列");
            if (Level120AffectBBV.IsChecked == true) level120Affects.Add("航战");
            if (Level120AffectCV.IsChecked == true) level120Affects.Add("航母");
            if (Level120AffectCVL.IsChecked == true) level120Affects.Add("轻航");
            if (Level120AffectAR.IsChecked == true) level120Affects.Add("维修");
            if (Level120AffectSS.IsChecked == true) level120Affects.Add("潜艇");
            if (Level120AffectSSV.IsChecked == true) level120Affects.Add("潜母");
            if (Level120AffectAE.IsChecked == true) level120Affects.Add("运输");
            if (Level120AffectSail.IsChecked == true) level120Affects.Add("风帆");

            // 舰队科技
            int techPointsObtain = (int)TechPointsObtainBox.Value;
            int techPointsMax = (int)TechPointsMaxBox.Value;
            int techPoints120 = (int)TechPoints120Box.Value;

            // 特殊兵装
            bool canSpecialGear = CanSpecialGearCheckBox.IsChecked == true;
            string specialGearName = canSpecialGear ? SpecialGearNameBox.Text : "";
            string specialGearDate = canSpecialGear ? SpecialGearDatePicker.Date.ToString("yyyy-MM-dd") : "";
            string specialGearAcquire = canSpecialGear ? SpecialGearAcquireBox.Text : "";

            // 构建 ShipStatic 对象
            var ship = new ShipStatic
            {
                Id = id,
                Name = name,
                AltName = altName,
                Faction = faction,
                ShipClass = shipClass,
                Rarity = rarity,
                GameOrder = gameOrder,
                CanRemodel = canRemodel,
                RemodelDate = remodelDate,
                AcquireMain = acquireMain,
                AcquireDetail = acquireDetail,
                BuildTime = buildTime,
                DropLocations = dropLocations,
                ShopExchange = shopExchange,
                IsPermanent = isPermanent,
                DebutEvent = debutEvent,
                ReleaseDate = releaseDate,
                Notes = notes,
                ObtainBonusAttr = GetSelectedComboBoxContent(ObtainBonusAttrCombo),
                ObtainBonusValue = (int)ObtainBonusValueBox.Value,
                ObtainAffects = obtainAffects,
                Level120BonusAttr = GetSelectedComboBoxContent(Level120BonusAttrCombo),
                Level120BonusValue = (int)Level120BonusValueBox.Value,
                Level120Affects = level120Affects,
                TechPointsObtain = techPointsObtain,
                TechPointsMax = techPointsMax,
                TechPoints120 = techPoints120,
                CanSpecialGear = canSpecialGear,
                SpecialGearName = specialGearName,
                SpecialGearDate = specialGearDate,
                SpecialGearAcquire = specialGearAcquire,
                // Id 会在 ShipManager.AddShip 中自动分配
            };

            return ship;
        }

        private string GetSelectedComboBoxContent(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item)
                return item.Content?.ToString() ?? "";
            return "";
        }
    }
}