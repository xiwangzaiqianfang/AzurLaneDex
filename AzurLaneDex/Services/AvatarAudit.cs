using AzurLaneDex.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// 头像文件体检（只读诊断）：装载完成后检查每艘船的头像文件是否存在于
    /// 本地头像包（数据目录 avatars/）或包内资源（Assets/icon/square/）。
    /// 解析规则与显示一致：**以资源实际目录为准**——先按阵营目录找（快路径），
    /// 未命中再按文件名在 icon/square 全树定位，所以库里 faction_id 与资源目录不符也能出图。
    /// <list type="bullet">
    /// <item>完全找不到 → 「头像缺失」（WARN，逐条 + 汇总，含候选文件名）；</item>
    /// <item>只在别处目录找到 → 「按资源实际目录命中」（INFO，仅汇总 + 逐条，用于了解数据差异，不影响显示）。</item>
    /// </list>
    /// 不改变任何显示逻辑。
    /// </summary>
    internal static class AvatarAudit
    {
        /// <summary>
        /// 遍历全部舰船：缺失者告警，按实际目录兜底命中者记 INFO；返回缺失数量。
        /// </summary>
        internal static int Run(IEnumerable<ShipViewModel> ships)
        {
            int total = 0;
            int missing = 0;
            int byActualFolder = 0;
            var missingIds = new List<int>();
            var fallbackIds = new List<int>();

            foreach (var ship in ships)
            {
                total++;
                if (ship.IsAvatarDownloaded) continue;          // 本地头像包已下载到 avatars/

                string? rel = AvatarFiles.Resolve(ship, out string expectedFolder, out string? actualFolder);
                if (rel == null)
                {
                    missing++;
                    missingIds.Add(ship.Id);
                    LogService.Warning(
                        "头像缺失：ID " + ship.Id +
                        "｜global " + ship.RawName +
                        "｜名称 " + ship.DisplayName +
                        "｜目录 " + expectedFolder +
                        "｜候选 " + string.Join(" / ", ship.AvatarFileBases.Select(b => b + ".png")),
                        nameof(AvatarAudit));
                    continue;
                }

                if (!string.IsNullOrEmpty(actualFolder) &&
                    !string.Equals(expectedFolder, actualFolder, System.StringComparison.OrdinalIgnoreCase))
                {
                    byActualFolder++;
                    fallbackIds.Add(ship.Id);
                    LogService.Info(
                        "头像按资源实际目录命中：ID " + ship.Id +
                        "｜global " + ship.RawName +
                        "｜名称 " + ship.DisplayName +
                        "｜库内阵营目录 " + expectedFolder +
                        "｜实际目录 " + actualFolder +
                        "｜文件 " + rel,
                        nameof(AvatarAudit));
                }
            }

            if (missing > 0)
            {
                LogService.Warning(
                    "头像体检结果：" + missing + " / " + total + " 艘找不到头像文件；缺失 ID："
                    + string.Join(", ", missingIds),
                    nameof(AvatarAudit));
            }
            else
            {
                LogService.Info("头像体检结果：" + total + " 艘全部命中（本地头像包或包内资源）", nameof(AvatarAudit));
            }

            if (byActualFolder > 0)
            {
                LogService.Info(
                    "头像目录：其中 " + byActualFolder + " 艘按资源实际目录命中（与库内阵营目录不同，已正常出图）："
                    + string.Join(", ", fallbackIds),
                    nameof(AvatarAudit));
            }
            return missing;
        }
    }
}
