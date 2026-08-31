using AzurLaneDex.Models;
using AzurLaneDex.Services.Interfaces;
using AzurLaneDex.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace AzurLaneDex.Services
{
    public class ShipStatsCalculator : IShipStatsCalculator
    {
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
                data.Obtain += ft.CollectPoints;
                if (ship.IsMaxBreakthrough)
                    data.Max += ft.LimitBreakPoints;
                if (ship.Level120)
                    data.Level120 += ft.Level120Points;
            }
            return result;
        }

        public int GetTotalTechPoints(IEnumerable<ShipViewModel> ships)
        {
            return ships.Sum(s => s.FleetTech.CollectPoints + s.FleetTech.LimitBreakPoints + s.FleetTech.Level120Points);
        }

        public int GetOwnedTechPoints(IEnumerable<ShipViewModel> ships)
        {
            return ships.Where(s => s.Owned).Sum(s =>
                s.FleetTech.CollectPoints +
                (s.IsMaxBreakthrough ? s.FleetTech.LimitBreakPoints : 0) +
                (s.Level120 ? s.FleetTech.Level120Points : 0));
        }

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
                CanRemodelNot = list.Count(s => s.Retrofit.CanRetrofit && !s.Retrofitted),
                Level120 = list.Count(s => s.Level120),
                SpecialGearObtained = list.Count(s => s.SpecialGearObtained),
                SpecialGearNotObtained = list.Count(s => s.CanSpecialGear && !s.SpecialGearObtained),
                CanRemodelTotal = list.Count(s => s.Retrofit.CanRetrofit)
            };
        }

        public Dictionary<(ShipType ShipType, AttributeType Attr), int> CalculateGlobalBonuses(IEnumerable<ShipViewModel> ships)
        {
            var bonuses = new Dictionary<(ShipType, AttributeType), int>();

            foreach (var ship in ships.Where(s => s.Owned))
            {
                // 获得时加成
                var obtain = ship.ObtainBonus;
                if (obtain.TargetTypes.Any())
                {
                    AddBonus(bonuses, obtain.TargetTypes, obtain);
                }

                // 120级加成
                var level120 = ship.Level120Bonus;
                if (level120.TargetTypes.Any())
                {
                    AddBonus(bonuses, level120.TargetTypes, level120);
                }
            }

            return bonuses;
        }
        private void AddBonus(
            Dictionary<(ShipType ShipType, AttributeType Attr), int> bonuses,
            List<ShipType> targetTypes,
            TechBonusDetail bonus)
        {
            var attrValues = new Dictionary<AttributeType, int>
            {
                [AttributeType.HP] = bonus.Hp,
                [AttributeType.FP] = bonus.Fp,
                [AttributeType.TRP] = bonus.Trp,
                [AttributeType.AVI] = bonus.Avi,
                [AttributeType.AA] = bonus.Aa,
                [AttributeType.ACC] = bonus.Hit,
                [AttributeType.EVA] = bonus.Eva,
                [AttributeType.ASW] = bonus.Asw
            };

            foreach (var shipType in targetTypes)
            {
                foreach (var kv in attrValues)
                {
                    if (kv.Value == 0) continue;
                    var key = (shipType, kv.Key);
                    bonuses[key] = bonuses.GetValueOrDefault(key) + kv.Value;
                }
            }
        }
    }

    // 数据容器类（已在旧代码中定义，但为确保完整，保留）
    public class CampTechData
    {
        public int Obtain { get; set; }
        public int Max { get; set; }
        public int Level120 { get; set; }
    }

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