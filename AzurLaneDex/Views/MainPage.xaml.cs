using AzurLaneDex.Helpers;
using AzurLaneDex.Models;
using AzurLaneDex.Services;
using AzurLaneDex.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace AzurLaneDex.Views;

public class SuggestionItem
{
    public string DisplayText { get; set; }
    public string SearchText { get; set; }
}

public sealed partial class MainPage : Page
{
    private ShipManager _shipManager;
    private ObservableCollection<ShipViewModel> _currentShips = new();
    private FilterCriteria _currentFilterCriteria;
    private int _lastSelectedShipId = -1;
    private double _lastScrollOffset = 0;
    private bool _isRefreshing = false;
    private List<SuggestionItem> _allSuggestions = new();
    private List<SuggestionItem> _currentSuggestions = new();
    private ShipCategory? _currentCategoryFilter = null;

    // 响应式布局相关
    private Window _mainWindow;
    private bool _isAscending = true;
    private DataTemplate _fullTemplate;
    private DataTemplate _compactTemplate;
    private DataTemplate _minimalTemplate;

    public MainPage()
    {
        this.InitializeComponent();
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        this.Loaded += MainPage_Loaded;
        this.SizeChanged += MainPage_SizeChanged;

        _fullTemplate = (DataTemplate)Resources["FullTemplate"];
        _compactTemplate = (DataTemplate)Resources["CompactTemplate"];
        _minimalTemplate = (DataTemplate)Resources["MinimalTemplate"];
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        var app = (App)Application.Current;
        int retry = 0;
        while (app?.ShipManager == null && retry < 100)
        {
            await Task.Delay(100);
            retry++;
        }
        if (app?.ShipManager == null)
        {
            System.Diagnostics.Debug.WriteLine("ShipManager is still null after waiting.");
            return;
        }

        _shipManager = app.ShipManager;
        ShipListView.ItemsSource = _currentShips;
        if (_shipManager != null)
        {
            AddShipButton.Visibility = app.AccountManager.IsDeveloper()
                ? Visibility.Visible
                : Visibility.Collapsed;
            _shipManager.DataStructureChanged += () => DispatcherQueue.TryEnqueue(() => OnDataChanged());
            _shipManager.StateChanged += () => DispatcherQueue.TryEnqueue(() => ApplyCurrentSort());
        }
        BuildSuggestionSource();
        if (CategorySelector.Items.Count > 0)
            CategorySelector.SelectedItem = CategorySelector.Items[0];
        _currentCategoryFilter = ShipCategory.Normal;
        RefreshShipList();

        // 周年庆信息栏
        DateTime now = DateTime.Now;
        DateTime activityStart = new DateTime(2026, 5, 19);
        DateTime activityEnd = new DateTime(2026, 6, 12, 23, 59, 59);
        bool isInActivity = now >= activityStart && now <= activityEnd;

        if (!isInActivity)
        {
            AnniversaryInfoBar.IsOpen = false;
        }
        else
        {
            bool userClosed = false;
            if (app.ShipManager?.Config != null &&
                app.ShipManager.Config.TryGetValue("anniversary_2026_closed", out var closedObj))
            {
                bool.TryParse(closedObj.ToString(), out userClosed);
            }
            AnniversaryInfoBar.IsOpen = !userClosed;
            AnniversaryInfoBar.Closed += (s, args) =>
            {
                if (app.ShipManager?.Config != null)
                {
                    app.ShipManager.Config["anniversary_2026_closed"] = true;
                    app.ShipManager.SaveConfig();
                }
            };
        }

        // 获取主窗口以监听大小变化
        _mainWindow = app.GetMainWindow();
        if (_mainWindow != null)
        {
            _mainWindow.SizeChanged += MainWindow_SizeChanged;
            UpdateListViewTemplate(_mainWindow.Bounds.Width);
        }
        else
        {
            var window = Window.Current;
            if (window != null)
            {
                window.SizeChanged += MainWindow_SizeChanged;
                UpdateListViewTemplate(window.Bounds.Width);
            }
        }
    }

    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        UpdateListViewTemplate(args.Size.Width);
    }

    private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateListViewTemplate(e.NewSize.Width);
    }

    private void UpdateListViewTemplate(double windowWidth)
    {
        DataTemplate template;
        bool showFull, showCompact, showMinimal;
        if (windowWidth >= 1000)
        {
            template = _fullTemplate;
            showFull = true; showCompact = false; showMinimal = false;
        }
        else if (windowWidth >= 830)
        {
            template = _compactTemplate;
            showFull = false; showCompact = true; showMinimal = false;
        }
        else
        {
            template = _minimalTemplate;
            showFull = false; showCompact = false; showMinimal = true;
        }

        if (ShipListView.ItemTemplate != template)
        {
            int? selectedId = (ShipListView.SelectedItem as ShipViewModel)?.Id;
            var scrollViewer = FindScrollViewer(ShipListView);
            double? verticalOffset = scrollViewer?.VerticalOffset;

            ShipListView.ItemTemplate = template;

            FullHeader.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
            CompactHeader.Visibility = showCompact ? Visibility.Visible : Visibility.Collapsed;
            MinimalHeader.Visibility = showMinimal ? Visibility.Visible : Visibility.Collapsed;

            if (selectedId.HasValue && ShipListView.ItemsSource is IEnumerable<ShipViewModel> items)
            {
                var selectedItem = items.FirstOrDefault(s => s.Id == selectedId.Value);
                if (selectedItem != null)
                    ShipListView.SelectedItem = selectedItem;
            }
            if (scrollViewer != null && verticalOffset.HasValue)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    scrollViewer.ChangeView(null, verticalOffset.Value, null);
                });
            }
        }
    }

    private ScrollViewer FindScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv) return sv;
            var result = FindScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }

    private void CategorySelector_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (sender.SelectedItem is SelectorBarItem selectedItem && selectedItem.Tag is string tag)
        {
            _currentCategoryFilter = tag switch
            {
                "Normal" => ShipCategory.Normal,
                "Collab" => ShipCategory.Collab,
                "Research" => ShipCategory.Research,
                "META" => ShipCategory.META,
                _ => null
            };
            _currentFilterCriteria = null;
            RefreshShipList();
        }
    }

    private void BuildSuggestionSource()
    {
        _allSuggestions.Clear();

        bool IsValidText(string text) => !string.IsNullOrEmpty(text) && !text.Contains('\\') && !text.Contains('/');

        foreach (var ship in _shipManager.Ships)
        {
            string rawName = ship.RawName;
            if (IsValidText(rawName))
                _allSuggestions.Add(new SuggestionItem { DisplayText = rawName, SearchText = rawName });
        }
        foreach (var ship in _shipManager.Ships)
        {
            string rawAlt = ship.AltName;
            if (IsValidText(rawAlt))
                _allSuggestions.Add(new SuggestionItem { DisplayText = $"[和谐名称] {rawAlt}", SearchText = rawAlt });
        }
        foreach (var ship in _shipManager.Ships)
        {
            string gear = ship.SpecialGear.Name.GetValueOrDefault("zh-Hans");
            if (IsValidText(gear))
                _allSuggestions.Add(new SuggestionItem { DisplayText = $"[专属兵装] {gear}", SearchText = gear });
        }
        var eventNames = _shipManager.Ships
            .Where(s => IsValidText(s.RelatedEvent))
            .Select(s => s.RelatedEvent)
            .Distinct();
        foreach (var evt in eventNames)
        {
            _allSuggestions.Add(new SuggestionItem { DisplayText = $"[活动] {evt}", SearchText = evt });
        }
        var acquireKeywords = new[] { "仅限打捞", "轻型池建造", "重型池建造", "特型池建造", "勋章支援", "舰队商店", "军需商店" };
        foreach (var kw in acquireKeywords)
        {
            _allSuggestions.Add(new SuggestionItem { DisplayText = $"[获取方式] {kw}", SearchText = kw });
        }
    }

    private void OnDataChanged()
    {
        int? selectedId = (ShipListView.SelectedItem as ShipViewModel)?.Id;
        var scrollViewer = FindScrollViewer(ShipListView);
        double? verticalOffset = scrollViewer?.VerticalOffset;

        RefreshShipList();

        if (selectedId.HasValue)
        {
            var newSelected = _currentShips.FirstOrDefault(s => s.Id == selectedId.Value);
            if (newSelected != null)
            {
                ShipListView.SelectedItem = newSelected;
                if (scrollViewer != null && verticalOffset.HasValue)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        scrollViewer.ChangeView(null, verticalOffset.Value, null);
                    });
                }
            }
        }
    }

    private void RefreshShipList()
    {
        if (_shipManager == null) return;
        var source = _shipManager.Ships.AsEnumerable();

        string keyword = SearchBox.Text?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            source = source.Where(s =>
                s.RawName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (s.AltName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.SpecialGear.Name.GetValueOrDefault("zh-Hans")?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.RelatedEvent?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            );
        }
        if (_currentFilterCriteria != null)
        {
            source = ApplyFilterCriteria(source, _currentFilterCriteria);
        }
        if (_currentCategoryFilter.HasValue)
        {
            source = source.Where(s => s.CategoryEnum == _currentCategoryFilter.Value);
        }

        int sortIndex = SortCombo.SelectedIndex;
        IEnumerable<ShipViewModel> sorted;
        // 排序索引映射：
        // 0: 编号, 1: 图鉴顺序, 2: 名称, 3: 稀有度,
        // 4: 获得状态, 5: 突破, 6: 誓约, 7: 120级
        switch (sortIndex)
        {
            case 0:
                sorted = _isAscending ? source.OrderBy(s => s.Id) : source.OrderByDescending(s => s.Id);
                break;
            case 1:
                sorted = _isAscending ? source.OrderBy(s => s.GameOrder) : source.OrderByDescending(s => s.GameOrder);
                break;
            case 2:
                sorted = _isAscending ? source.OrderBy(s => s.RawName) : source.OrderByDescending(s => s.RawName);
                break;
            case 3:
                sorted = _isAscending ? source.OrderBy(s => s.RarityEnum) : source.OrderByDescending(s => s.RarityEnum);
                break;
            case 4:
                sorted = _isAscending ? source.OrderBy(s => !s.Owned) : source.OrderByDescending(s => !s.Owned);
                break;
            case 5:
                sorted = _isAscending ? source.OrderBy(s => s.Breakthrough) : source.OrderByDescending(s => s.Breakthrough);
                break;
            case 6:
                sorted = _isAscending ? source.OrderBy(s => !s.Oath) : source.OrderByDescending(s => !s.Oath);
                break;
            case 7:
                sorted = _isAscending ? source.OrderBy(s => !s.Level120) : source.OrderByDescending(s => !s.Level120);
                break;
            default:
                sorted = _isAscending ? source.OrderBy(s => s.Id) : source.OrderByDescending(s => s.Id);
                break;
        }

        _currentShips.Clear();
        foreach (var ship in sorted)
            _currentShips.Add(ship);

        SelectAllCheckBox.IsChecked = false;
        CompactSelectAllCheckBox.IsChecked = false;
        MinimalSelectAllCheckBox.IsChecked = false;
        foreach (var ship in _currentShips)
            ship.IsSelected = false;
    }

    private void ApplyCurrentSort()
    {
        if (_currentShips == null || _currentShips.Count == 0) return;
        int sortIndex = SortCombo.SelectedIndex;
        List<ShipViewModel> sorted;
        switch (sortIndex)
        {
            case 0:
                sorted = _isAscending ? _currentShips.OrderBy(s => s.Id).ToList() : _currentShips.OrderByDescending(s => s.Id).ToList();
                break;
            case 1:
                sorted = _isAscending ? _currentShips.OrderBy(s => s.GameOrder).ToList() : _currentShips.OrderByDescending(s => s.GameOrder).ToList();
                break;
            case 2:
                sorted = _isAscending ? _currentShips.OrderBy(s => s.RawName).ToList() : _currentShips.OrderByDescending(s => s.RawName).ToList();
                break;
            case 3:
                sorted = _isAscending ? _currentShips.OrderBy(s => s.RarityEnum).ToList() : _currentShips.OrderByDescending(s => s.RarityEnum).ToList();
                break;
            case 4:
                sorted = _isAscending ? _currentShips.OrderBy(s => !s.Owned).ToList() : _currentShips.OrderByDescending(s => !s.Owned).ToList();
                break;
            case 5:
                sorted = _isAscending ? _currentShips.OrderBy(s => s.Breakthrough).ToList() : _currentShips.OrderByDescending(s => s.Breakthrough).ToList();
                break;
            case 6:
                sorted = _isAscending ? _currentShips.OrderBy(s => !s.Oath).ToList() : _currentShips.OrderByDescending(s => !s.Oath).ToList();
                break;
            case 7:
                sorted = _isAscending ? _currentShips.OrderBy(s => !s.Level120).ToList() : _currentShips.OrderByDescending(s => !s.Level120).ToList();
                break;
            default:
                sorted = _isAscending ? _currentShips.OrderBy(s => s.Id).ToList() : _currentShips.OrderByDescending(s => s.Id).ToList();
                break;
        }

        for (int i = 0; i < sorted.Count; i++)
        {
            int oldIndex = _currentShips.IndexOf(sorted[i]);
            if (oldIndex != i)
                _currentShips.Move(oldIndex, i);
        }
    }

    private void SelectAll_Checked(object sender, RoutedEventArgs e)
    {
        foreach (var ship in _currentShips)
            ship.IsSelected = true;
        var checkBox = sender as CheckBox;
        if (checkBox != FullHeader.FindName("SelectAllCheckBox") && FullHeader.Visibility == Visibility.Visible)
            SelectAllCheckBox.IsChecked = true;
        if (checkBox != CompactHeader.FindName("CompactSelectAllCheckBox") && CompactHeader.Visibility == Visibility.Visible)
            CompactSelectAllCheckBox.IsChecked = true;
        if (checkBox != MinimalHeader.FindName("MinimalSelectAllCheckBox") && MinimalHeader.Visibility == Visibility.Visible)
            MinimalSelectAllCheckBox.IsChecked = true;
    }

    private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        foreach (var ship in _currentShips)
            ship.IsSelected = false;
        var checkBox = sender as CheckBox;
        if (checkBox != FullHeader.FindName("SelectAllCheckBox") && FullHeader.Visibility == Visibility.Visible)
            SelectAllCheckBox.IsChecked = false;
        if (checkBox != CompactHeader.FindName("CompactSelectAllCheckBox") && CompactHeader.Visibility == Visibility.Visible)
            CompactSelectAllCheckBox.IsChecked = false;
        if (checkBox != MinimalHeader.FindName("MinimalSelectAllCheckBox") && MinimalHeader.Visibility == Visibility.Visible)
            MinimalSelectAllCheckBox.IsChecked = false;
    }

    private void ShipListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = ShipListView.SelectedItem as ShipViewModel;
        if (selected != null)
        {
            _lastSelectedShipId = selected.Id;
            ShipDetailControl.SetShip(selected);
        }
        else
        {
            ShipDetailControl.SetShip(null);
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            string input = sender.Text?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                sender.ItemsSource = null;
            }
            else
            {
                _currentSuggestions = _allSuggestions
                    .Where(item => item.DisplayText.Contains(input, StringComparison.OrdinalIgnoreCase))
                    .Take(30)
                    .ToList();
                sender.ItemsSource = _currentSuggestions;
            }
            RefreshShipList();
        }
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        RefreshShipList();
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SuggestionItem item)
        {
            sender.Text = item.SearchText;
            RefreshShipList();
        }
    }

    private int GetRaritySortValue(ShipViewModel ship) => (int)ship.RarityEnum;

    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshShipList();

    private void SortOrderToggle_Checked(object sender, RoutedEventArgs e)
    {
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        _isAscending = false;
        SortOrderToggle.Content = loader.GetString("SortDescending");
        RefreshShipList();
    }

    private void SortOrderToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        _isAscending = true;
        SortOrderToggle.Content = loader.GetString("SortAscending");
        RefreshShipList();
    }

    private async void BatchOperation_Click(object sender, RoutedEventArgs e)
    {
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        var selectedShips = _currentShips.Where(s => s.IsSelected).ToList();
        if (selectedShips.Count == 0)
        {
            var dialog = new ContentDialog
            {
                Title = loader.GetString("BatchOperation_Title"),
                Content = loader.GetString("NoShipSelected_Message"),
                CloseButtonText = loader.GetString("Common_Confirm"),
                XamlRoot = this.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
            return;
        }

        var menu = new MenuFlyout();

        // ========== 第一组：获得状态 ==========
        var markOwnedItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkOwned") ?? "标记为已获得",
            Icon = new FontIcon { Glyph = "\uE10B" }
        };
        markOwnedItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.Owned = true,
            ship => true,
            "标记为已获得",
            "已成功标记 {0} 艘舰船为已获得。");

        var clearAllItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkNotOwnedAndClear") ?? "标记为未获得并清除所有状态",
            Icon = new FontIcon { Glyph = "\uE10A" },
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed)
        };
        clearAllItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship =>
            {
                ship.Owned = false;
                ship.Breakthrough = 0;
                ship.Oath = false;
                ship.Level120 = false;
                ship.Retrofitted = false;
                ship.SpecialGearObtained = false;
                ship.AffectionMax = false;
                ship.Level125 = false;
            },
            ship => true,
            "标记为未获得并清除状态",
            "已成功标记 {0} 艘舰船为未获得并清除相关状态。");

        menu.Items.Add(markOwnedItem);
        menu.Items.Add(clearAllItem);
        menu.Items.Add(new MenuFlyoutSeparator());

        // ========== 第二组：突破状态（子菜单）==========
        var breakSubMenu = new MenuFlyoutSubItem
        {
            Text = loader.GetString("BatchOp_BreakthroughSub") ?? "突破状态",
            Icon = new FontIcon { Glyph = "\uE734" }
        };
        var breakFullItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkMaxBreak") ?? "标记为满破",
            Icon = new FontIcon { Glyph = "\uE734" }
        };
        breakFullItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.Breakthrough = 3,
            ship => ship.Owned && !IsBulinShip(ship),
            "标记为满破",
            "已成功标记 {0} 艘舰船为满破（跳过未获得或布里舰船 {1} 艘）。");

        var breakClearItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkNotMaxBreak") ?? "标记为未突破",
            Icon = new FontIcon { Glyph = "\uE735" }
        };
        breakClearItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.Breakthrough = 0,
            ship => ship.Owned && !IsBulinShip(ship),
            "标记为未突破",
            "已成功标记 {0} 艘舰船为未突破（跳过未获得或布里舰船 {1} 艘）。");

        breakSubMenu.Items.Add(breakFullItem);
        breakSubMenu.Items.Add(breakClearItem);
        menu.Items.Add(breakSubMenu);
        menu.Items.Add(new MenuFlyoutSeparator());

        // ========== 第三组：等级与誓约 ==========
        var level120Item = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkLevel120") ?? "标记为120级",
            Icon = new FontIcon { Glyph = "\uE752" }
        };
        level120Item.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.Level120 = true,
            ship => ship.Owned,
            "标记为120级",
            "已成功标记 {0} 艘舰船为120级（跳过未获得舰船 {1} 艘）。");

        var unlevel120Item = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkNotLevel120") ?? "取消120级",
            Icon = new FontIcon { Glyph = "\uE87F" }
        };
        unlevel120Item.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.Level120 = false,
            ship => ship.Owned,
            "取消120级",
            "已成功取消 {0} 艘舰船的120级标记（跳过未获得舰船 {1} 艘）。");

        var oathItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkOath") ?? "标记为誓约",
            Icon = new FontIcon { Glyph = "\uEB51" }
        };
        oathItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.Oath = true,
            ship => ship.Owned,
            "标记为誓约",
            "已成功标记 {0} 艘舰船为誓约（跳过未获得舰船 {1} 艘）。");

        var unoathItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkNotOath") ?? "取消誓约",
            Icon = new FontIcon { Glyph = "\uEB52" }
        };
        unoathItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.Oath = false,
            ship => ship.Owned,
            "取消誓约",
            "已成功取消 {0} 艘舰船的誓约标记（跳过未获得舰船 {1} 艘）。");

        menu.Items.Add(level120Item);
        menu.Items.Add(unlevel120Item);
        menu.Items.Add(oathItem);
        menu.Items.Add(unoathItem);
        menu.Items.Add(new MenuFlyoutSeparator());

        // ========== 第四组：改造与兵装 ==========
        var remodelItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkRemodeled") ?? "标记为改造",
            Icon = new FontIcon { Glyph = "\uE794" }
        };
        remodelItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.Retrofitted = true,
            ship => ship.Owned && ship.Retrofit.CanRetrofit,
            "标记为改造",
            "已成功标记 {0} 艘舰船为改造（跳过未获得或不支持改造的舰船 {1} 艘）。");

        var unremodelItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkNotRemodeled") ?? "取消改造",
            Icon = new FontIcon { Glyph = "\uEB78" }
        };
        unremodelItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.Retrofitted = false,
            ship => ship.Owned && ship.Retrofit.CanRetrofit,
            "取消改造",
            "已成功取消 {0} 艘舰船的改造标记（跳过未获得或不支持改造的舰船 {1} 艘）。");

        var specialGearItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkSpecialGear") ?? "获得专属兵装",
            Icon = new FontIcon { Glyph = "\uF157" }
        };
        specialGearItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.SpecialGearObtained = true,
            ship => ship.Owned && ship.CanSpecialGear,
            "获得专属兵装",
            "已成功为 {0} 艘舰船标记获得专属兵装（跳过未获得或无兵装的舰船 {1} 艘）。");

        var unspecialGearItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkNotSpecialGear") ?? "取消专属兵装",
            Icon = new FontIcon { Glyph = "\uF159" }
        };
        unspecialGearItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.SpecialGearObtained = false,
            ship => ship.Owned && ship.CanSpecialGear,
            "取消专属兵装",
            "已成功为 {0} 艘舰船取消专属兵装标记（跳过未获得或无兵装的舰船 {1} 艘）。");

        menu.Items.Add(remodelItem);
        menu.Items.Add(unremodelItem);
        menu.Items.Add(specialGearItem);
        menu.Items.Add(unspecialGearItem);

        menu.ShowAt(BatchOperationButton);
    }

    private async Task BatchOperationWithFilterAsync(
    List<ShipViewModel> selectedShips,
    Action<ShipViewModel> operation,
    Func<ShipViewModel, bool> condition,
    string operationName,
    string successMessageFormat)
    {
        var applicableShips = selectedShips.Where(condition).ToList();
        var skippedCount = selectedShips.Count - applicableShips.Count;

        // 如果没有符合条件的舰船，直接提示并返回
        if (applicableShips.Count == 0)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            var dialog = new ContentDialog
            {
                Title = "批量操作",
                Content = $"没有符合条件（{operationName}）的舰船。操作已取消。",
                CloseButtonText = loader.GetString("Common_Confirm") ?? "确定",
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
            return;
        }

        // 弹出确认对话框
        var confirmDialog = new ContentDialog
        {
            Title = "确认批量操作",
            Content = $"将对 {applicableShips.Count} 艘舰船执行“{operationName}”。{(skippedCount > 0 ? $"\n跳过 {skippedCount} 艘不符合条件的舰船。" : "")}\n是否继续？",
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        // 保存当前状态（选中和滚动位置）
        int? selectedId = (ShipListView.SelectedItem as ShipViewModel)?.Id;
        var scrollViewer = FindScrollViewer(ShipListView);
        double? verticalOffset = scrollViewer?.VerticalOffset;

        // 执行操作
        foreach (var ship in applicableShips)
        {
            operation(ship);
        }
        await _shipManager.SaveAsync();

        // 刷新列表
        RefreshShipList();

        // 恢复选中
        if (selectedId.HasValue)
        {
            var newSelected = _currentShips.FirstOrDefault(s => s.Id == selectedId.Value);
            if (newSelected != null)
            {
                ShipListView.SelectedItem = newSelected;
                ShipDetailControl.SetShip(newSelected);
            }
        }

        // 恢复滚动位置
        if (scrollViewer != null && verticalOffset.HasValue)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                scrollViewer.ChangeView(null, verticalOffset.Value, null);
            });
        }

        // 显示操作结果
        var resultDialog = new ContentDialog
        {
            Title = "批量操作完成",
            Content = string.Format(successMessageFormat, applicableShips.Count, skippedCount),
            CloseButtonText = "确定",
            XamlRoot = this.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        await resultDialog.ShowAsync();
    }

    private async void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        var filterPanel = new FilterPanel();
        if (_currentCategoryFilter.HasValue)
            filterPanel.SetCategory(_currentCategoryFilter.Value);
        if (_currentFilterCriteria != null)
            filterPanel.SetCriteria(_currentFilterCriteria);

        var dialog = new ContentDialog
        {
            Title = loader.GetString("FilterDialog_Title"),
            Content = filterPanel,
            PrimaryButtonText = loader.GetString("Common_Confirm"),
            CloseButtonText = loader.GetString("Common_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot,
            Width = 600,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        dialog.PrimaryButtonClick += (s, args) =>
        {
            _currentFilterCriteria = filterPanel.GetFilterCriteria();
            RefreshShipList();
        };

        await dialog.ShowAsync();
    }

    private IEnumerable<ShipViewModel> ApplyFilterCriteria(IEnumerable<ShipViewModel> source, FilterCriteria criteria)
    {
        if (criteria.ShipTypes.Any())
            source = source.Where(s => criteria.ShipTypes.Contains(s.ShipTypeEnum));
        if (criteria.Factions.Any())
            source = source.Where(s => criteria.Factions.Contains(s.FactionEnum));
        if (criteria.Rarities.Any())
            source = source.Where(s => criteria.Rarities.Contains(s.RarityEnum));
        if (criteria.CanRemodel)
            source = source.Where(s => s.Retrofit.CanRetrofit);
        if (criteria.Remodeled)
            source = source.Where(s => s.Retrofitted);
        if (criteria.MaxBreakthrough)
            source = source.Where(s => s.IsMaxBreakthrough);
        if (criteria.NotMaxBreakthrough)
            source = source.Where(s => s.Owned && !s.IsMaxBreakthrough);
        if (criteria.Level120)
            source = source.Where(s => s.Level120);
        if (criteria.NotLevel120)
            source = source.Where(s => s.Owned && !s.Level120);
        if (criteria.Oath)
            source = source.Where(s => s.Oath);
        if (criteria.NotOath)
            source = source.Where(s => !s.Oath);
        if (criteria.CanSpecialGear)
            source = source.Where(s => s.CanSpecialGear);
        if (criteria.SpecialGearObtained)
            source = source.Where(s => s.SpecialGearObtained);
        if (criteria.AttributeBonuses.Any())
        {
            source = source.Where(s => criteria.AttributeBonuses.Contains(s.ObtainBonusAttrEnum) ||
                                       criteria.AttributeBonuses.Contains(s.Level120BonusAttrEnum));
        }
        return source;
    }

    private void ResetAndRefresh_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        SortCombo.SelectedIndex = 0;
        _currentFilterCriteria = null;
        RefreshShipList();
    }

    private async void AddShipButton_Click(object sender, RoutedEventArgs e)
    {
        var app = Application.Current as App;
        if (app?.AccountManager?.IsDeveloper() != true)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
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

        var addDialog = new AddShipDialog(); // 无参数，新建空船
        addDialog.XamlRoot = this.XamlRoot;
        addDialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        if (await addDialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var newShip = addDialog.GetShip();
            if (newShip != null)
            {
                await _shipManager.AddShip(newShip);
                LogService.Operation("添加舰船", $"用户通过界面新增舰船: {newShip.Name.GetLocalized()}", app.AccountManager.CurrentAccount);
                RefreshShipList();
            }
        }
    }

    private ShipViewModel _contextShip;
    private void ShipListView_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var obj = (e.OriginalSource as FrameworkElement)?.DataContext;
        if (obj is not ShipViewModel ship) return;
        _contextShip = ship;
        ShipListView.SelectedItem = ship;

        var menu = (MenuFlyout)Resources["ShipContextMenu"];
        if (menu == null) return;

        var items = menu.Items;

        bool isBulin = ship.DisplayName.Contains("布里") || ship.RawName.Contains("布里");
        bool isOwned = ship.Owned;

        // 更新各菜单项
        // 0. 获得/未获得
        if (items[0] is MenuFlyoutItem ownItem)
        {
            var icon = ownItem.Icon as FontIcon;
            ownItem.Text = _contextShip.Owned ? "标记为未获得" : "标记为已获得";
            if (icon != null) icon.Glyph = _contextShip.Owned ? "\uE10A" : "\uE10B";
            ownItem.IsEnabled = true;
        }
        // 1. 120级
        if (items[1] is MenuFlyoutItem lv120Item)
        {
            var icon = lv120Item.Icon as FontIcon;
            lv120Item.Text = _contextShip.Level120 ? "取消120级" : "标记120级";
            if (icon != null) icon.Glyph = _contextShip.Level120 ? "\uE87F" : "\uE752";
            lv120Item.IsEnabled = isOwned;
        }
        // 2. 满破/未满破
        if (items[2] is MenuFlyoutItem breakItem)
        {
            var icon = breakItem.Icon as FontIcon;
            bool isFull = _contextShip.Breakthrough >= 3;
            breakItem.Text = isFull ? "取消满破" : "标记满破";
            if (icon != null) icon.Glyph = isFull ? "\uE735" : "\uE734";
            breakItem.IsEnabled = isOwned && !isBulin;
        }
        // 3. 誓约
        if (items[3] is MenuFlyoutItem oathItem)
        {
            var icon = oathItem.Icon as FontIcon;
            oathItem.Text = _contextShip.Oath ? "取消誓约" : "标记誓约";
            if (icon != null) icon.Glyph = _contextShip.Oath ? "\uEB52" : "\uEB51";
            oathItem.IsEnabled = isOwned;
        }

        // 分隔符索引4，跳过

        // 5. 突破子菜单（索引5）
        if (items[5] is MenuFlyoutSubItem breakSub)
        {
            bool enableSub = isOwned && !isBulin;
            breakSub.IsEnabled = enableSub;
            if (enableSub)
            {
                int currentBreak = ship.Breakthrough;
                for (int i = 0; i < breakSub.Items.Count; i++)
                {
                    if (breakSub.Items[i] is MenuFlyoutItem subItem && subItem.Tag is string tag && int.TryParse(tag, out int level))
                    {
                        if (level == currentBreak)
                            subItem.Icon = new FontIcon { Glyph = "\uE73E" };
                        else
                            subItem.Icon = null;
                    }
                }
            }
        }

        // 分隔符索引6，跳过

        // 7. 删除（索引7）
        if (items[7] is MenuFlyoutItem deleteItem)
        {
            var app = (App)Application.Current;
            deleteItem.Visibility = app.AccountManager.IsDeveloper() ? Visibility.Visible : Visibility.Collapsed;
            deleteItem.IsEnabled = true;
        }
    }

    // 右键菜单事件
    private async void CtxMenu_ToggleOwned_Click(object sender, RoutedEventArgs e)
    {
        bool willBeNotOwned = _contextShip.Owned;
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        await UpdateShipStateAndRefreshAsync(ship =>
        {
            if (ship.Owned)
            {
                ship.Owned = false;
                ship.Breakthrough = 0;
                ship.Level120 = false;
                ship.Oath = false;
                ship.Retrofitted = false;
                ship.SpecialGearObtained = false;
            }
            else
            {
                ship.Owned = true;
            }
        }, requireConfirm: willBeNotOwned,
          confirmTitle: loader.GetString("ConfirmClear_Title"),
          confirmContent: loader.GetString("ConfirmClearOwned_Message"));
        LogService.Operation("右键菜单", $"切换获得状态: {_contextShip.DisplayName} 变为 {_contextShip.Owned}", (Application.Current as App)?.AccountManager?.CurrentAccount);
    }

    private async void CtxMenu_ToggleLevel120_Click(object sender, RoutedEventArgs e)
    {
        await UpdateShipStateAndRefreshAsync(ship => ship.Level120 = !ship.Level120);
    }

    private async void CtxMenu_ToggleMaxBreak_Click(object sender, RoutedEventArgs e)
    {
        await UpdateShipStateAndRefreshAsync(ship =>
        {
            ship.Breakthrough = (ship.Breakthrough >= 3) ? 0 : 3;
        });
    }

    private async void CtxMenu_ToggleOath_Click(object sender, RoutedEventArgs e)
    {
        await UpdateShipStateAndRefreshAsync(ship => ship.Oath = !ship.Oath);
    }

    private async void CtxMenu_Break_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string tag && int.TryParse(tag, out int breakLevel))
        {
            await UpdateShipStateAndRefreshAsync(ship => ship.Breakthrough = breakLevel);
        }
    }

    private async void CtxMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        var dialog = new ContentDialog
        {
            Title = loader.GetString("ConfirmDelete_Title") ?? "确认删除",
            Content = string.Format(loader.GetString("ConfirmDeleteShip_Message") ?? "确定要删除舰船 {0} 吗？", _contextShip.DisplayName),
            PrimaryButtonText = loader.GetString("Common_Delete") ?? "删除",
            CloseButtonText = loader.GetString("Common_Cancel") ?? "取消",
            XamlRoot = this.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            int deletedId = _contextShip.Id;
            await _shipManager.DeleteShip(deletedId);
            RefreshShipList();
            ShipDetailControl.SetShip(null);
        }
    }

    // 通用方法
    private async Task UpdateShipStateAndRefreshAsync(Action<ShipViewModel> updateAction, bool requireConfirm = false, string confirmTitle = null, string confirmContent = null)
    {
        if (_contextShip == null) return;

        if (requireConfirm)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            var dialog = new ContentDialog
            {
                Title = confirmTitle ?? loader.GetString("ConfirmClear_Title") ?? "确认",
                Content = confirmContent ?? loader.GetString("ConfirmClearOwned_Message") ?? "是否继续？",
                PrimaryButtonText = loader.GetString("Common_Confirm") ?? "确定",
                CloseButtonText = loader.GetString("Common_Cancel") ?? "取消",
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;
        }

        int? selectedId = (ShipListView.SelectedItem as ShipViewModel)?.Id;
        var scrollViewer = FindScrollViewer(ShipListView);
        double? verticalOffset = scrollViewer?.VerticalOffset;

        updateAction(_contextShip);
        await _shipManager.SaveAsync();

        RefreshShipList();

        if (selectedId.HasValue)
        {
            var newSelected = _currentShips.FirstOrDefault(s => s.Id == selectedId.Value);
            if (newSelected != null)
            {
                ShipListView.SelectedItem = newSelected;
                ShipDetailControl.SetShip(newSelected);
            }
        }

        if (scrollViewer != null && verticalOffset.HasValue)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                scrollViewer.ChangeView(null, verticalOffset.Value, null);
            });
        }
    }

    private bool IsBulinShip(ShipViewModel ship)
    {
        return ship.DisplayName.Contains("布里") || ship.RawName.Contains("布里");
    }

    // 以下方法与改造/兵装相关，但菜单已移除，保留空方法或删除均可，这里保留以防止事件处理器缺失。
    // 注意：这两个方法在菜单中已没有引用，可以安全删除，但为了代码整洁，保留空实现。
    private void CtxMenu_ToggleRemodel_Click(object sender, RoutedEventArgs e) { }
    private void CtxMenu_ToggleSpecialGear_Click(object sender, RoutedEventArgs e) { }
}