using AzurLaneDex.ViewModels;
using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AzurLaneDex.Views;

public sealed partial class ShipDetailControl : UserControl
{
    private bool _isUpdating = false;
    private ShipViewModel? _currentShip;
    private int _avatarLoadToken = 0;
    public ShipDetailControl()
    {
        this.InitializeComponent();
    }

    public void SetShip(ShipViewModel? ship)
    {
        if (_currentShip != null && ship != null && _currentShip.Id == ship.Id)
        {
            // 记录改造状态是否变化（在更新 UI 前比较）
            bool remodelChanged = _currentShip.Remodeled != ship.Remodeled;

            // 更新引用（重要！）
            _currentShip = ship;

            // 仅增量更新变化的部分
            ShipNameText.Text = ship.DisplayName;
            RarityText.Text = ship.EffectiveRarity;
            OwnedCheckBox.IsChecked = ship.Owned;
            BreakthroughSlider.Value = ship.Breakthrough;
            BreakthroughValueText.Text = ship.Breakthrough.ToString();
            OathCheckBox.IsChecked = ship.Oath;
            Level120CheckBox.IsChecked = ship.Level120;
            RemodeledCheckBox.IsEnabled = ship.CanRemodel && ship.Owned;
            RemodeledCheckBox.IsChecked = ship.Remodeled;
            SpecialGearObtainedCheckBox.IsChecked = ship.SpecialGearObtained;
            SpecialGearBorder.Visibility = ship.CanSpecialGear ? Visibility.Visible : Visibility.Collapsed;
            SpecialGearObtainedCheckBox.Visibility = ship.CanSpecialGear ? Visibility.Visible : Visibility.Collapsed;
            if (remodelChanged)
            {
                string avatarName0 = ship.Remodeled && ship.CanRemodel ? ship.Name + "改" : ship.Name;
                int token = ++_avatarLoadToken;
                ShipAvatarImage.Opacity = 0;
                LoadAndFadeInAvatar(avatarName0, ship.Name, token);
            }
            UpdateControlStates();
            return;
        }
        _currentShip = ship;
        if (ship == null)
        {
            ClearDisplay();
            return;
        }
        int currentToken = ++_avatarLoadToken;

        // 基本信息
        ShipNameText.Text = ship.DisplayName;
        ShipIdText.Text = $"#{ship.Id}";
        FactionText.Text = ship.Faction;
        ShipClassText.Text = ship.ShipClass;
        RarityText.Text = ship.EffectiveRarity;
        // 改造相关
        CanRemodelText.Text = ship.CanRemodel ? "是" : "否";
        RemodelDateText.Text = string.IsNullOrEmpty(ship.RemodelDate) ? "未设定" : ship.RemodelDate;        

        // 状态
        OwnedCheckBox.IsChecked = ship.Owned;
        BreakthroughSlider.Value = ship.Breakthrough;
        BreakthroughValueText.Text = ship.Breakthrough.ToString();
        OathCheckBox.IsChecked = ship.Oath;
        Level120CheckBox.IsChecked = ship.Level120;
        RemodeledCheckBox.IsEnabled = ship.CanRemodel && ship.Owned;
        RemodeledCheckBox.IsChecked = ship.Remodeled;
        SpecialGearObtainedCheckBox.IsChecked = ship.SpecialGearObtained;

        // 属性加成
        if (!string.IsNullOrEmpty(ship.ObtainBonusAttr) && ship.ObtainBonusValue != 0)
            ObtainBonusText.Text = $"{ship.ObtainBonusAttr} +{ship.ObtainBonusValue}";
        else
            ObtainBonusText.Text = "无";
        ObtainAffectsText.Text = ship.ObtainAffects.Count > 0 ? string.Join(", ", ship.ObtainAffects) : "无限制";

        if (!string.IsNullOrEmpty(ship.Level120BonusAttr) && ship.Level120BonusValue != 0)
            Level120BonusText.Text = $"{ship.Level120BonusAttr} +{ship.Level120BonusValue}";
        else
            Level120BonusText.Text = "无";
        Level120AffectsText.Text = ship.Level120Affects.Count > 0 ? string.Join(", ", ship.Level120Affects) : "无限制";

        // 科技点
        TechPointsObtainText.Text = ship.TechPointsObtain.ToString();
        TechPointsMaxText.Text = ship.TechPointsMax.ToString();
        TechPoints120Text.Text = ship.TechPoints120.ToString();

        // 获取方式
        AcquireMainText.Text = string.IsNullOrEmpty(ship.AcquireMain) ? "无" : ship.AcquireMain;
        AcquireDetailText.Text = string.IsNullOrEmpty(ship.AcquireDetail) ? "" : ship.AcquireDetail;
        BuildTimeText.Text = string.IsNullOrEmpty(ship.BuildTime) ? "" : $"{ship.BuildTime}";
        DropLocationsText.Text = ship.DropLocations.Count > 0 ? $"打捞地点： {string.Join(", ", ship.DropLocations)}" : "";
        ShopExchangeText.Text = string.IsNullOrEmpty(ship.ShopExchange) ? "" : $"{ship.ShopExchange}";
        IsPermanentText.Text = ship.IsPermanent ? "常驻" : "未常驻";

        bool hasAcquire = !string.IsNullOrEmpty(ship.AcquireMain) || !string.IsNullOrEmpty(ship.AcquireDetail) ||
                          !string.IsNullOrEmpty(ship.BuildTime) || ship.DropLocations.Count > 0 ||
                          !string.IsNullOrEmpty(ship.ShopExchange);
        AcquireBorder.Visibility = hasAcquire ? Visibility.Visible : Visibility.Collapsed;

        // 实装活动
        DebutEventText.Text = string.IsNullOrEmpty(ship.DebutEvent) ? "无" : ship.DebutEvent;
        ReleaseDateText.Text = string.IsNullOrEmpty(ship.ReleaseDate) ? "无" : ship.ReleaseDate;
        NotesText.Text = string.IsNullOrEmpty(ship.Notes) ? "" : ship.Notes;
        bool hasEvent = !string.IsNullOrEmpty(ship.DebutEvent) || !string.IsNullOrEmpty(ship.ReleaseDate);
        EventBorder.Visibility = hasEvent ? Visibility.Visible : Visibility.Collapsed;

        // 特殊兵装
        SpecialGearBorder.Visibility = ship.CanSpecialGear ? Visibility.Visible : Visibility.Collapsed;
        SpecialGearObtainedCheckBox.Visibility = ship.CanSpecialGear ? Visibility.Visible : Visibility.Collapsed;
        var app = Application.Current as App;
        bool isDev = app?.AccountManager?.IsDeveloper() ?? false;
        EditShipButton.Visibility = isDev ? Visibility.Visible : Visibility.Collapsed;
        UpdateControlStates();
        if (string.IsNullOrEmpty(ship.Name))
        {
            SetDefaultAvatar();
            StartAvatarFadeIn();
            return;
        }

        string avatarName = ship.Remodeled && ship.CanRemodel ? ship.Name + "改" : ship.Name;

        ShipAvatarImage.Opacity = 0;
        LoadAndFadeInAvatar(avatarName, ship.Name, currentToken);
    }

    private void TryLoadImage(string uriString, int token, Action<bool> callback)
    {
        try
        {
            var uri = new Uri(uriString);
            var bitmap = new BitmapImage();
            // 监听加载失败事件
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

    // 实际加载头像资源并淡入
    private async void LoadAndFadeInAvatar(string shipName, string fallbackName, int token)
    {
        // 再次检查是否仍是当前期望的舰船（可能快速切换）
        if (token != _avatarLoadToken) return;
        // 重置头像为透明（准备淡入）
        ShipAvatarImage.Opacity = 0;

        // 尝试加载 png/jpg
        string jpgUri = $"ms-appx:///Assets/Ship/{shipName}.jpg";
        TryLoadImage(jpgUri, token, success =>
        {
            // 再次检查 pending 是否已变化
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
        AvatarFadeInStoryboard.Stop();      // 停止可能正在播放的动画
        ShipAvatarImage.Opacity = 0;        // 从完全透明开始淡入
        AvatarFadeInStoryboard.Begin();
    }

    private void ClearDisplay()
    {
        ++_avatarLoadToken;
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
                _currentShip.Owned = OwnedCheckBox.IsChecked ?? false;
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
        // 特殊布里禁止手动更改突破
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
                _currentShip.Breakthrough = newValue;
                BreakthroughValueText.Text = newValue.ToString();
                SaveShip();
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
                _currentShip.Oath = OathCheckBox.IsChecked ?? false;
                SaveShip();
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
                _currentShip.Level120 = Level120CheckBox.IsChecked ?? false;
                SaveShip();
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
                _currentShip.Remodeled = RemodeledCheckBox.IsChecked ?? false;
                SaveShip();

                // 如果改造状态实际发生了变化，立即刷新名称、稀有度、头像
                if (wasRemodeled != _currentShip.Remodeled)
                {
                    RefreshNameAndRarityDisplay();
                    RefreshAvatarForRemodel();
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
                            ? _currentShip.Name + "改"
                            : _currentShip.Name;
        int token = ++_avatarLoadToken;
        ShipAvatarImage.Opacity = 0;
        LoadAndFadeInAvatar(avatarName, _currentShip.Name, token);
    }

    private void OnSpecialGearObtainedChanged(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            if (_currentShip != null)
            {
                _currentShip.SpecialGearObtained = SpecialGearObtainedCheckBox.IsChecked ?? false;
                SaveShip();
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

        var app = Application.Current as App;
        if (app?.AccountManager?.IsDeveloper() != true)
        {
            var dialog = new ContentDialog
            {
                Title = "权限不足",
                Content = "只有开发者账户才能编辑舰船",
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }

        var shipStatic = _currentShip.GetStaticCopy(); // 需要实现此方法
        var editDialog = new AddShipDialog(shipStatic);
        editDialog.XamlRoot = this.XamlRoot;
        if (await editDialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var updatedShip = editDialog.GetShip();
            var manager = app?.ShipManager;
            app.ShipManager.UpdateShip(_currentShip.Id, updatedShip);
            var newVm = app.ShipManager.Ships.FirstOrDefault(s => s.Id == updatedShip.Id);
            if (newVm != null) SetShip(newVm);
        }
    }
    // 判断是否为三艘特殊布里
    private bool IsSpecialBulin(ShipViewModel? ship)
    {
        if (ship == null) return false;
        return ship.Name == "泛用型布里"
            || ship.Name == "试作型布里MKII"
            || ship.Name == "特装型布里MKIII";
    }

    // 根据当前舰船状态更新所有控件的启用/禁用状态
    private void UpdateControlStates()
    {
        if (_currentShip == null) return;

        bool owned = _currentShip.Owned;
        bool isBulin = IsSpecialBulin(_currentShip);

        // 获得复选框：特殊布里是否允许取消获得？若不希望取消，可设置为 false，这里保持可操作
        OwnedCheckBox.IsEnabled = true;
        BreakthroughSlider.IsEnabled = owned && !isBulin;
        OathCheckBox.IsEnabled = owned;
        Level120CheckBox.IsEnabled = owned;
        RemodeledCheckBox.IsEnabled = owned && _currentShip.CanRemodel;
        SpecialGearObtainedCheckBox.IsEnabled = owned && _currentShip.CanSpecialGear;

        // 特殊布里强制突破为3
        if (isBulin)
        {
            _currentShip.Breakthrough = 3;
            BreakthroughSlider.Value = 3;
            BreakthroughValueText.Text = "3";
        }
    }
}