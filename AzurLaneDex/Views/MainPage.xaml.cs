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

namespace AzurLaneDex.Views;

public class SuggestionItem
{
    public string DisplayText { get; set; }  // 显示在建议列表中的文本（带前缀）
    public string SearchText { get; set; }   // 实际用于搜索的关键词（无前缀）
}

public sealed partial class MainPage : Page
{
    private ShipManager _shipManager;
    private ObservableCollection<ShipViewModel> _currentShips = new();
    private FilterCriteria _currentFilterCriteria;
    private int _lastSelectedShipId = -1;
    private double _lastScrollOffset = 0;
    private bool _isRefreshing = false;
    private List<SuggestionItem> _allSuggestions = new();   // 全量建议项（只初始化一次）
    private List<SuggestionItem> _currentSuggestions = new(); // 当前过滤后的建议项

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += MainPage_Loaded;
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
            // 可以显示一个错误对话框并退出
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
            // 其他初始化
        }
        System.Diagnostics.Debug.WriteLine($"Ships count: {_shipManager.Ships.Count}");
        BuildSuggestionSource();
        RefreshShipList();
    }

    private void BuildSuggestionSource()
    {
        _allSuggestions.Clear();

        // 1. 舰船名（无前缀）
        foreach (var ship in _shipManager.Ships)
        {
            _allSuggestions.Add(new SuggestionItem
            {
                DisplayText = ship.Name,
                SearchText = ship.Name
            });
        }

        // 2. 和谐名（带前缀 [和谐名称]）
        foreach (var ship in _shipManager.Ships)
        {
            if (!string.IsNullOrEmpty(ship.AltName))
            {
                _allSuggestions.Add(new SuggestionItem
                {
                    DisplayText = $"[和谐名称] {ship.AltName}",
                    SearchText = ship.AltName
                });
            }
        }

        // 3. 特殊兵装名（带前缀 [兵装]）
        foreach (var ship in _shipManager.Ships)
        {
            if (!string.IsNullOrEmpty(ship.SpecialGearName))
            {
                _allSuggestions.Add(new SuggestionItem
                {
                    DisplayText = $"[专属兵装] {ship.SpecialGearName}",
                    SearchText = ship.SpecialGearName
                });
            }
        }

        // 4. 登场活动名（带前缀 [活动]）
        var eventNames = _shipManager.Ships
            .Where(s => !string.IsNullOrEmpty(s.DebutEvent))
            .Select(s => s.DebutEvent)
            .Distinct();
        foreach (var evt in eventNames)
        {
            _allSuggestions.Add(new SuggestionItem
            {
                DisplayText = $"[活动] {evt}",
                SearchText = evt
            });
        }

        // 5. 获取方式关键词
        var acquireKeywords = new[] { "仅限打捞", "轻型池建造", "重型池建造", "特型池建造", "勋章支援", "舰队商店", "军需商店" };
        foreach (var kw in acquireKeywords)
        {
            _allSuggestions.Add(new SuggestionItem
            {
                DisplayText = $"[获取方式] {kw}",
                SearchText = kw
            });
        }
    }

    private void OnDataChanged()
    {
        // 保存当前选中的舰船 ID 和滚动位置（可选）
        int? selectedId = (ShipListView.SelectedItem as ShipViewModel)?.Id;
        var scrollViewer = FindScrollViewer(ShipListView);
        double? verticalOffset = scrollViewer?.VerticalOffset;

        RefreshShipList();  // 重新加载数据

        // 恢复选中项
        if (selectedId.HasValue)
        {
            var newSelected = _currentShips.FirstOrDefault(s => s.Id == selectedId.Value);
            if (newSelected != null)
            {
                ShipListView.SelectedItem = newSelected;
                // 恢复滚动位置（需要异步等待布局完成）
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

    private void RefreshShipList()
    {
        if (_shipManager == null) return;
        var source = _shipManager.Ships;
        if (source == null) return;
            
        // 1. 筛选（搜索框 + 筛选面板条件）
        var filtered = source.AsEnumerable();
        string keyword = SearchBox.Text?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            filtered = filtered.Where(s =>
                    s.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    (s.AltName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.SpecialGearName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.DebutEvent?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.AcquireMain?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.AcquireDetail?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                );
        }
        // 应用筛选面板条件（如果有）
        if (_currentFilterCriteria != null)
        {
            filtered = ApplyFilterCriteria(filtered, _currentFilterCriteria);
        }
        // 2. 排序
        int sortIndex = SortCombo.SelectedIndex;
        Func<ShipViewModel, IComparable> keySelector = sortIndex switch
        {
            0 => s => s.Id,
            1 => s => s.GameOrder,
            2 => s => s.Name,
            3 => s => GetRaritySortValue(s),
            4 => s => s.Owned,
            5 => s => s.Oath,
            6 => s => s.Breakthrough,
            7 => s => s.Level120,
            8 => s => s.Remodeled,
            9 => s => s.SpecialGearObtained,
            _ => s => s.Id
        };
        if (_isAscending)
            filtered = filtered.OrderBy(keySelector);
        else
            filtered = filtered.OrderByDescending(keySelector);

        _currentShips.Clear();
        foreach (var ship in filtered)
            _currentShips.Add(ship);

        // 刷新后清除全选状态，并重置所有舰船的 IsSelected 为 false
        SelectAllCheckBox.IsChecked = false;
        foreach (var ship in _currentShips)
            ship.IsSelected = false;
    }

    // 原地排序（不改变 ItemsSource 实例）
    private void ApplyCurrentSort()
    {
        if (_currentShips == null || _currentShips.Count == 0) return;
        int sortIndex = SortCombo.SelectedIndex;
        Func<ShipViewModel, IComparable> keySelector = sortIndex switch
        {
            0 => s => s.Id,
            1 => s => s.GameOrder,
            2 => s => s.Name,
            3 => s => GetRaritySortValue(s),
            4 => s => s.Owned,
            5 => s => s.Oath,
            6 => s => s.Breakthrough,
            7 => s => s.Level120,
            8 => s => s.Remodeled,
            9 => s => s.SpecialGearObtained,
            _ => s => s.Id
        };

        var sorted = _isAscending
        ? _currentShips.OrderBy(keySelector).ToList()
        : _currentShips.OrderByDescending(keySelector).ToList();


        for (int i = 0; i < sorted.Count; i++)
        {
            int oldIndex = _currentShips.IndexOf(sorted[i]);
            if (oldIndex != i)
                _currentShips.Move(oldIndex, i);
        }
    }

    private void OnShipDataChanged()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
            // 保存当前选中舰船 ID 和滚动位置
            _lastSelectedShipId = ShipListView.SelectedItem is ShipViewModel selected ? selected.Id : -1;
            var scrollViewer = FindVisualChild<ScrollViewer>(ShipListView);
            if (scrollViewer != null)
            {
                _lastScrollOffset = scrollViewer.VerticalOffset;
            }

            RefreshShipList();

            // 恢复选中项
            if (_lastSelectedShipId != -1)
            {
                var shipToSelect = _currentShips.FirstOrDefault(s => s.Id == _lastSelectedShipId);
                if (shipToSelect != null)
                {
                    ShipListView.SelectedItem = shipToSelect;
                }
            }

            // 恢复滚动位置
            if (scrollViewer != null && _lastScrollOffset > 0)
            {
                // 需要延迟执行，等待 ListView 更新布局
                DispatcherQueue.TryEnqueue(() =>
                {
                    scrollViewer.ChangeView(null, _lastScrollOffset, null, true);
                });
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private void SelectAll_Checked(object sender, RoutedEventArgs e)
    {
        foreach (var ship in _currentShips)
            ship.IsSelected = true;
    }

    private void SelectAll_Unchecked(object sender, RoutedEventArgs e)
    {
        foreach (var ship in _currentShips)
            ship.IsSelected = false;
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
                // 清空建议列表
                sender.ItemsSource = null;
            }
            else
            {
                // 过滤建议项（不区分大小写）
                _currentSuggestions = _allSuggestions
                    .Where(item => item.DisplayText.Contains(input, StringComparison.OrdinalIgnoreCase))
                    .Take(30)   // 限制显示数量，避免过多
                    .ToList();
                sender.ItemsSource = _currentSuggestions;
            }
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
            // 将搜索框的文本设置为纯关键词（去掉前缀）
            sender.Text = item.SearchText;
            // 立即执行搜索（可选）
            RefreshShipList();
        }
    }
    private int GetRaritySortValue(ShipViewModel ship)
    {
        int baseValue = RarityOrderMap.GetValueOrDefault(ship.Rarity, 99);
        // 已改造且可改造的舰船稀有度提升一级（若未达最高）
        if (ship.Remodeled && ship.CanRemodel && baseValue < RarityOrderMap.Count - 1)
            return baseValue + 1;
        return baseValue;
    }
    private static readonly Dictionary<string, int> RarityOrderMap = new()
    {
        ["普通"] = 0,
        ["稀有"] = 1,
        ["精锐"] = 2,
        ["超稀有"] = 3,
        ["海上传奇"] = 4
    };
    private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshShipList();
    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshShipList();

    private async void BatchOperation_Click(object sender, RoutedEventArgs e)
    {
        var selectedShips = _currentShips.Where(s => s.IsSelected).ToList();
        if (selectedShips.Count == 0)
        {
            var dialog = new ContentDialog
            {
                Title = "批量操作",
                Content = "请先勾选要操作的舰船",
                CloseButtonText = "确定",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }

        // 弹出菜单让用户选择操作类型
        var menu = new MenuFlyout();
        var operations = new (string text, Action<ShipViewModel> action)[]
        {
            ("标记为已获得", s => s.Owned = true),
            ("标记为未获得", s => s.Owned = false),
            ("标记为已满破", s => s.Breakthrough = 3),
            ("标记为未满破", s => s.Breakthrough = 0),
            ("标记为已120级", s => s.Level120 = true),
            ("标记为未120级", s => s.Level120 = false),
            ("标记为已誓约", s => s.Oath = true),
            ("标记为未誓约", s => s.Oath = false),
            ("标记为已改造", s => s.Remodeled = true),
            ("标记为未改造", s => s.Remodeled = false),
            ("标记为已获得特殊兵装", s => s.SpecialGearObtained = true),
            ("标记为未获得特殊兵装", s => s.SpecialGearObtained = false),
        };
        foreach (var op in operations)
        {
            var item = new MenuFlyoutItem { Text = op.text };
            item.Click += (s, args) =>
            {
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
    private bool _isAscending = true;  // true=升序，false=降序

    private void SortOrderToggle_Checked(object sender, RoutedEventArgs e)
    {
        _isAscending = false;
        SortOrderToggle.Content = "▲";
        RefreshShipList();
    }

    private void SortOrderToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _isAscending = true;
        SortOrderToggle.Content = "▼";
        RefreshShipList();
    }

    private async void FilterButton_Click(object sender, RoutedEventArgs e)
    {
        var filterPanel = new FilterPanel();
        if (_currentFilterCriteria != null)
        {
            filterPanel.SetCriteria(_currentFilterCriteria);
        }

        var dialog = new ContentDialog
        {
            Title = "筛选",
            Content = filterPanel,
            PrimaryButtonText = "确定",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.Content.XamlRoot,
            MinWidth = 600
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
            source = source.Where(s =>
            {
                bool hasObtain = criteria.AttributeBonuses.Contains(s.ObtainBonusAttr);
                bool hasLevel120 = criteria.AttributeBonuses.Contains(s.Level120BonusAttr);
                return hasObtain || hasLevel120;
            });
        }
        return source;
    }
    private void ResetFilter_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        SortCombo.SelectedIndex = 0;
        if (SortOrderToggle.IsChecked == true)
            SortOrderToggle.IsChecked = false;

        _currentFilterCriteria = null;
        RefreshShipList();

    }
    private async void AddShipButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddShipDialog();
        dialog.XamlRoot = this.XamlRoot;
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
            // 同时选中该行（可选）
            ShipListView.SelectedItem = ship;
        }
        else
        {
            _contextShip = null;
        }
        var app = Application.Current as App;
        bool isDeveloper = app?.AccountManager?.IsDeveloper() ?? false;
        // Use FindName and null-checks so code compiles even if XAML does not define these names
        var menuDelete = this.FindName("ContextMenuDelete") as UIElement;
        if (menuDelete != null)
            menuDelete.Visibility = isDeveloper ? Visibility.Visible : Visibility.Collapsed;
        var separatorDelete = this.FindName("SeparatorDelete") as UIElement;
        if (separatorDelete != null)
            separatorDelete.Visibility = isDeveloper ? Visibility.Visible : Visibility.Collapsed;

    }
    private void ContextMenu_Owned_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.Owned = true;
        _shipManager.Save();
    }

    private void ContextMenu_NotOwned_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.Owned = false;
        // 取消拥有时清除其他状态
        _contextShip.Breakthrough = 0;
        _contextShip.Oath = false;
        _contextShip.Level120 = false;
        _contextShip.Remodeled = false;
        _contextShip.SpecialGearObtained = false;
        _shipManager.Save();
    }

    private void ContextMenu_MaxBreak_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.Breakthrough = 3;
        _shipManager.Save();
    }

    private void ContextMenu_NotMaxBreak_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.Breakthrough = 0;
        _shipManager.Save();
    }

    private void ContextMenu_Level120_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.Level120 = true;
        _shipManager.Save();
    }

    private void ContextMenu_NotLevel120_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.Level120 = false;
        _shipManager.Save();
    }

    private void ContextMenu_Oath_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.Oath = true;
        _shipManager.Save();
    }

    private void ContextMenu_NotOath_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.Oath = false;
        _shipManager.Save();
    }

    private void ContextMenu_Remodeled_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        if (_contextShip.CanRemodel)
        {
            _contextShip.Remodeled = true;
            _shipManager.Save();
        }
    }

    private void ContextMenu_NotRemodeled_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.Remodeled = false;
        _shipManager.Save();
    }

    private void ContextMenu_SpecialGear_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.SpecialGearObtained = true;
        _shipManager.Save();
    }

    private void ContextMenu_NotSpecialGear_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;
        _contextShip.SpecialGearObtained = false;
        _shipManager.Save();
    }

    private async void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_contextShip == null) return;

        // 确认删除
        var dialog = new ContentDialog
        {
            Title = "确认删除",
            Content = $"确定要删除舰船 {_contextShip.Name} 吗？此操作不可恢复。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            XamlRoot = this.XamlRoot
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // 调用 ShipManager 的删除方法（需要实现）
            _shipManager.DeleteShip(_contextShip.Id);
            RefreshShipList();
        }
    }
    private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}