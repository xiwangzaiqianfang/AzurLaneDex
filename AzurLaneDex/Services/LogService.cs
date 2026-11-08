using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace AzurLaneDex.Services
{
    public static class LogService
    {
        private static readonly object _lock = new object();
        private static string _logDirectory;
        private static string _logFilePath;
        private static int _maxRetentionDays = 30;
        private static bool _enabled = true;
        private static bool _initialized = false;
        /// <summary>文件日志是否仍可用（目录被拒写/文件被占用时自动降级为仅调试输出）。</summary>
        private static bool _fileLoggingAvailable = true;

        /// <summary>探测目录可写（Win32 权限探测，不抛异常）。</summary>
        private static bool IsDirectoryWritable(string dir) => FileAccessProbe.CanCreateIn(dir);

        /// <summary>
        /// 确保日志目录存在并返回路径：**只使用数据目录下的 log**。
        /// 数据目录不可写（App.DataRootWritable=false）时直接关闭文件日志，
        /// 不再回退到程序目录/%TEMP%——那些位置在受限运行环境下同样不可写，
        /// 每次尝试都会抛 0x80070005 / DirectoryNotFoundException。
        /// </summary>
        private static string EnsureLogDirectory()
        {
            // 旧版逻辑：直接建目录、直接写；失败才降级，不预先判断可写性
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(App.LogDirectory)) candidates.Add(App.LogDirectory);
            if (!string.IsNullOrEmpty(App.DataRoot)) candidates.Add(Path.Combine(App.DataRoot, "log"));
            try
            {
                candidates.Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AzurLaneDex", "data", "log"));
            }
            catch { }

            foreach (string dir in candidates)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    return dir;
                }
                catch { /* 换下一个 */ }
            }

            _fileLoggingAvailable = false;
            return "";
        }

        // 自动初始化（首次调用日志方法时执行）
        private static void Initialize()
        {
            if (_initialized) return;
            lock (_lock)
            {
                if (_initialized) return;
                _logDirectory = EnsureLogDirectory();
                _logFilePath = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
                LoadSettings();
                CleanOldLogs();
                _initialized = true;
            }
        }

        /// <summary>当前日志目录（供日志页显示 / 导出定位；未初始化时先初始化）。</summary>
        public static string LogDirectoryPath
        {
            get
            {
                Initialize();
                if (!string.IsNullOrEmpty(_logDirectory)) return _logDirectory;

                // 只读运行（数据目录不可写）时，仍给出预期路径供界面显示
                string root = App.DataRoot;
                return string.IsNullOrEmpty(root) ? "" : Path.Combine(root, "log");
            }
        }

        /// <summary>
        /// 数据目录确定后重新绑定日志目录（首次初始化可能发生在 App.DataRoot 赋值之前，
        /// 那时会落到 程序目录\App_Data\logs）。切换到新目录时把旧目录里的日志文件补过来。
        /// </summary>
        internal static void RebindLogDirectory()
        {
            lock (_lock)
            {
                string dir = EnsureLogDirectory();
                if (string.IsNullOrEmpty(dir)) return;

                _fileLoggingAvailable = true;   // 数据目录就绪 → 重新开启文件日志（首次初始化时可能因 DataRoot 未设置而关闭）

                if (string.Equals(dir, _logDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    _initialized = true;
                    return;
                }

                string oldDir = _logDirectory ?? "";
                _logDirectory = dir;
                _logFilePath = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyy-MM-dd}.txt");
                _initialized = true;
                MoveExistingLogs(oldDir, dir);
                System.Diagnostics.Debug.WriteLine($"[LogService] 日志目录：{dir}");
            }
        }

        /// <summary>把旧日志目录里的 log_*.txt 复制到新目录（目标已存在则跳过）。</summary>
        private static void MoveExistingLogs(string fromDir, string toDir)
        {
            if (string.IsNullOrEmpty(fromDir) || string.IsNullOrEmpty(toDir)) return;
            if (string.Equals(fromDir, toDir, StringComparison.OrdinalIgnoreCase)) return;
            try
            {
                if (!Directory.Exists(fromDir)) return;
                Directory.CreateDirectory(toDir);
                foreach (string src in Directory.GetFiles(fromDir, "log_*.txt"))
                {
                    string dest = Path.Combine(toDir, Path.GetFileName(src));
                    if (!File.Exists(dest)) File.Copy(src, dest);
                }
            }
            catch { /* 迁移旧日志失败不影响写日志 */ }
        }

        private static void LoadSettings()
        {
            try
            {
                var app = (App)Microsoft.UI.Xaml.Application.Current;
                if (app?.ShipManager?.Config != null)
                {
                    var config = app.ShipManager.Config;
                    if (config.TryGetValue("log_enabled", out var enabledObj) && enabledObj is bool enabled)
                        _enabled = enabled;
                    if (config.TryGetValue("log_retention_days", out var daysObj) && daysObj is int days)
                        _maxRetentionDays = days;
                }
            }
            catch { }
        }

        public static void SetSettings(bool enabled, int retentionDays)
        {
            _enabled = enabled;
            _maxRetentionDays = retentionDays;
            var app = (App)Microsoft.UI.Xaml.Application.Current;
            if (app?.ShipManager?.Config != null)
            {
                app.ShipManager.Config["log_enabled"] = enabled;
                app.ShipManager.Config["log_retention_days"] = retentionDays;
                app.ShipManager.SaveConfig();
            }
        }

        public static void Info(string message, string source = "System")
        {
            Initialize();
            WriteLog("INFO", $"[{source}] {message}");
        }

        public static void Warning(string message, string source = "System")
        {
            Initialize();
            WriteLog("WARN", $"[{source}] {message}");
        }

        public static void Error(string message, string source = "System", Exception ex = null)
        {
            Initialize();
            string fullMsg = $"[{source}] {message}";
            if (ex != null)
                fullMsg += $"\nException: {ex.Message}\n{ex.StackTrace}";
            WriteLog("ERROR", fullMsg);
        }

        public static void Operation(string operation, string details, string account = null)
        {
            Initialize();
            if (account == null)
            {
                var app = (App)Microsoft.UI.Xaml.Application.Current;
                account = app?.AccountManager?.CurrentAccount ?? "unknown";
            }
            WriteLog("OPER", $"[{account}] {operation}: {details}");
        }

        private static void WriteLog(string level, string content)
        {
            if (!_enabled) return;

            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {content}";
            // 始终写调试输出：文件日志不可用时也不丢日志
            System.Diagnostics.Debug.WriteLine(line);
            if (!_fileLoggingAvailable) return;

            Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_fileLoggingAvailable) return;
                    string today = DateTime.Now.ToString("yyyy-MM-dd");
                    string path = Path.Combine(_logDirectory, $"log_{today}.txt");
                    if (TryAppendLine(path, line))
                    {
                        _logFilePath = path;
                        return;
                    }

                    // 权限被拒（历史文件属主为管理员/只读属性）：清属性后重试一次
                    try
                    {
                        if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
                    }
                    catch { }
                    if (TryAppendLine(path, line))
                    {
                        _logFilePath = path;
                        return;
                    }

                    // 仍失败：停止文件日志（不再尝试 App_Data / %TEMP% 等备用目录，
                    // 受限运行环境下它们同样不可写，重试只会不断抛权限异常）
                    _fileLoggingAvailable = false;
                    System.Diagnostics.Debug.WriteLine($"[LogService] 日志文件不可写，已停止文件日志：{path}");
                }
            });
        }

        /// <summary>追加一行（共享读写打开，避免与其他进程/句柄互斥）。失败返回 false，不抛异常。</summary>
        private static bool TryAppendLine(string path, string line)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                byte[] bytes = Encoding.UTF8.GetBytes(line + Environment.NewLine);
                fs.Write(bytes, 0, bytes.Length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void CleanOldLogs()
        {
            if (!_initialized) return;
            try
            {
                var cutoff = DateTime.Now.AddDays(-_maxRetentionDays);
                var files = Directory.GetFiles(_logDirectory, "log_*.txt");
                foreach (var file in files)
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch { }
        }

        public static async Task<string> ExportLogsAsync(string destinationPath, DateTime? from = null, DateTime? to = null)
        {
            Initialize();
            if (!Directory.Exists(_logDirectory))
                throw new DirectoryNotFoundException("日志目录不存在");

            var allLines = new System.Collections.Generic.List<string>();
            var files = Directory.GetFiles(_logDirectory, "log_*.txt");
            Array.Sort(files);

            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                if (fileName.StartsWith("log_") && fileName.EndsWith(".txt"))
                {
                    string datePart = fileName.Substring(4, 10);
                    if (DateTime.TryParse(datePart, out DateTime fileDate))
                    {
                        if (from.HasValue && fileDate < from.Value) continue;
                        if (to.HasValue && fileDate > to.Value) continue;
                    }
                }
                var lines = await File.ReadAllLinesAsync(file);
                allLines.AddRange(lines);
            }

            await File.WriteAllLinesAsync(destinationPath, allLines, Encoding.UTF8);
            return destinationPath;
        }
    }
}