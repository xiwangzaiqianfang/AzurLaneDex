using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;

namespace AzurLaneDex.Views
{
    /// <summary>
    /// 头像管理子页（自「更新」页进入）：头像包版本、检查更新、立即下载、清理缓存、启动时自动检查。
    /// </summary>
    public sealed partial class AvatarPage : Page
    {
        private App? _app;
        private Services.AvatarUpdateService.AvatarManifest? _latestAvatarManifest;

        /// <summary>资源键取词（缺失回退中文兜底）。</summary>
        private static string L(string key, string fallback) {
                string text = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse().GetString(key);
                return string.IsNullOrEmpty(text) ? fallback : text;
        }

        public AvatarPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _app = Application.Current as App;
            RefreshAvatarStatus();
        }

        // ================= 头像管理 =================

        private void RefreshAvatarStatus()
        {
            var svc = _app?.AvatarUpdateService;
            var cfg = _app?.ShipManager?.Config;

            string local = svc?.GetLocalVersion() ?? "";
            AvatarLocalVersionText.Text = string.IsNullOrEmpty(local) ? L("Settings_Avatar_NotInstalled", "未安装") : local;
            AvatarRemoteVersionText.Text = _latestAvatarManifest == null ? "—" : _latestAvatarManifest.Version;
            AvatarCountText.Text = string.Format(L("Settings_Avatar_CountFormat", "本地头像文件：{0} 个"), svc?.CountLocalAvatars() ?? 0);

            bool auto = true;
            if (cfg != null && cfg.TryGetValue("avatar_auto_check", out var o) && o is bool b) auto = b;
            _syncingToggle = true;
            AvatarAutoCheckSwitch.IsOn = auto;
            _syncingToggle = false;

            DownloadAvatarNowButton.IsEnabled =
                _latestAvatarManifest != null && !string.Equals(_latestAvatarManifest.Version, local, StringComparison.Ordinal);

            StatusText.Text = svc == null ? L("Status_1", "未就绪") : "";
        }

        /// <summary>程序化设置开关时不触发保存（避免仅打开页面就写配置）。</summary>
        private bool _syncingToggle;

        private async void CheckAvatarUpdate_Click(object sender, RoutedEventArgs e)
        {
            var svc = _app?.AvatarUpdateService;
            if (svc == null) return;

            CheckAvatarButton.IsEnabled = false;
            AvatarRemoteVersionText.Text = L("Settings_Avatar_Checking", "检查中…");
            try
            {
                _latestAvatarManifest = await svc.CheckForUpdateAsync();
            }
            catch (Exception ex)
            {
                LogService.Error("检查头像更新失败", nameof(AvatarPage), ex);
            }
            finally
            {
                CheckAvatarButton.IsEnabled = true;
            }

            if (_latestAvatarManifest == null)
            {
                AvatarRemoteVersionText.Text = L("Settings_Avatar_CheckFailed", "检查失败");
                await ShowDialog(L("Settings_Avatar_DialogCheckFailTitle", "检查失败"),
                    L("Settings_Avatar_DialogCheckFailMsg", "无法连接头像更新源（GitHub/Gitee），请检查网络后重试。"));
                return;
            }
            RefreshAvatarStatus();
        }

        private async void DownloadAvatarNow_Click(object sender, RoutedEventArgs e)
        {
            var svc = _app?.AvatarUpdateService;
            var manifest = _latestAvatarManifest;
            if (svc == null || manifest == null) return;
            if (string.Equals(manifest.Version, svc.GetLocalVersion(), StringComparison.Ordinal))
            {
                DownloadAvatarNowButton.IsEnabled = false;
                return;
            }

            var window = _app?.GetMainWindow() as MainWindow;
            if (window == null) return;
            await window.ShowAvatarDownloadDialogAsync(manifest);
            RefreshAvatarStatus();
        }

        private async void ClearAvatarCache_Click(object sender, RoutedEventArgs e)
        {
            var svc = _app?.AvatarUpdateService;
            if (svc == null) return;

            var confirm = new ContentDialog
            {
                Title = L("Settings_Avatar_ClearTitle", "清理头像缓存"),
                Content = string.Format(L("Settings_Avatar_ClearMessage", "将删除本地全部头像文件（当前 {0} 个）并清除版本记录。确认清理？"), svc.CountLocalAvatars()),
                PrimaryButtonText = L("Settings_Avatar_ClearPrimary", "清理"),
                CloseButtonText = L("TeamPage_Cancel", "取消"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                svc.ClearLocalAvatars();
                RefreshAvatarBindings();
                _latestAvatarManifest = null;
                RefreshAvatarStatus();
                LogService.Operation("头像管理", "清理头像缓存");
            }
            catch (Exception ex)
            {
                await ShowDialog(L("Settings_Avatar_ClearFail", "清理失败"), ex.Message);
            }
        }

        private void AvatarAutoCheck_Toggled(object sender, RoutedEventArgs e)
        {
            if (_syncingToggle) return;
            var cfg = _app?.ShipManager?.Config;
            if (cfg == null) return;
            cfg["avatar_auto_check"] = AvatarAutoCheckSwitch.IsOn;
            _app!.ShipManager!.SaveConfig();
        }

        /// <summary>头像文件变化后通知全部舰船 VM 刷新头像绑定。</summary>
        private void RefreshAvatarBindings()
        {
            var ships = _app?.ShipManager?.Ships;
            if (ships == null) return;
            foreach (var vm in ships)
                vm.NotifyAvatarChanged();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private async Task ShowDialog(string title, string content)
        {
            var loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForViewIndependentUse();
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = loader.GetString("Common_Confirm"),
                XamlRoot = this.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style
            };
            await dialog.ShowAsync();
        }
    }
}
