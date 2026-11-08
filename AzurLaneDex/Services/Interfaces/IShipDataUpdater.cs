using System;
using System.Threading.Tasks;

namespace AzurLaneDex.Services.Interfaces
{
    /// <summary>
    /// 静态数据库（ships_static.db）远端信息：大小 + 最后修改时间，用于判断是否需要更新。
    /// </summary>
    public sealed class RemoteDataInfo
    {
        public long Size { get; init; }
        public DateTimeOffset? LastModified { get; init; }

        /// <summary>供界面显示的版本文本（如 "2026-10-24 18:22 ｜ 28.1 MB"）。</summary>
        public string Display =>
            (LastModified?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "未知时间") +
            " ｜ " + (Size / 1024.0 / 1024.0).ToString("0.0") + " MB";
    }

    /// <summary>
    /// 静态数据库更新器：只读远端文件头/长度，并把下载到的 .db 校验后替换到数据目录。
    /// </summary>
    public interface IShipDataUpdater
    {
        /// <summary>读取远端数据库的大小与最后修改时间（失败返回 null）。</summary>
        Task<RemoteDataInfo?> GetRemoteDataInfoAsync(string url, string proxy = "");

        /// <summary>
        /// 下载远端 .db → 校验 SQLite 文件头 → 备份现有库 → 原子替换数据目录中的 ships_static.db
        /// → 执行 <paramref name="onApplied"/>（通常是重新装载数据）。成功返回 true。
        /// </summary>
        Task<bool> DownloadAndApplyDatabaseAsync(string url, string proxy, Func<Task> onApplied);
    }
}
