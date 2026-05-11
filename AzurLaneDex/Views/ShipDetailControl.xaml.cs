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
    private static Dictionary<string, BitmapImage> _avatarCache = new Dictionary<string, BitmapImage>();

    public ShipDetailControl()
    {
        this.InitializeComponent();
    }

    public void SetShip(ShipViewModel? ship)
    {
        _currentShip = ship;
        if (ship == null)
        {
            ClearDisplay();
            return;
        }

        // 基本信息
        ShipNameText.Text = ship.DisplayName;
        ShipIdText.Text = $"#{ship.Id}";
        FactionText.Text = ship.Faction;
        ShipClassText.Text = ship.ShipClass;
        RarityText.Text = ship.Rarity;
        SetAvatarFromName(ship.Name);
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
        bool hasSpecialGear = !string.IsNullOrEmpty(ship.SpecialGearName) || !string.IsNullOrEmpty(ship.SpecialGearDate) || !string.IsNullOrEmpty(ship.SpecialGearAcquire);
        SpecialGearBorder.Visibility = hasSpecialGear ? Visibility.Visible : Visibility.Collapsed;
        if (hasSpecialGear)
        {
            SpecialGearNameText.Text = ship.SpecialGearName;
            SpecialGearDateText.Text = ship.SpecialGearDate;
            SpecialGearAcquireText.Text = ship.SpecialGearAcquire;
        }
    }
    private void SetAvatarFromName(string shipName)
    {
        if (string.IsNullOrEmpty(shipName))
        {
            SetDefaultAvatar();
            return;
        }

        // 直接使用原始名称，不做额外编码
        string pngUri = $"ms-appx:///Assets/Ship/{shipName}.png";
        TryLoadImage(pngUri, (success) =>
        {
            if (!success)
            {
                string jpgUri = $"ms-appx:///Assets/Ship/{shipName}.jpg";
                TryLoadImage(jpgUri, (success2) =>
                {
                    if (!success2)
                    {
                        SetDefaultAvatar();
                    }
                });
            }
        });
    }

    private void TryLoadImage(string uriString, Action<bool> callback)
    {
        try
        {
            var uri = new Uri(uriString);
            var bitmap = new BitmapImage();
            // 监听加载失败事件
            bitmap.ImageFailed += (s, e) =>
            {
                callback?.Invoke(false);
            };
            bitmap.ImageOpened += (s, e) =>
            {
                callback?.Invoke(true);
            };
            bitmap.UriSource = uri;
            ShipAvatarImage.Source = bitmap;
        }
        catch
        {
            callback?.Invoke(false);
        }
    }
    private void SetDefaultAvatar()
    {
        var defaultUri = new Uri("ms-appx:///Assets/Ship/default.png");
        ShipAvatarImage.Source = new BitmapImage(defaultUri);
    }

    private void ClearDisplay()
    {
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
                _currentShip.Remodeled = RemodeledCheckBox.IsChecked ?? false;
                SaveShip();
            }
        }
        finally
        {
            _isUpdating = false;
        }
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
}