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

        // 获取模板引用
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

        // 周年庆信息栏相关模块
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
            // 后备：监听 Page 的父级窗口
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
        // 当 Page 大小变化时也更新（备用）
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
            // 保存当前选中和滚动位置
            int? selectedId = (ShipListView.SelectedItem as ShipViewModel)?.Id;
            var scrollViewer = FindScrollViewer(ShipListView);
            double? verticalOffset = scrollViewer?.VerticalOffset;

            ShipListView.ItemTemplate = template;

            // 切换表头可见性
            FullHeader.Visibility = showFull ? Visibility.Visible : Visibility.Collapsed;
            CompactHeader.Visibility = showCompact ? Visibility.Visible : Visibility.Collapsed;
            MinimalHeader.Visibility = showMinimal ? Visibility.Visible : Visibility.Collapsed;
            // 恢复选中和滚动
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
            else
                System.Diagnostics.Debug.WriteLine($"无效的舰船名称: {rawName}");
        }
        foreach (var ship in _shipManager.Ships)
        {
            string rawAlt = ship.RawAltName;
            if (IsValidText(rawAlt))
                _allSuggestions.Add(new SuggestionItem { DisplayText = $"[和谐名称] {rawAlt}", SearchText = rawAlt });
        }
        foreach (var ship in _shipManager.Ships)
        {
            string gear = ship.SpecialGearName.GetValueOrDefault("zh-Hans");
            if (IsValidText(gear))
                _allSuggestions.Add(new SuggestionItem { DisplayText = $"[专属兵装] {gear}", SearchText = gear });
        }
        var eventNames = _shipManager.Ships
            .Where(s => IsValidText(s.DebutEvent))
            .Select(s => s.DebutEvent)
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
                (s.RawAltName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.SpecialGearName.GetValueOrDefault("zh-Hans")?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.DebutEvent?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.AcquireMain?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.AcquireDetail?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
            );
        }
        if (_currentFilterCriteria != null)
        {
            source = ApplyFilterCriteria(source, _currentFilterCriteria);
        }
        if (_currentCategoryFilter.HasValue)
        {
            source = source.Where(s => s.Category == _currentCategoryFilter.Value);
        }

        int sortIndex = SortCombo.SelectedIndex;
        IEnumerable<ShipViewModel> sorted;
        if (sortIndex == 4)
        {
            sorted = _isAscending
                ? source.OrderBy(s => !s.CanRemodel).ThenBy(s => s.RemodelDate)
                : source.OrderBy(s => !s.CanRemodel).ThenByDescending(s => s.RemodelDate);
        }
        else if (sortIndex == 5)
        {
            sorted = _isAscending
                ? source.OrderBy(s => !s.CanSpecialGear).ThenBy(s => s.SpecialGearDate)
                : source.OrderBy(s => !s.CanSpecialGear).ThenByDescending(s => s.SpecialGearDate);
        }
        else if (sortIndex == 7)
        {
            if (_isAscending)
                sorted = source.OrderBy(s => !s.Remodeled);
            else
                sorted = source.OrderByDescending(s => !s.Remodeled);
        }
        else if (sortIndex == 11)
        {
            if (_isAscending)
                sorted = source.OrderBy(s => !s.SpecialGearObtained);
            else
                sorted = source.OrderByDescending(s => !s.SpecialGearObtained);
        }
        else
        {
            Func<ShipViewModel, IComparable> keySelector = sortIndex switch
            {
                0 => s => s.Id,
                1 => s => s.CategoryOrder,
                2 => s => s.RawName,   // 使用原始中文名称排序
                3 => s => GetRaritySortValue(s),
                4 => s => s.CanRemodel ? (s.RemodelDate ?? "9999-12-31") : "9999-12-31",
                5 => s => s.CanSpecialGear ? (s.SpecialGearDate ?? "9999-12-31") : "9999-12-31",
                6 => s => s.Owned,
                7 => s => s.Remodeled,
                8 => s => s.Breakthrough,
                9 => s => s.Oath,
                10 => s => s.Level120,
                11 => s => s.SpecialGearObtained,
                _ => s => s.Id
            };

            if (_isAscending)
                sorted = source.OrderBy(keySelector);
            else
                sorted = source.OrderByDescending(keySelector);
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
        if (sortIndex == 4)
        {
            if (_isAscending)
                sorted = _currentShips.OrderBy(s => !s.CanRemodel).ThenBy(s => s.RemodelDate ?? "9999-12-31").ToList();
            else
                sorted = _currentShips.OrderBy(s => !s.CanRemodel).ThenByDescending(s => s.RemodelDate ?? "0000-01-01").ToList();
        }
        else if (sortIndex == 5)
        {
            if (_isAscending)
                sorted = _currentShips.OrderBy(s => !s.CanSpecialGear).ThenBy(s => s.SpecialGearDate ?? "9999-12-31").ToList();
            else
                sorted = _currentShips.OrderBy(s => !s.CanSpecialGear).ThenByDescending(s => s.SpecialGearDate ?? "0000-01-01").ToList();
        }
        else if (sortIndex == 7)
        {
            if (_isAscending)
                sorted = _currentShips.OrderBy(s => !s.Remodeled).ToList();
            else
                sorted = _currentShips.OrderByDescending(s => !s.Remodeled).ToList();
        }
        else if (sortIndex == 11)
        {
            if (_isAscending)
                sorted = _currentShips.OrderBy(s => !s.SpecialGearObtained).ToList();
            else
                sorted = _currentShips.OrderByDescending(s => !s.SpecialGearObtained).ToList();
        }
        else
        {
            Func<ShipViewModel, IComparable> keySelector = sortIndex switch
            {
                0 => s => s.Id,
                1 => s => s.CategoryOrder,
                2 => s => s.RawName,
                3 => s => GetRaritySortValue(s),
                4 => s => s.CanRemodel ? (s.RemodelDate ?? "9999-12-31") : "9999-12-31",
                5 => s => s.CanSpecialGear ? (s.SpecialGearDate ?? "9999-12-31") : "9999-12-31",
                6 => s => s.Owned,
                7 => s => s.Remodeled,
                8 => s => s.Breakthrough,
                9 => s => s.Oath,
                10 => s => s.Level120,
                11 => s => s.SpecialGearObtained,
                _ => s => s.Id
            };
            sorted = _isAscending ? _currentShips.OrderBy(keySelector).ToList() : _currentShips.OrderByDescending(keySelector).ToList();
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

    private int GetRaritySortValue(ShipViewModel ship)
    {
        // 使用 RarityId 而不是本地化字符串
        return ship.RarityId;
    }

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
            ship => true, // 无条件，对所有舰船执行
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
                ship.Remodeled = false;
                ship.SpecialGearObtained = false;
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
            ship => ship.Remodeled = true,
            ship => ship.Owned && ship.CanRemodel,
            "标记为改造",
            "已成功标记 {0} 艘舰船为改造（跳过未获得或不支持改造的舰船 {1} 艘）。");

        var unremodelItem = new MenuFlyoutItem
        {
            Text = loader.GetString("BatchOp_MarkNotRemodeled") ?? "取消改造",
            Icon = new FontIcon { Glyph = "\uEB78" }
        };
        unremodelItem.Click += async (s, args) => await BatchOperationWithFilterAsync(
            selectedShips,
            ship => ship.Remodeled = false,
            ship => ship.Owned && ship.CanRemodel,
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
        if (criteria.ShipClasses.Any())
            source = source.Where(s => criteria.ShipClasses.Contains(s.ShipClass));
        if (criteria.Factions.Any())
            source = source.Where(s => criteria.Factions.Contains(s.Faction));
        if (criteria.Rarities.Any())
            source = source.Where(s => criteria.Rarities.Contains(s.Rarity));
        if (criteria.CanRemodel)
            source = source.Where(s => s.CanRemodel);
        if (criteria.Remodeled)
            source = source.Where(s => s.Remodeled);
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
            source = source.Where(s => criteria.AttributeBonuses.Contains(s.ObtainBonusAttr) || criteria.AttributeBonuses.Contains(s.Level120BonusAttr));
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
        var dialog = new AddShipDialog();
        dialog.XamlRoot = this.XamlRoot;
        dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var newShip = dialog.GetShip();
            if (newShip != null)
            {
                _shipManager.AddShip(newShip);
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

        // 获取统一菜单资源
        var menu = (MenuFlyout)Resources["ShipContextMenu"];
        if (menu == null) return;

        var items = menu.Items;

        // 判断是否为布里系列
        bool isBulin = ship.DisplayName.Contains("布里") || ship.RawName.Contains("布里");
        bool isOwned = ship.Owned;

        // ----- 第一组（索引0~3）-----
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
        // 3. 改造（仅可改造舰船显示）
        if (items[3] is MenuFlyoutItem remodelItem)
        {
            if (isOwned && ship.CanRemodel)
            {
                remodelItem.Visibility = Visibility.Visible;
                var icon = remodelItem.Icon as FontIcon;
                remodelItem.Text = ship.Remodeled ? "取消改造" : "标记改造";
                if (icon != null) icon.Glyph = ship.Remodeled ? "\uEB78" : "\uE794";
                remodelItem.IsEnabled = true;
            }
            else
            {
                remodelItem.Visibility = Visibility.Visible;  // 依然显示但禁用
                remodelItem.Text = "不可改造";
                remodelItem.Icon = new FontIcon { Glyph = "\uE794" };
                remodelItem.IsEnabled = false;
            }
        }

        // 分隔符在索引4，跳过

        // ----- 第二组（索引5~7）-----
        // 5. 誓约
        if (items[5] is MenuFlyoutItem oathItem)
        {
            var icon = oathItem.Icon as FontIcon;
            oathItem.Text = _contextShip.Oath ? "取消誓约" : "标记誓约";
           if (icon != null) icon.Glyph = _contextShip.Oath ? "\uEB52" : "\uEB51";
            oathItem.IsEnabled = isOwned;
        }
        // 6. 专属兵装
        if (items[6] is MenuFlyoutItem gearItem)
        {
            if (isOwned && ship.CanSpecialGear)
            {
                gearItem.Visibility = Visibility.Visible;
                var icon = gearItem.Icon as FontIcon;
                gearItem.Text = ship.SpecialGearObtained ? "取消兵装" : "获得兵装";
                if (icon != null) icon.Glyph = ship.SpecialGearObtained ? "\uF159" : "\uF157";
                gearItem.IsEnabled = true;
            }
            else
            {
                gearItem.Visibility = Visibility.Visible;
                gearItem.Text = "无专属兵装";
                gearItem.Icon = new FontIcon { Glyph = "\uF157" };
                gearItem.IsEnabled = false;
            }
        }
        // 7. 突破子菜单（更新子项选中状态）
        if (items[7] is MenuFlyoutSubItem breakSub)
        {
            bool enableSub = isOwned && !isBulin;
            breakSub.IsEnabled = enableSub;
            if (enableSub)
            {
                // 更新子菜单项选中图标（仅在启用时）
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

        // 分隔符在索引8，跳过

        // ----- 第三组：删除（索引9）-----
        if (items[9] is MenuFlyoutItem deleteItem)
        {
            var app = (App)Application.Current;
            deleteItem.Visibility = app.AccountManager.IsDeveloper() ? Visibility.Visible : Visibility.Collapsed;
            deleteItem.IsEnabled = true;
        }
    }
    // 获得/未获得（带确认对话框）
    private async void CtxMenu_ToggleOwned_Click(object sender, RoutedEventArgs e)
    {
        bool willBeNotOwned = _contextShip.Owned;
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        await UpdateShipStateAndRefreshAsync(ship =>
        {
            if (ship.Owned)
            {
                // 已获得 -> 未获得，清除状态
                ship.Owned = false;
                ship.Breakthrough = 0;
                ship.Level120 = false;
                ship.Oath = false;
                ship.Remodeled = false;
                ship.SpecialGearObtained = false;
            }
            else
            {
                ship.Owned = true;
            }
        }, requireConfirm: willBeNotOwned,   // 仅当变为未获得时需要确认
          confirmTitle: loader.GetString("ConfirmClear_Title"),
          confirmContent: loader.GetString("ConfirmClearOwned_Message"));
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

    private async void CtxMenu_ToggleRemodel_Click(object sender, RoutedEventArgs e)
    {
        if (!_contextShip.CanRemodel) return;
        await UpdateShipStateAndRefreshAsync(ship => ship.Remodeled = !ship.Remodeled);
    }

    private async void CtxMenu_ToggleOath_Click(object sender, RoutedEventArgs e)
    {
        await UpdateShipStateAndRefreshAsync(ship => ship.Oath = !ship.Oath);
    }

    private async void CtxMenu_ToggleSpecialGear_Click(object sender, RoutedEventArgs e)
    {
        if (!_contextShip.CanSpecialGear) return;
        await UpdateShipStateAndRefreshAsync(ship => ship.SpecialGearObtained = !ship.SpecialGearObtained);
    }

    // 突破子菜单项点击（突破一次、两次、三次）
    private async void CtxMenu_Break_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string tag && int.TryParse(tag, out int breakLevel))
        {
            await UpdateShipStateAndRefreshAsync(ship => ship.Breakthrough = breakLevel);
        }
    }

    // 删除舰船（仅开发者）
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
            _shipManager.DeleteShip(deletedId);
            // 删除后直接刷新，无需恢复选中（因为舰船已不存在）
            RefreshShipList();
            ShipDetailControl.SetShip(null);
        }
    }
    /// 执行舰船状态修改，保存数据，刷新列表并恢复选中与滚动位置
    private async Task UpdateShipStateAndRefreshAsync(Action<ShipViewModel> updateAction, bool requireConfirm = false, string confirmTitle = null, string confirmContent = null)
    {
        if (_contextShip == null) return;

        // 如果需要确认对话框
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

        // 记录当前选中的舰船ID和滚动位置
        int? selectedId = (ShipListView.SelectedItem as ShipViewModel)?.Id;
        var scrollViewer = FindScrollViewer(ShipListView);
        double? verticalOffset = scrollViewer?.VerticalOffset;

        // 执行状态修改
        updateAction(_contextShip);

        // 保存数据
        _shipManager.Save();

        // 刷新列表（保留选中和滚动）
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

        // 恢复滚动位置（需要等待UI更新）
        if (scrollViewer != null && verticalOffset.HasValue)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                scrollViewer.ChangeView(null, verticalOffset.Value, null);
            });
        }
    }

    /// <summary>
    /// 执行批量操作，保留选中和滚动位置
    /// </summary>
    private async Task ExecuteBatchOperationAsync(List<ShipViewModel> ships, Action<ShipViewModel> operation, string title, string content)
    {
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = loader.GetString("Common_Confirm"),
            CloseButtonText = loader.GetString("Common_Cancel"),
            XamlRoot = this.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        await RefreshAndRestoreSelectionAsync(() =>
        {
            foreach (var ship in ships)
                operation(ship);
            _shipManager.Save();
        });
    }
    private async Task RefreshAndRestoreSelectionAsync(Action updateAction = null)
    {
        // 记录当前选中的舰船ID和滚动位置
        int? selectedId = (ShipListView.SelectedItem as ShipViewModel)?.Id;
        var scrollViewer = FindScrollViewer(ShipListView);
        double? verticalOffset = scrollViewer?.VerticalOffset;

        // 执行更新操作（如果有）
        updateAction?.Invoke();

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
        _shipManager.Save();

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
    private bool IsBulinShip(ShipViewModel ship)
    {
        return ship.DisplayName.Contains("布里") || ship.RawName.Contains("布里");
    }
}