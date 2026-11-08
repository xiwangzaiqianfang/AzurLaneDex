using AzurLaneDex.Models;
using AzurLaneDex.Services.Interfaces;
using AzurLaneDex.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace AzurLaneDex.Services
{
    /// <summary>
    /// 统计计算器：舰队科技点、图鉴统计与全局属性加成计算。
    /// 纯展示计算（无副作用），UI 层只做展示，不参与计算。
    /// </summary>
    public class ShipStatsCalculator : IShipStatsCalculator
    {
        /// <summary>按阵营汇总科技点（收集/满破/120 级），键为本地化阵营名。</summary>
        public Dictionary<string, CampTechData> CalculateCampTech(IEnumerable<ShipViewModel> ships)
        {
            var result = new Dictionary<string, CampTechData>();
            foreach (var ship in ships.Where(s => s.Owned))
            {
                string faction = ship.Faction; // 本地化阵营名
                if (!result.ContainsKey(faction))
                    result[faction] = new CampTechData();

                var ft = ship.FleetTech;
                var data = result[faction];
                data.Obtain += ft?.Collect?.Pts ?? 0;
                if (ship.IsMaxBreakthrough)
                    data.Max += ft?.LimitBreak?.Pts ?? 0;
                if (ship.Level120)
                    data.Level120 += ft?.Level120?.Pts ?? 0;
            }
            return result;
        }

        /// <summary>全部舰船科技点理论总值。</summary>
        public int GetTotalTechPoints(IEnumerable<ShipViewModel> ships)
        {
            return ships.Sum(s =>
            {
                var ft = s.FleetTech;
                return (ft?.Collect?.Pts ?? 0) + (ft?.LimitBreak?.Pts ?? 0) + (ft?.Level120?.Pts ?? 0);
            });
        }

        /// <summary>已获得舰船可取得的科技点。</summary>
        public int GetOwnedTechPoints(IEnumerable<ShipViewModel> ships)
        {
            return ships.Where(s => s.Owned).Sum(s =>
            {
                var ft = s.FleetTech;
                return (ft?.Collect?.Pts ?? 0) +
                       (s.IsMaxBreakthrough ? ft?.LimitBreak?.Pts ?? 0 : 0) +
                       (s.Level120 ? ft?.Level120?.Pts ?? 0 : 0);
            });
        }

        /// <summary>图鉴统计（排除联动）。</summary>
        public StatsData CalculateStats(IEnumerable<ShipViewModel> ships)
        {
            var list = ships.Where(s => s.CategoryEnum != ShipCategory.Collab).ToList();
            return new StatsData
            {
                Total = list.Count,
                Owned = list.Count(s => s.Owned),
                NotOwned = list.Count - list.Count(s => s.Owned),
                MaxBreakthrough = list.Count(s => s.IsMaxBreakthrough),
                NotMaxBreakthrough = list.Count(s => s.Owned && !s.IsMaxBreakthrough),
                Oath = list.Count(s => s.Oath),
                Remodeled = list.Count(s => s.Retrofitted),
                CanRemodelNot = list.Count(s => s.Retrofit?.CanRetrofit == true && !s.Retrofitted),
                Level120 = list.Count(s => s.Level120),
                SpecialGearObtained = list.Count(s => s.SpecialGearObtained),
                SpecialGearNotObtained = list.Count(s => s.CanSpecialGear && !s.SpecialGearObtained),
                CanRemodelTotal = list.Count(s => s.Retrofit?.CanRetrofit == true)
            };
        }

        /// <summary>
        /// 全局属性加成：遍历已拥有舰船，把"获得时/120 级"科技任务加成按目标舰种 × 属性累加。
        /// 返回键为 (舰种, 属性)，值为累计值。
        /// </summary>
        public Dictionary<(ShipType ShipType, AttributeType Attr), int> CalculateGlobalBonuses(IEnumerable<ShipViewModel> ships)
        {
            var bonuses = new Dictionary<(ShipType, AttributeType), int>();

            foreach (var ship in ships.Where(s => s.Owned))
            {
                var ft = ship.FleetTech;
                if (ft == null) continue;
                AddTaskBonus(bonuses, ft.Collect);   // 获得时加成
                AddTaskBonus(bonuses, ft.Level120);  // 120 级加成
            }

            return bonuses;
        }

        /// <summary>把单条科技任务加成（stat/value/hulls）累加到结果字典。</summary>
        private static void AddTaskBonus(
            Dictionary<(ShipType ShipType, AttributeType Attr), int> bonuses,
            TechTaskBonus? task)
        {
            if (task == null || task.Value == 0 || string.IsNullOrEmpty(task.Stat) || task.Hulls.Count == 0)
                return;

            var attr = DataCodeMaps.StatKeyToAttribute(task.Stat);
            if (attr == AttributeType.None) return;

            foreach (int hull in task.Hulls)
            {
                var shipType = DataCodeMaps.HullToShipType(hull);
                if (shipType == null) continue;
                var key = (shipType.Value, attr);
                bonuses[key] = bonuses.GetValueOrDefault(key) + task.Value;
            }
        }
    }

    /// <summary>单阵营科技点统计（收集/满破/120 级）。</summary>
    public class CampTechData
    {
        public int Obtain { get; set; }
        public int Max { get; set; }
        public int Level120 { get; set; }
    }

    /// <summary>图鉴统计结果容器。</summary>
    public class StatsData
    {
        public int Total { get; set; }
        public int Owned { get; set; }
        public int NotOwned { get; set; }
        public int MaxBreakthrough { get; set; }
        public int NotMaxBreakthrough { get; set; }
        public int Oath { get; set; }
        public int Remodeled { get; set; }
        public int CanRemodelNot { get; set; }
        public int Level120 { get; set; }
        public int SpecialGearObtained { get; set; }
        public int SpecialGearNotObtained { get; set; }
        public int CanRemodelTotal { get; set; }
    }
}
