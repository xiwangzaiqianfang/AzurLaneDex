using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using AzurLaneDex.Services;
using AzurLaneDex.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using static AzurLaneDex.Models.ShipStatic;
using Exception = System.Exception;
using Uri = System.Uri;

namespace AzurLaneDex.Views;

public sealed partial class ShipDetailControl : UserControl
{
    private bool _isUpdating = false;
    private ShipViewModel? _currentShip;
    private int _avatarLoadToken = 0;
    private int _gearLoadToken = 0;

    public ShipDetailControl()
    {
        this.InitializeComponent();
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
    }

    public void SetShip(ShipViewModel? ship)
    {
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();

        // 添加 null 检查
        if (ship == null)
        {
            ClearDisplay();
            return;
        }

        if (_currentShip != null && ship != null && _currentShip.Id == ship.Id)
        {
            bool remodelChanged = _currentShip.Remodeled != ship.Remodeled;
            _currentShip = ship;

            ShipNameText.Text = ship.DisplayName;
            RarityText.Text = ship.EffectiveRarity;
            OwnedCheckBox.IsChecked = ship.Owned;
            BreakthroughSlider.Value = ship.Breakthrough;
            BreakthroughValueText.Text = ship.Breakthrough.ToString();
            OathCheckBox.IsChecked = ship.Oath;
            Level120CheckBox.IsChecked = ship.Level120;
            RemodeledCheckBox.IsEnabled = ship.CanRemodel && ship.Owned;
            RemodeledCheckBox.IsChecked = ship.Remodeled;
            SpecialGearBorder.Visibility = ship.CanSpecialGear ? Visibility.Visible : Visibility.Collapsed;
            if (ship.CanSpecialGear)
            {
                SpecialGearNameText.Text = ship.SpecialGearName.GetLocalized();
                SpecialGearDateText.Text = string.IsNullOrEmpty(ship.SpecialGearDate) ? "未设定" : ship.SpecialGearDate;
                if (ship.SpecialGearEntries != null && ship.SpecialGearEntries.Any())
                {
                    var displayTexts = ship.SpecialGearEntries.Select(entry => FormatGearEntry(entry)).Where(t => !string.IsNullOrEmpty(t));
                    SpecialGearAcquireText.Text = ship.SpecialGearAcquireText;
                }
                else
                {
                    SpecialGearAcquireText.Text = loader.GetString("NoAcquireInfo");
                }
                SetDefaultGearIcon();
                StartGearFadeIn();
                _gearLoadToken++;
                int token = _gearLoadToken;
                string gearNameChinese = ship.SpecialGearName.GetValueOrDefault("zh-Hans");
                if (!string.IsNullOrEmpty(gearNameChinese))
                    LoadCustomGearIcon(gearNameChinese, token);
            }
            else
            {
                SpecialGearNameText.Text = "";
                SpecialGearDateText.Text = "";
                SpecialGearAcquireText.Text = "";
                SpecialGearImage.Opacity = 0;
                SpecialGearImage.Source = null;
                GearFadeInStoryboard.Stop();
            }
            if (remodelChanged)
            {
                string avatarName0 = ship.Remodeled && ship.CanRemodel ? ship.RawName + "改" : ship.RawName;
                int token = ++_avatarLoadToken;
                ShipAvatarImage.Opacity = 0;
                LoadAndFadeInAvatar(avatarName0, ship.RawName, token);
            }
            UpdateControlStates();
            return;
        }
        else if (!string.IsNullOrEmpty(ship.AcquireMainLegacyText))
        {
            AcquireMainText.Text = ship.AcquireMainLegacyText;
        }
        _currentShip = ship;
        if (ship == null)
        {
            ClearDisplay();
            return;
        }
        int currentToken = ++_avatarLoadToken;
        int currentToken1 = ++_gearLoadToken;

        // 基本信息
        ShipNameText.Text = ship.DisplayName;
        ShipIdText.Text = ship.DisplayId;
        FactionText.Text = ship.Faction;
        ShipClassText.Text = ship.ShipClass;
        RarityText.Text = ship.EffectiveRarity;
        CanRemodelText.Text = ship.CanRemodel ? loader.GetString("Yes") : loader.GetString("No");
        RemodelDateText.Text = string.IsNullOrEmpty(ship.RemodelDate) ? loader.GetString("NotSet") : ship.RemodelDate;

        // 状态
        OwnedCheckBox.IsChecked = ship.Owned;
        BreakthroughSlider.Value = ship.Breakthrough;
        BreakthroughValueText.Text = ship.Breakthrough.ToString();
        OathCheckBox.IsChecked = ship.Oath;
        Level120CheckBox.IsChecked = ship.Level120;
        RemodeledCheckBox.IsEnabled = ship.CanRemodel && ship.Owned;
        RemodeledCheckBox.IsChecked = ship.Remodeled;
        SpecialGearObtainedCheckBox.IsChecked = ship.SpecialGearObtained;
        IsPermanentText.Text = ship.IsPermanent ? loader.GetString("Permanent") : loader.GetString("NotPermanent");

        // 属性加成
        if (!string.IsNullOrEmpty(ship.ObtainBonusAttr) && ship.ObtainBonusValue != 0)
            ObtainBonusText.Text = $"{ship.ObtainBonusAttr} +{ship.ObtainBonusValue}";
        else
            ObtainBonusText.Text = loader.GetString("None");
        ObtainAffectsText.Text = ship.ObtainAffectsDisplay; // 使用已本地化的显示属性
        if (!string.IsNullOrEmpty(ship.Level120BonusAttr) && ship.Level120BonusValue != 0)
            Level120BonusText.Text = $"{ship.Level120BonusAttr} +{ship.Level120BonusValue}";
        else
            Level120BonusText.Text = loader.GetString("None");
        Level120AffectsText.Text = ship.Level120AffectsDisplay;

        // 科技点
        TechPointsObtainText.Text = ship.TechPointsObtain.ToString();
        TechPointsMaxText.Text = ship.TechPointsMax.ToString();
        TechPoints120Text.Text = ship.TechPoints120.ToString();

        // 获取方式
        bool hasBuild = false;
        bool hasDrop = false;
        bool hasExchange = false;
        bool isUnbuildable = ship.AcquireEntries.Any(e => e.Tag == "acquire_11") ||
                     string.IsNullOrWhiteSpace(ship.BuildTime) ||
                     ship.BuildTime.Contains("无法建造");
        bool isUndroppable = ship.AcquireEntries.Any(e => e.Tag == "acquire_50") ||
                             ship.DropLocations == null || ship.DropLocations.Count == 0 ||
                             (ship.AcquireMainLegacyText?.Contains("无法打捞") ?? false);
        bool isUnexchangeable = ship.AcquireEntries.Any(e => e.Tag == "acquire_51") ||
                                string.IsNullOrWhiteSpace(ship.ShopExchange) ||
                                ship.ShopExchange.Contains("无法兑换");
        if (ship.AcquireEntries != null && ship.AcquireEntries.Any())
        {
            // 构建主要获取方式（Category 去重）
            var categories = new HashSet<string>();
            var detailItems = new List<string>();

            foreach (var entry in ship.AcquireEntries)
            {
                // 排除否定标记和打捞详情（acquire_61 已单独显示）
                if (entry.Tag == "acquire_11" || entry.Tag == "acquire_50" || entry.Tag == "acquire_51" || entry.Tag == "acquire_61")
                    continue;

                var tagDef = TagLibrary.GetAllTags().FirstOrDefault(t => t.Tag == entry.Tag);
                if (tagDef != null)
                {
                    categories.Add(tagDef.LocalizedCategory);
                    string detail = FormatAcquireEntry(entry);
                    if (!string.IsNullOrEmpty(detail))
                        detailItems.Add(detail);
                }
            }

            AcquireMainText.Text = string.Join("、", categories);
            AcquireDetailText.Text = string.Join("；", detailItems);
        }
        else if (!string.IsNullOrEmpty(ship.AcquireMainLegacyText))
        {
            // 降级兼容旧数据
            AcquireMainText.Text = ship.AcquireMainLegacyText;
            AcquireDetailText.Text = ship.AcquireDetailLegacyText;
        }
        else
        {
            AcquireMainText.Text = loader.GetString("None");
            AcquireDetailText.Text = "";
        }
        // 打捞地点独立展示
        var dropLocationParts = new List<string>();
        // 原有普通/档案/活动掉落点
        if (ship.DropLocations != null && ship.DropLocations.Any())
        {
            dropLocationParts.Add(string.Format(string.Join("、", ship.DropLocations)));
        }
        // 新增：处理 acquire_61（作战档案无冒号掉落）
        var archiveDropEntries = ship.AcquireEntries?.Where(e => e.Tag == "acquire_61").ToList();
        if (archiveDropEntries != null && archiveDropEntries.Any())
        {
            foreach (var entry in archiveDropEntries)
            {
                string formatted = FormatAcquireEntry(entry);
                if (!string.IsNullOrEmpty(formatted))
                    dropLocationParts.Add(formatted);
            }
        }

        if (dropLocationParts.Any())
        {
            DropLocationsText.Text = string.Join("；", dropLocationParts);
        }
        else if (ship.AcquireEntries?.Any(e => e.Tag == "acquire_50") == true)
        {
            DropLocationsText.Text = loader.GetString("acquire_50");
        }
        else
        {
            DropLocationsText.Text = "";
        }

        if (isUnbuildable)
            BuildTimeText.Text = loader.GetString("acquire_11"); // 资源文件中定义 "无法建造"
        else
            BuildTimeText.Text = ship.BuildTime;
        var exchangeEntries = ship.AcquireEntries.Where(e =>
            e.Tag.StartsWith("acquire_14") ||   // 舰队商店
            e.Tag.StartsWith("acquire_15") ||   // 军需商店
            e.Tag.StartsWith("acquire_16") ||   // META商店
            e.Tag.StartsWith("acquire_17") ||   // 核心商店
            e.Tag.StartsWith("acquire_18") ||   // 勋章商店
            e.Tag == "acquire_19" ||             // 原型商店
            e.Tag == "acquire_20" ||             // 活动商店
            e.Tag == "acquire_21" ||             // 礼包购买
            e.Tag == "acquire_22" ||             // 科研（但科研不一定是兑换）
            e.Tag == "acquire_23" ||             // 作战补给
            e.Tag == "acquire_24" ||             // 通用兑换
            e.Tag == "acquire_51");              // 无法兑换（否定标记）
        if (exchangeEntries.Any())
        {
            var exchangeTexts = exchangeEntries.Select(entry => FormatAcquireEntry(entry));
            ShopExchangeText.Text = string.Join("；", exchangeTexts);
        }
        else if (isUnexchangeable)
        {
            ShopExchangeText.Text = loader.GetString("acquire_51");
         }
        // "无法兑换"
        else
        {
            ShopExchangeText.Text = ship.ShopExchange;
        }
        // 实装活动
        DebutEventText.Text = string.IsNullOrEmpty(ship.DebutEvent) ? loader.GetString("None") : ship.DebutEvent;
        ReleaseDateText.Text = string.IsNullOrEmpty(ship.ReleaseDate) ? loader.GetString("None") : ship.ReleaseDate;
        NotesText.Text = string.IsNullOrEmpty(ship.Notes) ? "" : ship.Notes;
        bool hasEvent = !string.IsNullOrEmpty(ship.DebutEvent) || !string.IsNullOrEmpty(ship.ReleaseDate);
        EventBorder.Visibility = hasEvent ? Visibility.Visible : Visibility.Collapsed;

        // 特殊兵装
        SpecialGearBorder.Visibility = ship.CanSpecialGear ? Visibility.Visible : Visibility.Collapsed;
        if (ship.CanSpecialGear)
        {
            SpecialGearNameText.Text = ship.SpecialGearName.GetLocalized();
            SpecialGearDateText.Text = string.IsNullOrEmpty(ship.SpecialGearDate) ? loader.GetString("NotSet") : ship.SpecialGearDate;
            if (!string.IsNullOrEmpty(ship.SpecialGearAcquireText))
            {
                SpecialGearAcquireText.Text = ship.SpecialGearAcquireText;
            }
            else if (ship.SpecialGearEntries != null && ship.SpecialGearEntries.Any())
            {
                var displayTexts = ship.SpecialGearEntries.Select(entry => FormatGearEntry(entry)).Where(t => !string.IsNullOrEmpty(t));
                SpecialGearAcquireText.Text = string.Join("；", displayTexts);
            }
            else
            {
                SpecialGearAcquireText.Text = loader.GetString("NoAcquireInfo");
            }
            SetDefaultGearIcon();
            StartGearFadeIn();
            _gearLoadToken++;
            int token = _gearLoadToken;
            string gearNameChinese = ship.SpecialGearName.GetValueOrDefault("zh-Hans");
            if (!string.IsNullOrEmpty(gearNameChinese))
                LoadCustomGearIcon(gearNameChinese, token);
        }
        else
        {
            SpecialGearNameText.Text = "";
            SpecialGearDateText.Text = "";
            SpecialGearAcquireText.Text = "";
            SpecialGearImage.Opacity = 0;
            SpecialGearImage.Source = null;
        }

        var app = Application.Current as App;
        bool isDev = app?.AccountManager?.IsDeveloper() ?? false;
        EditShipButton.Visibility = isDev ? Visibility.Visible : Visibility.Collapsed;
        UpdateControlStates();
        if (string.IsNullOrEmpty(ship.RawName))
        {
            SetDefaultAvatar();
            StartAvatarFadeIn();
            return;
        }

        string avatarName = ship.Remodeled && ship.CanRemodel ? ship.RawName + "改" : ship.RawName;
        ShipAvatarImage.Opacity = 0;
        LoadAndFadeInAvatar(avatarName, ship.RawName, currentToken);
    }

    private string FormatGearEntry(SpecialGearEntry entry)
    {
        if (entry.Tag == "gear_custom")
            return entry.CustomText.GetLocalized();

        string template = GetGearTagTemplate(entry.Tag);
        if (string.IsNullOrEmpty(template)) return "";
        try
        {
            return string.Format(template, entry.Parameters.ToArray());
        }
        catch
        {
            return template;
        }
    }

    private string GetGearTagTemplate(string tag)
    {
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        return loader.GetString($"{tag}") ?? "";
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

    private void SetDefaultAvatar()
    {
        var defaultUri = new Uri("ms-appx:///Assets/Ship/default.png");
        ShipAvatarImage.Source = new BitmapImage(defaultUri);
    }

    private async void LoadAndFadeInAvatar(string shipName, string fallbackName, int token)
    {
        if (token != _avatarLoadToken) return;
        ShipAvatarImage.Opacity = 0;

        string jpgUri = $"ms-appx:///Assets/Ship/{shipName}.jpg";
        TryLoadImage(jpgUri, token, success =>
        {
            if (token != _avatarLoadToken) return;
            if (success)
            {
                StartAvatarFadeIn();
            }
            else
            {
                string pngUri = $"ms-appx:///Assets/Ship/{shipName}.png";
                TryLoadImage(pngUri, token, success2 =>
                {
                    if (token != _avatarLoadToken) return;
                    if (success2)
                    {
                        StartAvatarFadeIn();
                    }
                    else
                    {
                        if (shipName != fallbackName)
                        {
                            LoadAndFadeInAvatar(fallbackName, fallbackName, token);
                        }
                        else
                        {
                            SetDefaultAvatar();
                            StartAvatarFadeIn();
                        }
                    }
                });
            }
        });
    }

    private void StartAvatarFadeIn()
    {
        AvatarFadeInStoryboard.Stop();
        ShipAvatarImage.Opacity = 0;
        AvatarFadeInStoryboard.Begin();
    }

    private async void LoadCustomGearIcon(string gearName, int token)
    {
        if (string.IsNullOrEmpty(gearName) || token != _gearLoadToken) return;
        string[] extensions = { ".jpg", ".png" };
        bool loaded = false;
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
                        loaded = true;
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load {relativePath}: {ex.Message}");
            }
        }
        if (!loaded && token == _gearLoadToken)
        {
            // 所有扩展名都找不到，使用默认图标
            SetDefaultGearIcon();
            StartGearFadeIn();
        }
    }

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

    private void ClearDisplay()
    {
        ++_avatarLoadToken;
        ++_gearLoadToken;
        _currentShip = null;
        ShipNameText.Text = "";
        ShipIdText.Text = "";
        FactionText.Text = "";
        ShipClassText.Text = "";
        RarityText.Text = "";
        CanRemodelText.Text = "";
        RemodelDateText.Text = "";
        OwnedCheckBox.IsChecked = false;
        BreakthroughSlider.Value = 0;
        BreakthroughValueText.Text = "0";
        OathCheckBox.IsChecked = false;
        Level120CheckBox.IsChecked = false;
        RemodeledCheckBox.IsChecked = false;
        RemodeledCheckBox.IsEnabled = false;
        SpecialGearObtainedCheckBox.IsChecked = false;
        ObtainBonusText.Text = "";
        ObtainAffectsText.Text = "";
        Level120BonusText.Text = "";
        Level120AffectsText.Text = "";
        TechPointsObtainText.Text = "";
        TechPointsMaxText.Text = "";
        TechPoints120Text.Text = "";
        AcquireMainText.Text = "";
        AcquireDetailText.Text = "";
        BuildTimeText.Text = "";
        DropLocationsText.Text = "";
        ShopExchangeText.Text = "";
        IsPermanentText.Text = "";
        DebutEventText.Text = "";
        ReleaseDateText.Text = "";
        NotesText.Text = "";
        SpecialGearBorder.Visibility = Visibility.Collapsed;
        SpecialGearObtainedCheckBox.Visibility = Visibility.Collapsed;
        SpecialGearImage.Source = null;
        SetDefaultAvatar();
        AvatarFadeInStoryboard.Stop();
        ShipAvatarImage.Opacity = 1;
    }

    private void OnOwnedChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            if (_currentShip != null)
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
                        _currentShip.Remodeled = false;
                        _currentShip.SpecialGearObtained = false;
                        BreakthroughSlider.Value = 0;
                        OathCheckBox.IsChecked = false;
                        Level120CheckBox.IsChecked = false;
                        RemodeledCheckBox.IsChecked = false;
                        SpecialGearObtainedCheckBox.IsChecked = false;
                    }
                    RemodeledCheckBox.IsEnabled = _currentShip.CanRemodel && _currentShip.Owned;
                    SaveShip();
                    UpdateControlStates();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.DisplayName} (ID:{_currentShip.Id}) 获得状态改为 {_currentShip.Owned}");
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnBreakthroughChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdating) return;
        if (IsSpecialBulin(_currentShip))
        {
            BreakthroughSlider.Value = 3;
            return;
        }
        _isUpdating = true;
        try
        {
            if (_currentShip != null)
            {
                int newValue = (int)e.NewValue;
                if (_currentShip.Breakthrough != newValue)
                {
                    _currentShip.Breakthrough = newValue;
                    BreakthroughValueText.Text = newValue.ToString();
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.DisplayName} (ID:{_currentShip.Id}) 突破状态改为 {_currentShip.Breakthrough}");
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnOathChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            if (_currentShip != null)
            {
                bool newOath = OathCheckBox.IsChecked ?? false;
                if (_currentShip.Oath != newOath)
                {
                    _currentShip.Oath = newOath;
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.DisplayName} (ID:{_currentShip.Id}) 誓约状态改为 {_currentShip.Oath}");
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnLevel120Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            if (_currentShip != null)
            {
                bool newLevel120 = Level120CheckBox.IsChecked ?? false;
                if (_currentShip.Level120 != newLevel120)
                {
                    _currentShip.Level120 = newLevel120;
                    SaveShip();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.DisplayName} (ID:{_currentShip.Id}) 等级状态改为 {_currentShip.Level120}");
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void OnRemodeledChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            if (_currentShip != null)
            {
                bool wasRemodeled = _currentShip.Remodeled;
                bool newRemodeled = RemodeledCheckBox.IsChecked ?? false;
                SaveShip();

                if (wasRemodeled != newRemodeled)
                {
                    _currentShip.Remodeled = newRemodeled;
                    SaveShip();
                    RefreshNameAndRarityDisplay();
                    RefreshAvatarForRemodel();
                    LogService.Operation("状态变更", $"舰船 {_currentShip.DisplayName} (ID:{_currentShip.Id}) 改造状态改为 {_currentShip.Remodeled}");
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void RefreshNameAndRarityDisplay()
    {
        if (_currentShip == null) return;
        ShipNameText.Text = _currentShip.DisplayName;
        RarityText.Text = _currentShip.EffectiveRarity;
    }

    private void RefreshAvatarForRemodel()
    {
        if (_currentShip == null) return;
        string avatarName = _currentShip.Remodeled && _currentShip.CanRemodel
                            ? _currentShip.RawName + "改"
                            : _currentShip.RawName;
        int token = ++_avatarLoadToken;
        ShipAvatarImage.Opacity = 0;
        LoadAndFadeInAvatar(avatarName, _currentShip.RawName, token);
    }

    private void OnSpecialGearObtainedChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            bool newObtained = SpecialGearObtainedCheckBox.IsChecked ?? false;
            if (_currentShip != null)
            {
                _currentShip.SpecialGearObtained = newObtained;
                SaveShip();
                LogService.Operation("状态变更", $"舰船 {_currentShip.DisplayName} (ID:{_currentShip.Id}) 专属兵装状态改为 {_currentShip.SpecialGearObtained}");
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private async void SaveShip()
    {
        if (_currentShip == null) return;
        var app = Application.Current as App;
        app?.ShipManager?.Save();
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
            app.ShipManager.UpdateShip(_currentShip.Id, updatedShip);
            var newVm = app.ShipManager.Ships.FirstOrDefault(s => s.Id == updatedShip.Id);
            if (newVm != null) SetShip(newVm);
        }
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
        RemodeledCheckBox.IsEnabled = owned && _currentShip.CanRemodel;
        SpecialGearObtainedCheckBox.IsEnabled = owned && _currentShip.CanSpecialGear;
        if (isBulin)
        {
            _currentShip.Breakthrough = 3;
            BreakthroughSlider.Value = 3;
            BreakthroughValueText.Text = "3";
        }
    }
    private string FormatAcquireEntry(AcquireEntry entry)
    {
        if (entry.Tag == "acquire_custom")
        {
            // 返回当前语言的本地化文本，若无则 fallback 到中文
            return entry.CustomText.GetLocalized();
        }
        // 标准 Tag 处理
        string template = GetTagTemplate(entry.Tag);
        if (string.IsNullOrEmpty(template))
            return "";
        try
        {
            // 针对周年邀请函等需要转换中文数字的 Tag
            var parameters = entry.Parameters.ToArray();
            if (entry.Tag == "acquire_27" && parameters.Length > 0)
            {
                // 将第一个参数中的中文数字转为阿拉伯数字
                parameters[0] = ChineseToArabic(parameters[0]);
            }
            // 其他需要转换的 Tag 可继续添加
            return string.Format(template, parameters);
        }
        catch
        {
            return template;
        }
    }
    private string ChineseToArabic(string chineseNum)
    {
        var map = new Dictionary<string, string>
    {
        {"一", "1"}, {"二", "2"}, {"三", "3"}, {"四", "4"}, {"五", "5"},
        {"六", "6"}, {"七", "7"}, {"八", "8"}, {"九", "9"}, {"十", "10"}
    };
        if (map.ContainsKey(chineseNum))
            return map[chineseNum];
        return chineseNum; // 如果已经是数字，直接返回
    }
    private string GetTagTemplate(string tag)
    {
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        return loader.GetString($"{tag}") ?? "";
    }
}