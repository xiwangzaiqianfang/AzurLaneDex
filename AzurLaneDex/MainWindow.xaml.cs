using AzurLaneDex.Services;
using AzurLaneDex.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.UI.Core;
using WinUIEx;

namespace AzurLaneDex;

public sealed partial class MainWindow : Window
{
    private bool _initialized = false;
    private bool _isNavigatingByUser = false;
    public Frame AppContentFrame => ContentFrame;

    public MainWindow()
    {
        this.InitializeComponent();
        // 窗口外观配置
        var manager = WinUIEx.WindowManager.Get(this);
        manager.PersistenceId = "MainWindow";
        this.ExtendsContentIntoTitleBar = true;
        var titleBar = this.AppWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;
        titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        this.SetTitleBar(AppTitleBar);
        AppTitleBar.MinHeight = 48;
        manager.Width = 1310;
        manager.Height = 750;

        // 默认导航到舰船图鉴页（内容将显示在 ContentFrame 中）
        ContentFrame.Navigated += ContentFrame_Navigated;
        ContentFrame.Navigate(typeof(MainPage));
        SetSelectedNavItem("MainPage");

        // 响应窗口激活事件（确保 XamlRoot 可用）
        this.Activated += OnWindowActivated;
    }

    private async void OnWindowActivated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (_initialized) return;
        _initialized = true;
        this.Activated -= OnWindowActivated;

        // 等待 XamlRoot 就绪
        int timeoutMs = 5000;
        int elapsedMs = 0;
        while (this.Content?.XamlRoot == null && elapsedMs < timeoutMs)
        {
            await Task.Delay(50);
            elapsedMs += 50;
        }
        if (this.Content?.XamlRoot == null)
        {
            // 超时后仍无 XamlRoot，记录错误并尝试继续（部分控件可能无法正常弹窗）
            System.Diagnostics.Debug.WriteLine("警告：XamlRoot 在超时后仍未就绪，部分对话框可能无法显示。");
        }

        var app = (App)Application.Current;

        // === 1. 账户初始化 ===
        app.AccountManager = new AccountManager();
        System.Diagnostics.Debug.WriteLine($"Regular accounts: {app.AccountManager.GetRegularAccountCount()}");
        foreach (var acc in app.AccountManager.Accounts)
        {
            System.Diagnostics.Debug.WriteLine($"Account: {acc.Name}, IsSystem: {acc.IsSystem}");
        }

        // 首次运行：无普通账户，弹出新建账户对话框
        if (app.AccountManager.GetRegularAccountCount() == 0)
        {
            var firstRunDialog = new FirstRunDialog();
            firstRunDialog.XamlRoot = this.Content.XamlRoot;
            firstRunDialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style; // 添加样式

            if (await firstRunDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var info = firstRunDialog.GetAccountInfo();

                if (app.AccountManager.AddAccount(info.Name, info.Password, info.Avatar, info.IsDeveloper))
                {
                    app.AccountManager.SetSecurityQuestion(info.Name, info.SecurityQuestion, info.SecurityAnswer);
                    app.AccountManager.SetCurrentAccount(info.Name);
                    app.AccountManager.Save();
                }
                else
                {
                    Application.Current.Exit();
                    return;
                }
            }
            // 有普通账户，但未指定当前账户（例如账户文件损坏），则弹出登录对话框
            else if (string.IsNullOrEmpty(app.AccountManager.CurrentAccount))
            {
                var dialog = new AccountLoginDialog(app.AccountManager, requirePassword: false);
                dialog.XamlRoot = this.Content.XamlRoot;
                dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    Application.Current.Exit();
                    return;
                }
            }
        }

        // === 2. 加载舰船数据 ===
        var shipManager = new ShipManager(app.AccountManager);
        app.ShipManager = shipManager;
        await shipManager.LoadAsync();

        if (app.ShipManager?.Config != null)
        {
            string savedLang = app.ShipManager.Config.GetValueOrDefault("app_language")?.ToString();
            if (!string.IsNullOrEmpty(savedLang))
            {
                ApplicationLanguages.PrimaryLanguageOverride = savedLang;
            }
        }

        var resourceManager = new ImageResourceManager(App.DataRoot);
        resourceManager.MigrateLegacyAvatarFolders();

        var avatarManager = new FactionAvatarManager();
        app.FactionAvatarManager = avatarManager;

        avatarManager.FactionAvatarUpdated += (factionId, version) =>
        {
            // 头像更新完成后，可以通知MainPage或ShipDetailControl刷新特定阵营的头像
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                // 发送一个全局消息或调用刷新方法
            });
        };

        // _ = Task.Run(async () => await avatarManager.UpdateAllFactionAvatarsAsync());
        /*
        var avatarUpdateResult = await avatarManager.UpdateAllFactionAvatarsAsync();
        switch (avatarUpdateResult.Result)
        {
            case AvatarUpdateResult.NetworkError:
                await ShowMessageDialog("网络错误", avatarUpdateResult.Message);
                break;
            case AvatarUpdateResult.ServerError:
                await ShowMessageDialog("服务器错误", avatarUpdateResult.Message);
                break;
            case AvatarUpdateResult.OtherError:
                await ShowMessageDialog("更新失败", avatarUpdateResult.Message);
                break;
            case AvatarUpdateResult.UpdateAvailable:
                // 弹出询问是否下载的对话框
                var dialog = new ContentDialog
                {
                    Title = "头像更新可用",
                    Content = $"发现有 {avatarUpdateResult.AvailableFactions.Count} 个阵营的头像可以更新。是否现在下载？\n\n（头像文件较大，请在网络良好时进行）",
                    PrimaryButtonText = "下载",
                    CloseButtonText = "稍后",
                    XamlRoot = this.Content.XamlRoot,
                    Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // 用户确认下载
                    var progressDialog = new ContentDialog
                    {
                        Title = "正在下载头像...",
                        Content = new ProgressBar { IsIndeterminate = true, Margin = new Thickness(0, 10, 0, 0) },
                        CloseButtonText = "取消",
                        XamlRoot = this.Content.XamlRoot
                    };
                    // 显示进度对话框（注意：无法同时进行多个对话框，需要异步处理）
                    // 这里简单实现：实际可显示进度条
                    var downloadTask = avatarManager.DownloadFactionUpdatesAsync(avatarUpdateResult.AvailableFactions);
                    // 此处可以添加进度回调更新进度条
                    var success = await downloadTask;
                    await ShowMessageDialog("下载完成", success ? "头像更新成功。" : "部分头像下载失败，请检查网络。");
                }
                break;
            case AvatarUpdateResult.NoUpdate:
                // 无需提示，静默即可
                break;
            case AvatarUpdateResult.Success:
                // 成功更新（但此情况不会单独出现，因为更新由用户触发）
                break;
        }
        */
        // 加载日志配置
        if (app.ShipManager != null && app.ShipManager.Config.TryGetValue("log_enabled", out var enabledObj))
        {
            LogService.SetSettings(enabledObj is bool b ? b : true,
                app.ShipManager.Config.TryGetValue("log_retention_days", out var daysObj) && daysObj is int days ? days : 30);
        }
        else
            LogService.SetSettings(true, 30);

        // === 3. 读取“询问账号”配置 ===
        bool askAccount = false;
        if (app.ShipManager.Config.TryGetValue("ask_account_on_startup", out var askObj) && askObj is bool askBool)
            askAccount = askBool;
        System.Diagnostics.Debug.WriteLine($"ask_account_on_startup = {askAccount}");

        var regularAccounts = app.AccountManager.GetAccountList();
        System.Diagnostics.Debug.WriteLine($"Regular account count: {regularAccounts.Count}");

        // 如果配置了询问账号且存在多个普通账户，则弹出账户选择对话框（仅选择，无需密码）
        if (askAccount && regularAccounts.Count > 1)
        {
            System.Diagnostics.Debug.WriteLine("About to show account picker dialog");
            var loginDialog = new AccountLoginDialog(app.AccountManager);
            loginDialog.XamlRoot = this.Content.XamlRoot;
            loginDialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            if (await loginDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                // AccountLoginDialog 内部已经调用了 SetCurrentAccount，无需额外处理
                System.Diagnostics.Debug.WriteLine($"用户已登录: {app.AccountManager.CurrentAccount}");
            }
            else
            {
                Application.Current.Exit();
                return;
            }
        }
        // 只有一个普通账户，自动登录
        else if (regularAccounts.Count == 1)
        {
            System.Diagnostics.Debug.WriteLine($"Auto login to {regularAccounts[0]}");
            app.AccountManager.SetCurrentAccount(regularAccounts[0]);
        }

        // 所有初始化完成，隐藏启动画面
        app.HideSplashScreen();
        // 恢复侧边栏展开状态
        if (app.ShipManager != null)
        {
            bool savedOpen = LoadPaneOpenState();
            MainNavView.IsPaneOpen = savedOpen;
        }
    }

    // 导航栏事件
    private void MainNavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            _isNavigatingByUser = true;
            return;
        }

        if (args.InvokedItemContainer is NavigationViewItem item &&
                item.Tag is string tag &&
                !string.IsNullOrEmpty(tag))
        {
            Type? pageType = tag switch
            {
                "MainPage" => typeof(MainPage),
                "CampTechPage" => typeof(CampTechPage),
                "AttrBonusPage" => typeof(AttrBonusPage),
                "StatsPage" => typeof(StatsPage),
                "SettingsPage" => typeof(SettingsPage),
                "AccountPage" => typeof(AccountPage),
                "HelpPage" => typeof(HelpPage),
                _ => null
            };

            if (pageType != null)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }
    private void MainNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        // 如果不是用户主动点击，则忽略
        if (!_isNavigatingByUser)
            return;
    }

    private void TitleBar_BackRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack();
    }

    private void TitleBar_PaneToggleRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
    {
        MainNavView.IsPaneOpen = !MainNavView.IsPaneOpen;
        SavePaneOpenState(MainNavView.IsPaneOpen);
    }

    public void NavigateTo(Type pageType, object parameter = null)
    {
        _isNavigatingByUser = true;
        ContentFrame.Navigate(pageType, parameter);
    }
    private void OnBackRequested(object sender, BackRequestedEventArgs e)
    {
        if (ContentFrame.CanGoBack)
        {
            e.Handled = true;
            ContentFrame.GoBack();
        }
    }
    public void SetSelectedNavItem(string tag)
    {
        if (tag == "SettingsPage")
        {
            MainNavView.SelectedItem = MainNavView.SettingsItem;
            return;
        }
        foreach (NavigationViewItem item in MainNavView.MenuItems)
        {
            if (item.Tag?.ToString() == tag)
            {
                MainNavView.SelectedItem = item;
                break;
            }
        }
    }
    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        _isNavigatingByUser = false;
        // 根据导航到的页面类型，高亮侧边栏对应的菜单项
        string tag = e.SourcePageType.Name switch
        {
            nameof(MainPage) => "MainPage",
            nameof(CampTechPage) => "CampTechPage",
            nameof(AttrBonusPage) => "AttrBonusPage",
            nameof(StatsPage) => "StatsPage",
            nameof(SettingsPage) => "SettingsPage",
            nameof(AccountPage) => "AccountPage",
            _ => null
        };
        if (tag != null)
            SetSelectedNavItem(tag);
    }
    private void SavePaneOpenState(bool isOpen)
    {
        var app = (App)Application.Current;
        if (app.ShipManager?.Config != null)
        {
            app.ShipManager.Config["nav_pane_open"] = isOpen;
            app.ShipManager.SaveConfig();
        }
    }

    private bool LoadPaneOpenState()
    {
        var app = (App)Application.Current;
        if (app.ShipManager?.Config != null &&
            app.ShipManager.Config.TryGetValue("nav_pane_open", out var obj) &&
            obj is bool savedState)
        {
            return savedState;
        }
        return true; // 默认展开
    }

    private async Task ShowMessageDialog(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "确定",
            XamlRoot = this.Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };
        await dialog.ShowAsync();
    }
}