using AzurLaneDex.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;

namespace AzurLaneDex.Views
{
    public sealed partial class HelpPage : Page
    {
        public HelpPage()
        {
            this.InitializeComponent();
            RefreshDataPath();
        }

        /// <summary>
        /// 显示**真实**数据目录（App.DataRoot），不再写死路径。
        /// 打包调试时路径可能是 …\Packages\{PFN}\LocalState，或被容器重定向到 LocalCache\Local；
        /// 进程被降权时还会退到 LocalLow —— 只有运行时取值才准确。
        /// </summary>
        private void RefreshDataPath()
        {
            DataPathBox.Text = string.IsNullOrEmpty(App.DataRoot)
                ? "（尚未确定；应用启动后才有值）"
                : App.DataRoot;
        }

        /// <summary>用文件资源管理器打开数据目录；目录不存在时说明原因（多半是只读运行）。</summary>
        private async void OnOpenDataFolderClick(object sender, RoutedEventArgs e)
        {
            try
            {
                string dir = App.DataRoot;

                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    var dialog = new ContentDialog
                    {
                        Title = "数据目录不可用",
                        Content = string.IsNullOrEmpty(dir)
                            ? "数据目录尚未确定，请重启应用后再试。"
                            : $"目录不存在：\n{dir}\n\n可能是当前处于只读运行（数据目录不可写），因此没有生成任何文件。",
                        CloseButtonText = "知道了",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return;
                }

                // 打包应用拥有 runFullTrust，直接调 explorer 打开即可；
                // 未用 Launcher.LaunchFolderPathAsync 是因为它对部分受保护路径会静默失败。
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                LogService.Warning($"打开数据目录失败：{ex.GetType().Name}: {ex.Message}", nameof(HelpPage));
            }
        }
    }
}
