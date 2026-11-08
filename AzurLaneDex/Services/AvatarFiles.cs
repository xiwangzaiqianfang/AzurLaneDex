using AzurLaneDex.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// 包内头像文件定位：**以资源实际目录为准**。
    /// 先按「类型/阵营（联动为作品）」目录查（快路径），未命中再按文件名在 icon/square 全树定位；
    /// 全树定位时优先同一分类根目录（Common / Collab / META），避免同名文件跨分类误配。
    /// 供列表绑定（ShipViewModel.AvatarUri）与头像体检（AvatarAudit）共用。
    /// </summary>
    internal static class AvatarFiles
    {
        private static readonly string[] Extensions = { ".png", ".jpg" };

        private static Dictionary<string, List<string>>? _stemIndex;   // 文件名（无扩展名）→ 相对路径（含扩展名）列表
        private static readonly object _indexLock = new();

        /// <summary>包内头像根目录（打包/非打包都是程序目录下的 Assets/icon/square）。</summary>
        internal static string PackageSquareiconDir =>
            Path.Combine(AppContext.BaseDirectory, "Assets", "icon", "square");

        /// <summary>指定目录下是否存在该文件名（png/jpg 任一），存在则返回带扩展名的相对路径。</summary>
        internal static string? ExistsIn(string folderRel, string baseName)
        {
            if (string.IsNullOrEmpty(folderRel) || string.IsNullOrEmpty(baseName)) return null;
            string dir = Path.Combine(PackageSquareiconDir, folderRel.Replace('/', Path.DirectorySeparatorChar));
            foreach (string ext in Extensions)
            {
                try
                {
                    if (File.Exists(Path.Combine(dir, baseName + ext)))
                        return folderRel + "/" + baseName + ext;
                }
                catch { /* 非法路径忽略 */ }
            }
            return null;
        }

        /// <summary>按候选顺序解析包内头像（返回含扩展名的相对路径），全部缺失返回 null。</summary>
        internal static string? ResolvePackageImageRel(ShipViewModel ship) => Resolve(ship, out _, out _);

        /// <summary>按「指定目录 + 指定候选文件名」解析（配队槽位可用自定义改造形态的候选）。</summary>
        internal static string? ResolvePackageImageRel(string folderRel, IReadOnlyList<string> candidates)
            => ResolveCore(folderRel, candidates, out _, out _);

        /// <summary>
        /// 解析包内头像并给出诊断：<paramref name="expectedFolder"/> 为按阵营算出的目录，
        /// <paramref name="actualFolder"/> 为实际命中的目录；两者不同说明库内阵营与资源目录不一致，
        /// 但**显示以实际目录为准**（照常出图），差异只写进日志。
        /// </summary>
        internal static string? Resolve(ShipViewModel ship, out string expectedFolder, out string? actualFolder)
            => ResolveCore(ship.AvatarFolderRel, ship.AvatarFileNameCandidates, out expectedFolder, out actualFolder);

        private static string? ResolveCore(string folderRel, IReadOnlyList<string> candidates,
            out string expectedFolder, out string? actualFolder)
        {
            expectedFolder = folderRel;
            actualFolder = null;

            // 1) 快路径：按类型/阵营（联动为作品）目录
            foreach (string fileName in candidates)
            {
                string? hit = ExistsIn(expectedFolder, StripExtension(fileName));
                if (hit != null)
                {
                    actualFolder = expectedFolder;
                    return hit;
                }
            }

            // 2) 以资源实际目录为准：按文件名全树定位，优先同一分类根目录（Common / Collab / META）
            var index = StemIndex;
            if (index.Count == 0) return null;

            string expectedRoot = RootSegment(expectedFolder);
            string? anyMatch = null;
            foreach (string fileName in candidates)
            {
                if (!index.TryGetValue(StripExtension(fileName), out var rels)) continue;
                foreach (string rel in rels)
                {
                    if (anyMatch == null) anyMatch = rel;
                    if (!string.IsNullOrEmpty(expectedRoot) &&
                        rel.StartsWith(expectedRoot + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        actualFolder = FolderOf(rel);
                        return rel;
                    }
                }
            }

            if (anyMatch != null) actualFolder = FolderOf(anyMatch);
            return anyMatch;
        }

        /// <summary>文件名 → 相对路径（含扩展名）索引；首次使用时建立，之后复用。</summary>
        private static Dictionary<string, List<string>> StemIndex
        {
            get
            {
                if (_stemIndex != null) return _stemIndex;
                lock (_indexLock)
                {
                    if (_stemIndex != null) return _stemIndex;

                    var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                    try
                    {
                        string root = PackageSquareiconDir;
                        if (Directory.Exists(root))
                        {
                            foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
                            {
                                string ext = Path.GetExtension(file);
                                if (!ext.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                                    !ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)) continue;

                                string dir = Path.GetDirectoryName(file) ?? root;
                                string rel = Path.GetRelativePath(root, dir).Replace('\\', '/') + "/" + Path.GetFileName(file);
                                string stem = Path.GetFileNameWithoutExtension(file);
                                if (!map.TryGetValue(stem, out var list)) map[stem] = list = new List<string>();
                                if (!list.Contains(rel)) list.Add(rel);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AvatarFiles] 建立头像索引失败：{ex.Message}");
                    }
                    _stemIndex = map;
                    return map;
                }
            }
        }

        private static string RootSegment(string folderRel)
        {
            if (string.IsNullOrEmpty(folderRel)) return "";
            int slash = folderRel.IndexOf('/');
            return slash > 0 ? folderRel.Substring(0, slash) : folderRel;
        }

        private static string FolderOf(string rel)
        {
            int slash = rel.LastIndexOf('/');
            return slash > 0 ? rel.Substring(0, slash) : "";
        }

        private static string StripExtension(string fileName) =>
            fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                ? fileName.Substring(0, fileName.Length - 4)
                : fileName;
    }
}
