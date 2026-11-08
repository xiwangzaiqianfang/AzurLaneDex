using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// 文件系统能力探测：用 Win32 句柄检查「能否在目录下创建文件/子目录」，
    /// **不抛异常**（受限环境、权限不足、目录不存在都只是返回 false），
    /// 避免在调试器里产生 first-chance 的 UnauthorizedAccessException。
    /// </summary>
    internal static class FileAccessProbe
    {
        private const uint FILE_LIST_DIRECTORY = 0x0001;
        private const uint FILE_ADD_FILE = 0x0002;
        private const uint FILE_ADD_SUBDIRECTORY = 0x0004;
        private const uint FILE_TRAVERSE = 0x0020;
        private const uint FILE_READ_ATTRIBUTES = 0x0080;

        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint FILE_SHARE_DELETE = 0x00000004;

        private const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>是否可在该目录（或其最近的已存在祖先）下创建文件/子目录。</summary>
        public static bool CanCreateIn(string? directory)
        {
            if (string.IsNullOrEmpty(directory)) return false;
            try
            {
                string? existing = FindNearestExistingDirectory(directory);
                if (existing == null) return false;

                uint access = FILE_LIST_DIRECTORY | FILE_ADD_FILE | FILE_ADD_SUBDIRECTORY | FILE_TRAVERSE | FILE_READ_ATTRIBUTES;
                uint share = FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE;
                IntPtr handle = CreateFileW(existing, access, share, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
                if (handle == new IntPtr(-1)) return false;
                CloseHandle(handle);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>是否可写入该文件（其所在目录可创建/改文件即可；已存在的只读文件单独判断）。</summary>
        public static bool CanWriteFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            try
            {
                if (File.Exists(filePath))
                {
                    var attrs = File.GetAttributes(filePath);
                    if ((attrs & FileAttributes.ReadOnly) != 0) return false;
                }
                return CanCreateIn(Path.GetDirectoryName(filePath));
            }
            catch
            {
                return false;
            }
        }

        private static string? FindNearestExistingDirectory(string directory)
        {
            string? current;
            try
            {
                current = Path.GetFullPath(directory);
            }
            catch
            {
                return null;
            }

            while (!string.IsNullOrEmpty(current))
            {
                try
                {
                    if (Directory.Exists(current)) return current;
                }
                catch
                {
                    // 无权访问该层：继续向上找
                }
                current = Path.GetDirectoryName(current);
            }
            return null;
        }
    }
}
