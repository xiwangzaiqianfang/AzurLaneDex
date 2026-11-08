using AzurLaneDex.Models;
using System.Collections.Generic;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// 【保留壳】原 JSON 目录装载器已退役（数据源改为 SQLite），仅留 <see cref="LoadResult"/> 供 SQLite 装载器复用。
    /// 若要恢复离线 JSON 工具：从 _backup 取回旧实现替换本文件，并保持 <see cref="LoadResult"/> 成员不变。
    /// </summary>
    public static class DataSourceLoader
    {
        /// <summary>一次静态数据装载的结果容器（SQLite 装载器同样使用该容器）。</summary>
        public sealed class LoadResult
        {
            /// <summary>转换后的舰船目录。</summary>
            public List<ShipStatic> Ships { get; set; } = new();

            /// <summary>数据版本（形如 yyyyMMdd_HHmmss）。</summary>
            public string DataVersion { get; set; } = "";

            /// <summary>数据源是否可用（false = 调用方应走缓存/空目录）。</summary>
            public bool SourceAvailable { get; set; }

            /// <summary>转换过程警告（不阻断）。</summary>
            public List<string> Warnings { get; } = new();
        }
    }
}
