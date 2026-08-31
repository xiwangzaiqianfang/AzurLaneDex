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
using System.Threading.Tasks;
using Windows.Storage;

namespace AzurLaneDex.Views
{
    public sealed partial class ShipDetailControl : UserControl
    {
        private bool _isUpdating = false;
        private ShipViewModel? _currentShip;
        private int _avatarLoadToken = 0;
        private int _gearLoadToken = 0;

        public ShipDetailControl()
        {
            this.InitializeComponent();
        }

        public void SetShip(ShipViewModel? ship)
        {
            if (ship == null)
            {
                ClearDisplay();
                return;
            }

            // 增量更新（如果ID相同）
            if (_currentShip != null && _currentShip.Id == ship.Id)
            {
                bool remodelChanged = _currentShip.Retrofitted != ship.Retrofitted;
                _currentShip = ship;

                // 更新基本显示
                ShipNameText.Text = ship.DisplayName;
                RarityText.Text = ship.EffectiveRarity;

                // 状态控件
                OwnedCheckBox.IsChecked = ship.Owned;
                BreakthroughSlider.Value = ship.Breakthrough;
                BreakthroughValueText.Text = ship.Breakthrough.ToString();
                OathCheckBox.IsChecked = ship.Oath;
                Level120CheckBox.IsChecked = ship.Level120;
                RemodeledCheckBox.IsEnabled = ship.Retrofit.CanRetrofit && ship.Owned;
                RemodeledCheckBox.IsChecked = ship.Retrofitted;
                AffectionMaxCheckBox.IsChecked = ship.AffectionMax;
                Level125CheckBox.IsChecked = ship.Level125;
                ResearchLevelSlider.Value = ship.ResearchLevel;
                ResearchLevelText.Text = ship.ResearchLevel.ToString();
                FateLevelSlider.Value = ship.FateLevel;
                FateLevelText.Text = ship.FateLevel.ToString();

                // 兵装
                bool inc_hasGear = ship.SpecialGear != null;
                SpecialGearExpander.Visibility = inc_hasGear ? Visibility.Visible : Visibility.Collapsed;
                SpecialGearObtainedCheckBox.Visibility = inc_hasGear ? Visibility.Visible : Visibility.Collapsed;
                if (inc_hasGear)
                {
                    GearNameText.Text = ship.SpecialGear.Name.GetLocalized();
                    GearDateText.Text = string.IsNullOrEmpty(ship.SpecialGear.ReleaseDate) ? "未设定" : ship.SpecialGear.ReleaseDate;
                    GearAcquireText.Text = string.IsNullOrEmpty(ship.SpecialGear.AcquisitionMethod) ? "暂无获取方式" : ship.SpecialGear.AcquisitionMethod;

                    _gearLoadToken++;
                    int inc_token = _gearLoadToken;

                    SetDefaultGearIcon();
                    StartGearFadeIn();
                    LoadCustomGearIcon(ship.SpecialGear.Name.GetLocalized(), inc_token);
                }
                else
                {
                    GearNameText.Text = "";
                    GearDateText.Text = "";
                    GearAcquireText.Text = "";
                    SpecialGearImage.Opacity = 0;
                    SpecialGearImage.Source = null;
                    GearFadeInStoryboard.Stop();
                }

                // 改造头像刷新
                if (remodelChanged)
                {
                    string inc_avatarName = ship.Retrofitted && ship.Retrofit.CanRetrofit ? ship.RawName + "_g" : ship.RawName;
                    int inc_token = ++_avatarLoadToken;
                    ShipAvatarImage.Opacity = 0;
                    LoadAndFadeInAvatar(inc_avatarName, inc_token);
                }

                UpdateControlStates();
                return;
            }

            // 全新设置
            _currentShip = ship;

            // 基本信息
            ShipIdText.Text = ship.Id.ToString();
            ShipNameText.Text = ship.DisplayName;
            AltNameText.Text = ship.AltName;
            AliasText.Text = ship.Alias;
            ClassText.Text = ship.ClassName;
            CategoryText.Text = LocalizationHelper.GetEnumString("ShipCategory", (int)ship.CategoryEnum);
            ShipTypeText.Text = LocalizationHelper.GetEnumString("ShipType", (int)ship.ShipTypeEnum);
            RarityText.Text = ship.EffectiveRarity;
            FactionText.Text = LocalizationHelper.GetEnumString("Faction", (int)ship.FactionEnum);
            ReleaseDateText.Text = ship.ReleaseDate;
            IsPermanentText.Text = ship.IsPermanent ? "是" : "否";
            CvText.Text = ship.CV;
            ArtistText.Text = ship.Artist;
            RelatedEventText.Text = ship.RelatedEvent;
            RemarksText.Text = ship.Remarks;
            NotesText.Text = ship.Notes;

            // 属性
            HpText.Text = ship.Stats.Hp.ToString();
            ArmorText.Text = ship.Stats.Armor.ToString();
            FpText.Text = ship.Stats.Fp.ToString();
            TrpText.Text = ship.Stats.Trp.ToString();
            AaText.Text = ship.Stats.Aa.ToString();
            AviText.Text = ship.Stats.Avi.ToString();
            HitText.Text = ship.Stats.Hit.ToString();
            EvaText.Text = ship.Stats.Eva.ToString();
            AswText.Text = ship.Stats.Asw.ToString();
            LuckText.Text = ship.Stats.Luck.ToString();
            OilText.Text = ship.Stats.Oil.ToString();
            SpeedText.Text = ship.Stats.Speed.ToString("0.0");

            // 性能评级
            HpGradeText.Text = ship.Performance.Hp.ToString();
            AaGradeText.Text = ship.Performance.Aa.ToString();
            EvaGradeText.Text = ship.Performance.Eva.ToString();
            AviGradeText.Text = ship.Performance.Avi.ToString();
            TrpGradeText.Text = ship.Performance.Trp.ToString();
            FpGradeText.Text = ship.Performance.Fp.ToString();

            // 舰队科技
            CollectPointsText.Text = ship.FleetTech.CollectPoints.ToString();
            LimitBreakPointsText.Text = ship.FleetTech.LimitBreakPoints.ToString();
            Level120PointsText.Text = ship.FleetTech.Level120Points.ToString();

            // 获取方式
            AcquisitionMethodsItemsControl.ItemsSource = ship?.Acquisition?.Methods;

            // 装备槽
            EquipmentSlotsItemsControl.ItemsSource = ship.EquipmentSlots;

            // 初始装备
            InitialEquipmentItemsControl.ItemsSource = ship.InitialEquipment;

            // 专属兵装
            bool hasGear = ship.SpecialGear != null;
            HasSpecialGearText.Text = hasGear ? "可拥有" : "无";
            SpecialGearExpander.Visibility = hasGear ? Visibility.Visible : Visibility.Collapsed;
            if (hasGear)
            {
                GearNameText.Text = ship.SpecialGear.Name.GetLocalized();
                GearDateText.Text = ship.SpecialGear.ReleaseDate;
                GearIdText.Text = ship.SpecialGear.Id.ToString();
                GearAcquireText.Text = ship.SpecialGear.AcquisitionMethod;
            }
            else
            {
                GearNameText.Text = "";
                GearDateText.Text = "";
                GearIdText.Text = "";
                GearAcquireText.Text = "";
            }

            // 技能
            SkillsItemsControl.ItemsSource = ship.Skills;

            // 改造
            CanRetrofitText.Text = ship.Retrofit.CanRetrofit ? "可改造" : "不可改造";
            RetrofitShipTypeChangedText.Text = ship.Retrofit.ShipTypeChanged ? "改造后舰种变化" : "舰种不变";
            TargetShipTypeText.Text = ship.Retrofit.TargetShipType.ToString();
            RetrofitNodesItemsControl.ItemsSource = ship.Retrofit.Nodes;

            // 科研
            PreReqFactionsText.Text = string.Join(", ", ship.Research.PreRequisiteFactions);
            TechPointsText.Text = ship.Research.TechPoints.ToString();
            ResearchTasksItemsControl.ItemsSource = ship.Research.Tasks;
            BlueprintRequiredText.Text = ship.Research.BlueprintRequired.ToString();
            DevelopBonusItemsControl.ItemsSource = ship.Research.DevelopBonus.Select(kv => new { Key = $"{kv.Key}: {kv.Value}" });
            DevelopBlueprintRequiredText.Text = ship.Research.DevelopBlueprintRequired.ToString();
            if (ship.Research.HasFateSimulation && ship.Research.FateSim != null)
            {
                FateSimPanel.Visibility = Visibility.Visible;
                FateLevelText.Text = ship.Research.FateSim.Level.ToString();
                FateDescText.Text = ship.Research.FateSim.Description;
                FateBlueprintRequiredText.Text = ship.Research.FateSim.BlueprintRequired.ToString();
            }
            else
            {
                FateSimPanel.Visibility = Visibility.Collapsed;
            }

            // 皮肤
            SkinsItemsControl.ItemsSource = ship.Skins;

            // 台词
            LinesItemsControl.ItemsSource = ship.Lines;

            // 礼物偏好
            GiftPreferencesItemsControl.ItemsSource = ship.GiftPreferences;

            // 强化/退役
            CanBeEnhanceMaterialText.Text = ship.CanBeEnhanceMaterial ? "是" : "否";
            EnhanceValueText.Text = ship.EnhanceValue.ToString();
            CanRetireText.Text = ship.CanRetire ? "是" : "否";
            RetirementRewardText.Text = ship.RetirementReward;
            EnhanceExpFpText.Text = ship.EnhanceExp.Fp.ToString();
            EnhanceExpTrpText.Text = ship.EnhanceExp.Trp.ToString();
            EnhanceExpAviText.Text = ship.EnhanceExp.Avi.ToString();
            EnhanceExpRldText.Text = ship.EnhanceExp.Rld.ToString();
            EnhanceItemsText.Text = string.Join(", ", ship.EnhanceItems);
            ExtraEnhanceText.Text = ship.ExtraEnhance;

            // 参考资料
            ReferenceMarkdownText.Text = ship.ReferenceMarkdown;

            // 状态控件（复选框等）
            OwnedCheckBox.IsChecked = ship.Owned;
            BreakthroughSlider.Value = ship.Breakthrough;
            BreakthroughValueText.Text = ship.Breakthrough.ToString();
            OathCheckBox.IsChecked = ship.Oath;
            Level120CheckBox.IsChecked = ship.Level120;
            RemodeledCheckBox.IsEnabled = ship.Retrofit.CanRetrofit && ship.Owned;
            RemodeledCheckBox.IsChecked = ship.Retrofitted;
            SpecialGearObtainedCheckBox.IsChecked = ship.SpecialGearObtained;

            // 编辑按钮权限
            var app = Application.Current as App;
            bool isDev = app?.AccountManager?.IsDeveloper() ?? false;
            EditShipButton.Visibility = isDev ? Visibility.Visible : Visibility.Collapsed;

            UpdateControlStates();

            // 头像
            string avatarName = (ship.Retrofitted && ship.Retrofit.CanRetrofit) ? ship.RawName + "_g" : ship.RawName;
            int token = ++_avatarLoadToken;
            ShipAvatarImage.Opacity = 0;
            LoadAndFadeInAvatar(avatarName, token);
        }

        // ========== 头像加载相关（与旧版本一致） ==========
        private void SetDefaultAvatar()
        {
            var defaultUri = new Uri("ms-appx:///Assets/Ship/default.png");
            ShipAvatarImage.Source = new BitmapImage(defaultUri);
        }

        private void StartAvatarFadeIn()
        {
            AvatarFadeInStoryboard.Stop();
            ShipAvatarImage.Opacity = 0;
            AvatarFadeInStoryboard.Begin();
        }

        private void TryLoadImage(string uriString, int token, Action<bool> callback)
        {
            try
            {
                var uri = new Uri(uriString);
                var bitmap = new BitmapImage();
                bitmap.ImageFailed += (s, e) =>
                {
                    if (token == _avatarLoadToken)
                        callback?.Invoke(false);
                };
                bitmap.ImageOpened += (s, e) =>
                {
                    if (token == _avatarLoadToken)
                        callback?.Invoke(true);
                };
                bitmap.UriSource = uri;
                ShipAvatarImage.Source = bitmap;
            }
            catch
            {
                if (token == _avatarLoadToken)
                    callback?.Invoke(false);
            }
        }

        private async void LoadAndFadeInAvatar(string shipName, int token)
        {
            if (token != _avatarLoadToken) return;
            ShipAvatarImage.Opacity = 0;

            // 1. 尝试本地文件
            if (_currentShip != null)
            {
                string localPath = _currentShip.LocalAvatarPath;
                if (File.Exists(localPath))
                {
                    try
                    {
                        var file = await StorageFile.GetFileFromPathAsync(localPath);
                        using (var stream = await file.OpenReadAsync())
                        {
                            var bitmap = new BitmapImage();
                            await bitmap.SetSourceAsync(stream);
                            if (token == _avatarLoadToken)
                            {
                                ShipAvatarImage.Source = bitmap;
                                StartAvatarFadeIn();
                                return;
                            }
                        }
                    }
                    catch { /* 忽略本地加载失败 */ }
                }
            }

            // 2. 内置资源（优先 .png，再 .jpg）
            string pngUri = $"ms-appx:///Assets/Ship/{shipName}.png";
            TryLoadImage(pngUri, token, success =>
            {
                if (token != _avatarLoadToken) return;
                if (success)
                {
                    StartAvatarFadeIn();
                }
                else
                {
                    string jpgUri = $"ms-appx:///Assets/Ship/{shipName}.jpg";
                    TryLoadImage(jpgUri, token, success2 =>
                    {
                        if (token != _avatarLoadToken) return;
                        if (success2)
                        {
                            StartAvatarFadeIn();
                        }
                        else
                        {
                            SetDefaultAvatar();
                            StartAvatarFadeIn();
                        }
                    });
                }
            });
        }

        // ========== 兵装图标加载 ==========
        private void SetDefaultGearIcon()
        {
            var defaultUri = new Uri("ms-appx:///Assets/Gear/default.png");
            SpecialGearImage.Source = new BitmapImage(defaultUri);
        }

        private void StartGearFadeIn()
        {
            GearFadeInStoryboard.Stop();
            SpecialGearImage.Opacity = 0;
            GearFadeInStoryboard.Begin();
        }

        private async void LoadCustomGearIcon(string gearName, int token)
        {
            if (string.IsNullOrEmpty(gearName) || token != _gearLoadToken) return;
            string[] extensions = { ".jpg", ".png" };
            foreach (var ext in extensions)
            {
                string relativePath = $"Assets/Gear/{gearName}{ext}";
                try
                {
                    var uri = new Uri($"ms-appx:///{relativePath}");
                    var file = await StorageFile.GetFileFromApplicationUriAsync(uri);
                    using (var stream = await file.OpenReadAsync())
                    {
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(stream);
                        if (token == _gearLoadToken)
                        {
                            SpecialGearImage.Source = bitmap;
                            StartGearFadeIn();
                            return;
                        }
                    }
                }
                catch { /* 忽略 */ }
            }
        }

        // ========== 清空显示 ==========
        private void ClearDisplay()
        {
            ++_avatarLoadToken;
            ++_gearLoadToken;
            _currentShip = null;

            // 清空所有 TextBlock
            foreach (var child in FindVisualChildren<TextBlock>(this))
                child.Text = "";

            // 清空列表
            AcquisitionMethodsItemsControl.ItemsSource = null;
            EquipmentSlotsItemsControl.ItemsSource = null;
            InitialEquipmentItemsControl.ItemsSource = null;
            SkillsItemsControl.ItemsSource = null;
            RetrofitNodesItemsControl.ItemsSource = null;
            ResearchTasksItemsControl.ItemsSource = null;
            DevelopBonusItemsControl.ItemsSource = null;
            SkinsItemsControl.ItemsSource = null;
            LinesItemsControl.ItemsSource = null;
            GiftPreferencesItemsControl.ItemsSource = null;
            FateSimPanel.Visibility = Visibility.Collapsed;

            // 重置状态控件
            OwnedCheckBox.IsChecked = false;
            BreakthroughSlider.Value = 0;
            BreakthroughValueText.Text = "0";
            OathCheckBox.IsChecked = false;
            Level120CheckBox.IsChecked = false;
            RemodeledCheckBox.IsChecked = false;
            RemodeledCheckBox.IsEnabled = false;
            SpecialGearObtainedCheckBox.IsChecked = false;
            SpecialGearObtainedCheckBox.Visibility = Visibility.Collapsed;

            // 重置头像
            SetDefaultAvatar();
            AvatarFadeInStoryboard.Stop();
            ShipAvatarImage.Opacity = 1;

            // 重置兵装
            SpecialGearImage.Source = null;
            SpecialGearExpander.Visibility = Visibility.Collapsed;
            GearFadeInStoryboard.Stop();
        }

        // ========== 状态变更事件 ==========
        private void OnOwnedChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                bool newOwned = OwnedCheckBox.IsChecked ?? false;
                if (_currentShip.Owned != newOwned)
                {
                    _currentShip.Owned = newOwned;
                    if (!_currentShip.Owned)
                    {
                        _currentShip.Breakthrough = 0;
                        _currentShip.Oath = false;
                        _currentShip.Level120 = false;
                        _currentShip.Retrofitted = false;
                        _currentShip.SpecialGearObtained = false;
                        BreakthroughSlider.Value = 0;
                        OathCheckBox.IsChecked = false;
                        Level120CheckBox.IsChecked = false;
                        RemodeledCheckBox.IsChecked = false;
                        SpecialGearObtainedCheckBox.IsChecked = false;
                    }
                    RemodeledCheckBox.IsEnabled = _currentShip.Retrofit.CanRetrofit && _currentShip.Owned;
                    SaveShip();
                    UpdateControlStates();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 获得状态改为 {_currentShip.Owned}");
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnBreakthroughChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            if (IsSpecialBulin(_currentShip))
            {
                BreakthroughSlider.Value = 3;
                return;
            }
            _isUpdating = true;
            try
            {
                int newValue = (int)e.NewValue;
                if (_currentShip.Breakthrough != newValue)
                {
                    _currentShip.Breakthrough = newValue;
                    BreakthroughValueText.Text = newValue.ToString();
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 突破状态改为 {_currentShip.Breakthrough}");
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnOathChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                bool newOath = OathCheckBox.IsChecked ?? false;
                if (_currentShip.Oath != newOath)
                {
                    _currentShip.Oath = newOath;
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 誓约状态改为 {_currentShip.Oath}");
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnLevel120Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                bool newLevel120 = Level120CheckBox.IsChecked ?? false;
                if (_currentShip.Level120 != newLevel120)
                {
                    _currentShip.Level120 = newLevel120;
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 等级状态改为 {_currentShip.Level120}");
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnRemodeledChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                bool wasRemodeled = _currentShip.Retrofitted;
                bool newRemodeled = RemodeledCheckBox.IsChecked ?? false;
                if (wasRemodeled != newRemodeled)
                {
                    _currentShip.Retrofitted = newRemodeled;
                    SaveShip();
                    RefreshNameAndRarityDisplay();
                    RefreshAvatarForRemodel();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 改造状态改为 {_currentShip.Retrofitted}");
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnSpecialGearObtainedChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                bool newObtained = SpecialGearObtainedCheckBox.IsChecked ?? false;
                if (_currentShip.SpecialGearObtained != newObtained)
                {
                    _currentShip.SpecialGearObtained = newObtained;
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.RawName} (ID:{_currentShip.Id}) 专属兵装状态改为 {_currentShip.SpecialGearObtained}");
                }
            }
            finally { _isUpdating = false; }
        }

        // ========== 辅助方法 ==========
        private void RefreshNameAndRarityDisplay()
        {
            if (_currentShip == null) return;
            ShipNameText.Text = _currentShip.DisplayName;
            RarityText.Text = _currentShip.EffectiveRarity;
        }

        private void RefreshAvatarForRemodel()
        {
            if (_currentShip == null) return;
            string avatarName = _currentShip.Retrofitted && _currentShip.Retrofit.CanRetrofit
                                ? _currentShip.RawName + "_g"
                                : _currentShip.RawName;
            int token = ++_avatarLoadToken;
            ShipAvatarImage.Opacity = 0;
            LoadAndFadeInAvatar(avatarName, token);
        }

        private bool IsSpecialBulin(ShipViewModel? ship)
        {
            if (ship == null) return false;
            return ship.RawName == "泛用型布里"
                || ship.RawName == "试作型布里MKII"
                || ship.RawName == "特装型布里MKIII";
        }

        private void UpdateControlStates()
        {
            if (_currentShip == null) return;
            bool owned = _currentShip.Owned;
            bool isBulin = IsSpecialBulin(_currentShip);

            OwnedCheckBox.IsEnabled = true;
            BreakthroughSlider.IsEnabled = owned && !isBulin;
            OathCheckBox.IsEnabled = owned;
            Level120CheckBox.IsEnabled = owned;
            RemodeledCheckBox.IsEnabled = owned && _currentShip.Retrofit.CanRetrofit;
            SpecialGearObtainedCheckBox.IsEnabled = owned && _currentShip.SpecialGear != null;

            if (isBulin)
            {
                _currentShip.Breakthrough = 3;
                BreakthroughSlider.Value = 3;
                BreakthroughValueText.Text = "3";
            }
        }

        private void OnAffectionChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                _currentShip.AffectionMax = (sender as CheckBox)?.IsChecked ?? false;
                SaveShip();
            }
            finally { _isUpdating = false; }
        }

        private void OnLevel125Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                _currentShip.Level125 = (sender as CheckBox)?.IsChecked ?? false;
                SaveShip();
            }
            finally { _isUpdating = false; }
        }

        private void OnResearchLevelChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                int newValue = (int)e.NewValue;
                if (_currentShip.ResearchLevel != newValue)
                {
                    _currentShip.ResearchLevel = newValue;
                    ResearchLevelText.Text = newValue.ToString();
                    SaveShip();
                }
            }
            finally { _isUpdating = false; }
        }

        private void OnFateLevelChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdating || _currentShip == null) return;
            _isUpdating = true;
            try
            {
                int newValue = (int)e.NewValue;
                if (_currentShip.FateLevel != newValue)
                {
                    _currentShip.FateLevel = newValue;
                    FateLevelText.Text = newValue.ToString();
                    SaveShip();
                }
            }
            finally { _isUpdating = false; }
        }

        private async void SaveShip()
        {
            if (_currentShip == null) return;
            var app = Application.Current as App;
            await app?.ShipManager?.SaveAsync();
        }

        private async void EditShipButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentShip == null) return;
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            var app = Application.Current as App;
            if (app?.AccountManager?.IsDeveloper() != true)
            {
                var dialog = new ContentDialog
                {
                    Title = loader.GetString("InsufficientPrivilege_Title"),
                    Content = loader.GetString("EditShipNeedDeveloper_Message"),
                    CloseButtonText = loader.GetString("Common_Confirm"),
                    XamlRoot = this.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };
                await dialog.ShowAsync();
                return;
            }

            var shipStatic = _currentShip.GetStaticCopy();
            var editDialog = new AddShipDialog(shipStatic);
            editDialog.XamlRoot = this.XamlRoot;
            editDialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            if (await editDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var updatedShip = editDialog.GetShip();
                await app.ShipManager.UpdateShip(_currentShip.Id, updatedShip);
                var newVm = app.ShipManager.Ships.FirstOrDefault(s => s.Id == updatedShip.Id);
                if (newVm != null) SetShip(newVm);
            }
        }

        private IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var sub in FindVisualChildren<T>(child))
                    yield return sub;
            }
        }
    }
}