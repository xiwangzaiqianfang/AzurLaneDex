using AzurLaneDex.Services;
using AzurLaneDex.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using static AzurLaneDex.Models.ShipStatic;

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
        if (windowWidth >= 1200)
        {
            template = _fullTemplate;
            showFull = true; showCompact = false; showMinimal = false;
        }
        else if (windowWidth >= 850)
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

        // 辅助函数：检查是否为有效文本（不含路径分隔符）
        bool IsValidText(string text) => !string.IsNullOrEmpty(text) && !text.Contains('\\') && !text.Contains('/');

        foreach (var ship in _shipManager.Ships)
        {
            if (IsValidText(ship.Name))
                _allSuggestions.Add(new SuggestionItem { DisplayText = ship.Name, SearchText = ship.Name });
            else
                System.Diagnostics.Debug.WriteLine($"无效的舰船名称: {ship.Name}");
        }
        foreach (var ship in _shipManager.Ships)
        {
            if (IsValidText(ship.AltName))
                _allSuggestions.Add(new SuggestionItem { DisplayText = $"[和谐名称] {ship.AltName}", SearchText = ship.AltName });
        }
        foreach (var ship in _shipManager.Ships)
        {
            if (IsValidText(ship.SpecialGearName))
                _allSuggestions.Add(new SuggestionItem { DisplayText = $"[专属兵装] {ship.SpecialGearName}", SearchText = ship.SpecialGearName });
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
                s.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (s.AltName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.SpecialGearName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
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
                2 => s => s.Name,
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
                2 => s => s.Name,
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
        // 同步其他表头的全选复选框状态
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
        var map = new Dictionary<string, int>
        {
            ["普通"] = 0,
            ["稀有"] = 1,
            ["精锐"] = 2,
            ["超稀有"] = 3,
            ["海上传奇"] = 4,
            ["最高方案"] = 5,
            ["决战方案"] = 6
        };
        int baseValue = map.GetValueOrDefault(ship.Rarity, 99);
        if (ship.Remodeled && ship.CanRemodel && baseValue < map.Count - 1)
            return baseValue + 1;
        return baseValue;
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
        var operations = new (string text, Action<ShipViewModel> action)[]
        {
            (loader.GetString("BatchOp_MarkOwned"), s => s.Owned = true),
            (loader.GetString("BatchOp_MarkNotOwnedAndClear"), s => { s.Owned = false; s.Breakthrough = 0; s.Oath = false; s.Level120 = false; s.Remodeled = false; s.SpecialGearObtained = false; }),
            (loader.GetString("BatchOp_MarkMaxBreak"), s => s.Breakthrough = 3),
            (loader.GetString("BatchOp_MarkNotMaxBreak"), s => s.Breakthrough = 0),
            (loader.GetString("BatchOp_MarkLevel120"), s => s.Level120 = true),
            (loader.GetString("BatchOp_MarkNotLevel120"), s => s.Level120 = false),
            (loader.GetString("BatchOp_MarkOath"), s => s.Oath = true),
            (loader.GetString("BatchOp_MarkNotOath"), s => s.Oath = false),
            (loader.GetString("BatchOp_MarkRemodeled"), s => s.Remodeled = true),
            (loader.GetString("BatchOp_MarkNotRemodeled"), s => s.Remodeled = false),
            (loader.GetString("BatchOp_MarkSpecialGear"), s => s.SpecialGearObtained = true),
            (loader.GetString("BatchOp_MarkNotSpecialGear"), s => s.SpecialGearObtained = false),
        };
        foreach (var op in operations)
        {
            var item = new MenuFlyoutItem { Text = op.text };
            item.Click += async (s, args) =>
            {
                var dialog = new ContentDialog
                {
                    Title = loader.GetString("ConfirmBatchOp_Title"),
                    Content = string.Format(loader.GetString("ConfirmBatchOp_Message"), selectedShips.Count, op.text),
                    PrimaryButtonText = loader.GetString("Common_Confirm"),
                    CloseButtonText = loader.GetString("Common_Cancel"),
                    XamlRoot = this.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };
                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                    return;
                foreach (var ship in selectedShips)
                    op.action(ship);
                _shipManager.Save();
                RefreshShipList();
                ShipDetailControl.SetShip(ShipListView.SelectedItem as ShipViewModel);
            };
            menu.Items.Add(item);
        }
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
        if (obj is ShipViewModel ship)
        {
            _contextShip = ship;
            ShipListView.SelectedItem = ship;
        }
        else
        {
            _contextShip = null;
        }
        var app = Application.Current as App;
        bool isDeveloper = app?.AccountManager?.IsDeveloper() ?? false;
        var menuDelete = this.FindName("ContextMenuDelete") as UIElement;
        if (menuDelete != null)
            menuDelete.Visibility = isDeveloper ? Visibility.Visible : Visibility.Collapsed;
        var separatorDelete = this.FindName("SeparatorDelete") as UIElement;
        if (separatorDelete != null)
            separatorDelete.Visibility = isDeveloper ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ContextMenu_Owned_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.Owned = true; _shipManager.Save(); } }
    private void ContextMenu_NotOwned_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.Owned = false; _contextShip.Breakthrough = 0; _contextShip.Oath = false; _contextShip.Level120 = false; _contextShip.Remodeled = false; _contextShip.SpecialGearObtained = false; _shipManager.Save(); } }
    private void ContextMenu_MaxBreak_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.Breakthrough = 3; _shipManager.Save(); } }
    private void ContextMenu_NotMaxBreak_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.Breakthrough = 0; _shipManager.Save(); } }
    private void ContextMenu_Level120_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.Level120 = true; _shipManager.Save(); } }
    private void ContextMenu_NotLevel120_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.Level120 = false; _shipManager.Save(); } }
    private void ContextMenu_Oath_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.Oath = true; _shipManager.Save(); } }
    private void ContextMenu_NotOath_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.Oath = false; _shipManager.Save(); } }
    private void ContextMenu_Remodeled_Click(object sender, RoutedEventArgs e) { if (_contextShip != null && _contextShip.CanRemodel) { _contextShip.Remodeled = true; _shipManager.Save(); } }
    private void ContextMenu_NotRemodeled_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.Remodeled = false; _shipManager.Save(); } }
    private void ContextMenu_SpecialGear_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.SpecialGearObtained = true; _shipManager.Save(); } }
    private void ContextMenu_NotSpecialGear_Click(object sender, RoutedEventArgs e) { if (_contextShip != null) { _contextShip.SpecialGearObtained = false; _shipManager.Save(); } }
    private async void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
        if (_contextShip == null) return;
        var dialog = new ContentDialog
        {
            Title = loader.GetString("ConfirmDelete_Title"),
            Content = string.Format(loader.GetString("ConfirmDeleteShip_Message"), _contextShip.Name),
            PrimaryButtonText = loader.GetString("Common_Delete"),
            CloseButtonText = loader.GetString("Common_Cancel"),
            XamlRoot = this.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            _shipManager.DeleteShip(_contextShip.Id);
            RefreshShipList();
        }
    }
}