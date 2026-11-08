using AzurLaneDex.Helpers;
using AzurLaneDex.Services;
using AzurLaneDex.Views;
using Microsoft.UI.Composition;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
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
        // 统一用应用自己的 window_state.json 持久化窗口状态：
        // WinUIEx 默认经 ApplicationData（无包身份或身份不完整时会抛
        // "Error trying to open the explicit application data handle"），因此不启用它的存储后端。
        WinUIEx.WindowManager.PersistenceStorage = App.WindowState;
        this.Closed += (_, _) => App.SaveWindowState();
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

        // 启动画面覆盖层：底色随系统主题、加载居中徽标（在窗口显示前完成，避免闪帧）
        InitializeSplashOverlay();

        // 响应窗口激活事件（确保 XamlRoot 可用）
        this.Activated += OnWindowActivated;
    }

    // ================= 启动动画（阶段 1 启动画面 / 阶段 2 窗口淡入） =================

    /// <summary>启动画面深色主题底色（Windows 11 深色基础背景色）。</summary>
    private static readonly Windows.UI.Color SplashColorDark = Windows.UI.Color.FromArgb(0xFF, 0x20, 0x20, 0x20);
    /// <summary>启动画面浅色主题底色（Windows 11 浅色基础背景色）。</summary>
    private static readonly Windows.UI.Color SplashColorLight = Windows.UI.Color.FromArgb(0xFF, 0xF3, 0xF3, 0xF3);

    /// <summary>
    /// 初始化启动画面覆盖层：按当前系统主题铺底色、加载居中徽标，并订阅主题变化。
    /// 在窗口构造阶段调用（窗口尚未显示），因此不会出现"先闪真实界面再被盖住"。
    /// </summary>
    private void InitializeSplashOverlay()
    {
        try
        {
            ApplySplashTheme();
            if (this.Content is FrameworkElement root)
                root.ActualThemeChanged += (s, e) => ApplySplashTheme();

            // 启动徽标：打包环境经 MRT 解析未限定名；失败回退 scale-100 实体文件
            SplashLogo.ImageFailed += (s, e) =>
            {
                try { SplashLogo.Source = new BitmapImage(new Uri("ms-appx:///Assets/SplashScreen.scale-100.png")); }
                catch { /* 无启动图时仅显示底色 */ }
            };
            SplashLogo.Source = new BitmapImage(new Uri("ms-appx:///Assets/SplashScreen.png"));
        }
        catch (Exception ex)
        {
            LogService.Warning($"启动画面初始化失败: {ex.Message}", nameof(MainWindow));
        }
    }

    /// <summary>启动画面底色跟随系统主题（深色 #202020 / 浅色 #F3F3F3）。</summary>
    private void ApplySplashTheme()
    {
        try
        {
            var theme = (this.Content as FrameworkElement)?.ActualTheme ?? ElementTheme.Default;
            var color = theme == ElementTheme.Light ? SplashColorLight : SplashColorDark;
            SplashOverlay.Background = new SolidColorBrush(color);
        }
        catch { /* 忽略 */ }
    }

    /// <summary>
    /// 揭幕：淡出启动覆盖层 + 内容淡入/缩放，随后收起覆盖层（不再遮挡交互）。
    /// 无论动画是否成功，覆盖层最终一定会被收起——启动画面不可能"关不掉"。
    /// </summary>
    private void RevealContent()
    {
        try
        {
            // 内容淡入 + 缩放（合成线程）
            PlayWindowRevealAnimation();

            // 阶段 3：内容渐进式显示——导航栏(0ms) → 列表(60ms) → 详情(120ms) 依次淡入上滑
            StartupAnimations.PlayReveal(MainNavView, 0);
            if (ContentFrame.Content is MainPage page)
                page.PlayStartupReveal();

            // 覆盖层淡出
            var visual = ElementCompositionPreview.GetElementVisual(SplashOverlay);
            var compositor = visual.Compositor;
            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));
            var fade = compositor.CreateScalarKeyFrameAnimation();
            fade.InsertKeyFrame(1f, 0f, easing);
            fade.Duration = TimeSpan.FromMilliseconds(250);
            visual.StartAnimation("Opacity", fade);

            var timer = SplashOverlay.DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(260);
            timer.IsRepeating = false;
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                SplashOverlay.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            LogService.Warning($"启动画面淡出失败: {ex.Message}", nameof(MainWindow));
            SplashOverlay.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 窗口缩放进场：96% → 100%，250ms，Fluent「减速进入」曲线，缩放原点 = 窗口中心。
    /// 只做缩放不做整层淡入——「淡入」由启动覆盖层淡出与随后各区域错峰淡入承担，
    /// 否则两级淡入叠加会把内容二次压暗、看不清渐进效果。
    /// 采用 Composition 动画（合成线程），UI 线程此时仍在做初始化也不会卡顿。
    /// </summary>
    private void PlayWindowRevealAnimation()
    {
        try
        {
            if (this.Content is not FrameworkElement root) return;

            var visual = ElementCompositionPreview.GetElementVisual(root);
            var compositor = visual.Compositor;

            // 以窗口中心为缩放原点（布局已完成时尺寸可信；极端情况下回退到 XamlRoot 尺寸）
            double w = root.ActualWidth > 0 ? root.ActualWidth : root.XamlRoot?.Size.Width ?? 0;
            double h = root.ActualHeight > 0 ? root.ActualHeight : root.XamlRoot?.Size.Height ?? 0;
            visual.CenterPoint = new Vector3((float)(w / 2d), (float)(h / 2d), 0f);
            visual.Opacity = 1f;
            visual.Scale = new Vector3(0.96f, 0.96f, 1f);

            var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f));

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(1f, Vector3.One, easing);
            scale.Duration = TimeSpan.FromMilliseconds(250);

            visual.StartAnimation("Scale", scale);
        }
        catch (Exception ex)
        {
            // 动画异常时保证界面可见，绝不能因动画导致启动失败
            try
            {
                if (this.Content is UIElement r)
                {
                    var v = ElementCompositionPreview.GetElementVisual(r);
                    v.StopAnimation("Scale");
                    v.Opacity = 1f;
                }
            }
            catch { /* 忽略 */ }
            LogService.Warning($"窗口缩放动画失败: {ex.Message}", nameof(MainWindow));
        }
    }

    /// <summary>
    /// 交叉过渡：主窗口淡入 + 缩放，同时淡出启动画面。
    /// 首帧布局尚未完成时（ActualWidth 为 0）等一次 LayoutUpdated 再播放，保证缩放原点为窗口中心。
    /// </summary>
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

        // === 1. 窗口已以启动画面观感出现 ===
        // AccountManager 与界面语言已在 App.OnLaunched 完成（语言必须在创建 UI 之前应用）；
        // 账户对话框统一放到「揭幕之后」（见第 4 步），避免在启动画面上弹出对话框

        // === 2. 加载舰船数据（此时窗口显示的是启动画面覆盖层，装载完成后再揭幕）===
        try
        {
            if (app.ShipManager != null)
                await app.ShipManager.LoadAsync();
        }
        catch (Exception ex)
        {
            // 装载失败不能让启动画面卡住：记录后继续走揭幕流程
            LogService.Error("启动期加载舰船数据失败", nameof(MainWindow), ex);
        }

        // 头像体检：逐条记录「找不到头像文件」的舰船（id / global_name / 目录 / 候选名），便于补图
        try
        {
            if (app.ShipManager != null && app.ShipManager.Ships.Count > 0)
                AvatarAudit.Run(app.ShipManager.Ships);
        }
        catch (Exception ex)
        {
            LogService.Warning("头像体检失败：" + ex.Message, nameof(MainWindow));
        }

        // 头像旧目录迁移（首启可能搬运大量文件，放在窗口出现之后，不拖慢启动画面出现）
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
        {
            LogService.SetSettings(true, 30);
        }

        // === 3. 揭幕：启动画面结束（此后才显示任何对话框）===
        RevealContent();

        // === 4. 账户对话框：首次运行 / 未指定当前账户 ===
        // 放在揭幕之后，避免对话框压在启动画面上
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

        // === 5. 读取“询问账号”配置 ===
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
        // 未询问账号（或只有一个账户）：优先使用「登录选项」里配置的默认账户，其次单账户自动登录
        else
        {
            string defaultAccount = app.ShipManager.Config.TryGetValue("default_account", out var defObj)
                ? defObj?.ToString() ?? ""
                : "";
            string autoLogin = !string.IsNullOrEmpty(defaultAccount) && regularAccounts.Contains(defaultAccount)
                ? defaultAccount
                : (regularAccounts.Count == 1 ? regularAccounts[0] : "");

            if (!string.IsNullOrEmpty(autoLogin))
            {
                System.Diagnostics.Debug.WriteLine($"Auto login to {autoLogin}");
                app.AccountManager.SetCurrentAccount(autoLogin);
            }
        }

        // === 6. 账户被对话框改变时，按新账户重新加载用户状态与配队 ===
        // （数据装载发生在对话框之前，那时用的是配置中的账户）
        string? activeAccount = app.AccountManager?.CurrentAccount;
        if (app.ShipManager != null && !string.IsNullOrEmpty(activeAccount) &&
            !string.Equals(app.ShipManager.CurrentAccount, activeAccount, StringComparison.Ordinal))
        {
            System.Diagnostics.Debug.WriteLine($"账户变化，重新加载用户数据: {activeAccount}");
            await app.ShipManager.SwitchAccountAsync(activeAccount);
        }

        // === 7. 收尾 ===
        // 按账户权限显示开发者工具入口（数据编辑器，仅开发者可见）
        // 恢复侧边栏展开状态
        if (app.ShipManager != null)
        {
            bool savedOpen = LoadPaneOpenState();
            MainNavView.IsPaneOpen = savedOpen;
        }

        // === 8. 头像包后台更新检查（不阻塞 UI；有新版本时 InfoBar 通知） ===
        app.AvatarUpdateService = new AvatarUpdateService();
        app.AvatarUpdateService.UpdateCompleted += OnAvatarUpdateCompleted;
        // 应用退出时取消进行中的网络请求（避免退出期 I/O 中止异常噪音）
        this.Closed += (_, _) => app.AvatarUpdateService?.CancelAll();
        _ = RunAvatarAutoCheckAsync();
    }

    // ================= 头像包更新 =================

    /// <summary>资源键取词（缺失回退中文兜底）。</summary>
    private static string L(string key, string fallback) {
            string text = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse().GetString(key);
            return string.IsNullOrEmpty(text) ? fallback : text;
    }

    private Services.AvatarUpdateService.AvatarManifest? _pendingAvatarManifest;

    private void AvatarDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        AvatarUpdateInfoBar.IsOpen = false;
        if (_pendingAvatarManifest != null)
            _ = ShowAvatarDownloadDialogAsync(_pendingAvatarManifest);
    }

    /// <summary>公开：按 manifest 弹出下载进度对话框（设置页/通知条共用）。</summary>
    public Task ShowAvatarDownloadDialogAsync(Services.AvatarUpdateService.AvatarManifest manifest)
        => DownloadAvatarCoreAsync(manifest);

    /// <summary>启动后台检查：按 avatar_auto_check 开关执行，5 分钟内不重复打扰。</summary>
    private async Task RunAvatarAutoCheckAsync()
    {
        var app = (App)Application.Current;
        var svc = app.AvatarUpdateService;
        var mgr = app.ShipManager;
        if (svc == null || mgr == null) return;

        bool autoCheck = true;
        if (mgr.Config.TryGetValue("avatar_auto_check", out var autoObj) && autoObj is bool autoB)
            autoCheck = autoB;
        if (!autoCheck) return;

        // 防骚扰：最近提示过则跳过（5 分钟）
        if (mgr.Config.TryGetValue("avatar_last_prompt_ticks", out var lastObj) &&
            long.TryParse(lastObj?.ToString(), out var lastTicks) &&
            DateTime.UtcNow - new DateTime(lastTicks, DateTimeKind.Utc) < TimeSpan.FromMinutes(5))
        {
            return;
        }
        MarkAvatarPrompted();

        var manifest = await svc.CheckForUpdateAsync();
        if (manifest == null) return;                       // 网络失败静默
        if (string.Equals(manifest.Version, svc.GetLocalVersion(), StringComparison.Ordinal)) return; // 已最新

        _pendingAvatarManifest = manifest;
        string local = string.IsNullOrEmpty(svc.GetLocalVersion()) ? L("Settings_Avatar_NotInstalled", "未安装") : svc.GetLocalVersion();
        string msg = string.Format(L("MainWindow_AvatarInfoMessage", "发现新头像包 v{0}（本地：{1}）。是否下载？"), manifest.Version, local);
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            AvatarUpdateInfoBar.Title = L("MainWindow_AvatarInfoTitle", "头像包更新可用");
            AvatarUpdateInfoBar.Message = msg;
            AvatarUpdateInfoBar.Severity = InfoBarSeverity.Informational;
            AvatarDownloadButton.Visibility = Visibility.Visible;
            AvatarUpdateInfoBar.IsOpen = true;
        });
    }

    private void MarkAvatarPrompted()
    {
        var mgr = ((App)Application.Current).ShipManager;
        if (mgr?.Config == null) return;
        mgr.Config["avatar_last_prompt_ticks"] = DateTime.UtcNow.Ticks.ToString();
        mgr.SaveConfig();
    }

    /// <summary>头像包更新完成：刷新全部头像绑定并提示。</summary>
    private void OnAvatarUpdateCompleted(string version)
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            RefreshAllAvatarBindings();
            AvatarDownloadButton.Visibility = Visibility.Collapsed;
            AvatarUpdateInfoBar.Title = L("MainWindow_AvatarDoneTitle", "头像包已更新");
            AvatarUpdateInfoBar.Message = string.Format(L("MainWindow_AvatarDoneMessage", "已更新到 v{0}。"), version);
            AvatarUpdateInfoBar.Severity = InfoBarSeverity.Success;
            AvatarUpdateInfoBar.IsOpen = true;
        });
    }

    /// <summary>通知所有舰船 ViewModel 头像属性刷新（绑定自动重载图片）。</summary>
    private void RefreshAllAvatarBindings()
    {
        var ships = ((App)Application.Current).ShipManager?.Ships;
        if (ships == null) return;
        foreach (var vm in ships)
            vm.NotifyAvatarChanged();
    }

    /// <summary>下载头像包：进度对话框（可取消），完成后清理并自动关闭。</summary>
    private async Task DownloadAvatarCoreAsync(Services.AvatarUpdateService.AvatarManifest? manifest)
    {
        var app = (App)Application.Current;
        var svc = app.AvatarUpdateService;
        if (svc == null || manifest == null || this.Content?.XamlRoot == null) return;

        var cts = new CancellationTokenSource();
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Width = 360,
            Height = 24,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var stack = new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = string.Format(L("MainWindow_AvatarProgressDesc", "头像包 v{0} 将下载并解压到本地 avatars/ 目录。"), manifest.Version),
                    TextWrapping = TextWrapping.Wrap
                },
                bar
            }
        };
        var dialog = new ContentDialog
        {
            Title = string.Format(L("MainWindow_AvatarProgressTitle", "下载头像包 v{0}"), manifest.Version),
            Content = stack,
            CloseButtonText = L("MainWindow_AvatarCancel", "取消"),
            XamlRoot = this.Content.XamlRoot,
            Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
        };

        var shownTask = dialog.ShowAsync().AsTask();
        var progress = new Progress<double>(value =>
        {
            bar.Value = Math.Clamp(value * 100, 0, 100);
        });

        try
        {
            var downloadTask = svc.DownloadAndExtractAsync(manifest, progress, cts.Token);
            var first = await Task.WhenAny(downloadTask, shownTask);
            if (first == shownTask)
            {
                // 用户点“取消”
                cts.Cancel();
                try { await downloadTask; }
                catch (OperationCanceledException) { /* 已取消，临时文件由服务清理 */ }
                return;
            }
            await downloadTask; // 成功（UpdateCompleted 已触发 UI 刷新）
            bar.Value = 100;
            try { dialog.Hide(); } catch { /* 可能已被关闭 */ }
        }
        catch (OperationCanceledException)
        {
            /* 取消 */
        }
        catch (Exception ex)
        {
            try { dialog.Hide(); } catch { }
            await ShowMessageDialog(L("MainWindow_AvatarFailTitle", "头像下载失败"),
                string.Format(L("MainWindow_AvatarFailMessage", "下载/解压头像包失败：\n{0}\n\n请检查网络连接或磁盘空间，稍后重试。"), ex.Message));
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
                "TeamPage" => typeof(TeamPage),
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
        // 遍历主菜单与底部菜单（开发者工具位于底部）
        foreach (var container in new[] { MainNavView.MenuItems, MainNavView.FooterMenuItems })
        {
            foreach (NavigationViewItem item in container)
            {
                if (item.Tag?.ToString() == tag)
                {
                    MainNavView.SelectedItem = item;
                    return;
                }
            }
        }
    }

    /// <summary>按当前账户权限显示/隐藏开发者工具入口（数据编辑器）。</summary>
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
            nameof(TeamPage) => "TeamPage",
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