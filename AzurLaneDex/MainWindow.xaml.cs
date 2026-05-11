using AzurLaneDex.Services;
using AzurLaneDex.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.UI.Core;

namespace AzurLaneDex;

public sealed partial class MainWindow : Window
{
    private bool _initialized = false;
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
        while (this.Content?.XamlRoot == null)
            await Task.Delay(50);

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
                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    Application.Current.Exit();
                    return;
                }
            }
        }

        // === 2. 加载舰船数据 ===
        app.ShipManager = new ShipManager(app.AccountManager);

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
        if (ContentFrame.Content == null || ContentFrame.Content.GetType() != typeof(MainPage))
        {
            ContentFrame.Navigate(typeof(MainPage));
        }
    }

    // 导航栏事件（原有）
    private void MainNavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.InvokedItemContainer.Tag is string tag)
        {
            ContentFrame.Navigate(tag switch
            {
                "MainPage" => typeof(MainPage),
                "CampTechPage" => typeof(CampTechPage),
                "AttrBonusPage" => typeof(AttrBonusPage),
                "StatsPage" => typeof(StatsPage),
                "SettingsPage" => typeof(SettingsPage),
                "AccountPage" => typeof(AccountPage),
                "HelpPage" => typeof(HelpPage),
                _ => typeof(MainPage)
            });
        }
    }

    private void TitleBar_BackRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack();
    }

    private void TitleBar_PaneToggleRequested(Microsoft.UI.Xaml.Controls.TitleBar sender, object args)
    {
        MainNavView.IsPaneOpen = !MainNavView.IsPaneOpen;
    }

    public void NavigateTo(Type pageType, object parameter = null)
    {
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
        // 根据导航到的页面类型，高亮侧边栏对应的菜单项
        string tag = e.SourcePageType.Name switch
        {
            nameof(MainPage) => "MainPage",
            nameof(CampTechPage) => "CampTechPage",
            nameof(AttrBonusPage) => "AttrBonusPage",
            nameof(StatsPage) => "StatsPage",
            nameof(SettingsPage) => "SettingsPage",
            nameof(AccountPage) => "AccountPage",
            // 如果有其他二级页面但不属于主菜单，可以选择不改变高亮或回到默认
            _ => null
        };
        if (tag != null)
            SetSelectedNavItem(tag);
    }
}