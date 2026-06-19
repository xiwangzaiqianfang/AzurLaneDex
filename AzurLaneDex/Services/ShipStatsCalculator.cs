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
                string faction = ship.Faction;
                if (!result.ContainsKey(faction))
                    result[faction] = new CampTechData();
                var data = result[faction];
                data.Obtain += ship.TechPointsObtain;
                if (ship.IsMaxBreakthrough)
                    data.Max += ship.TechPointsMax;
                if (ship.Level120)
                    data.Level120 += ship.TechPoints120;
            }
            return result;
        }

        public int GetTotalTechPoints(IEnumerable<ShipViewModel> ships)
        {
            return ships.Sum(s => s.TechPointsObtain + s.TechPointsMax + s.TechPoints120);
        }

        public int GetOwnedTechPoints(IEnumerable<ShipViewModel> ships)
        {
            return ships.Where(s => s.Owned).Sum(s => s.TechPointsObtain + (s.IsMaxBreakthrough ? s.TechPointsMax : 0) + (s.Level120 ? s.TechPoints120 : 0));
        }

        public StatsData CalculateStats(IEnumerable<ShipViewModel> ships)
        {
            var list = ships.Where(s => s.Category != ShipCategory.Collab).ToList();
            return new StatsData
            {
                Total = list.Count,
                Owned = list.Count(s => s.Owned),
                NotOwned = list.Count - list.Count(s => s.Owned),
                MaxBreakthrough = list.Count(s => s.IsMaxBreakthrough),
                NotMaxBreakthrough = list.Count(s => s.Owned && !s.IsMaxBreakthrough),
                Oath = list.Count(s => s.Oath),
                Remodeled = list.Count(s => s.Remodeled),
                CanRemodelNot = list.Count(s => s.CanRemodel && !s.Remodeled),
                Level120 = list.Count(s => s.Level120),
                SpecialGearObtained = list.Count(s => s.SpecialGearObtained),
                SpecialGearNotObtained = list.Count(s => s.CanSpecialGear && !s.SpecialGearObtained),
                CanRemodelTotal = list.Count(s => s.CanRemodel)
            };
        }

        public Dictionary<(string ShipClass, string Attr), int> CalculateGlobalBonuses(IEnumerable<ShipViewModel> ships)
        {
            var bonuses = new Dictionary<(string, string), int>();
            foreach (var ship in ships.Where(s => s.Owned))
            {
                if (ship.ObtainBonusValue != 0)
                {
                    foreach (var sc in ship.ObtainAffectsDisplay.Split(',').Select(s => s.Trim()))
                    {
                        var key = (sc, ship.ObtainBonusAttr);
                        bonuses[key] = bonuses.GetValueOrDefault(key) + ship.ObtainBonusValue;
                    }
                }
                if (ship.Level120BonusValue != 0)
                {
                    foreach (var sc in ship.Level120AffectsDisplay.Split(',').Select(s => s.Trim()))
                    {
                        var key = (sc, ship.Level120BonusAttr);
                        bonuses[key] = bonuses.GetValueOrDefault(key) + ship.Level120BonusValue;
                    }
                }
            }
            return bonuses;
        }
    }

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