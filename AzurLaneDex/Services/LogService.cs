using System;
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

        // 确保日志目录存在并返回路径
        private static string EnsureLogDirectory()
        {
            string dataRoot = App.DataRoot;
            if (string.IsNullOrEmpty(dataRoot))
            {
                // 后备：使用 LocalApplicationData
                dataRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AzurLaneDex", "data");
                Directory.CreateDirectory(dataRoot);
            }
            string logDir = Path.Combine(dataRoot, "log");
            Directory.CreateDirectory(logDir);
            return logDir;
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

            // 每日文件切换（线程安全）
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string expectedPath = Path.Combine(_logDirectory, $"log_{today}.txt");
            lock (_lock)
            {
                if (expectedPath != _logFilePath)
                {
                    _logFilePath = expectedPath;
                }
            }

            string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {content}";
            Task.Run(() =>
            {
                try
                {
                    lock (_lock)
                    {
                        File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
                    }
                }
                catch { }
            });
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