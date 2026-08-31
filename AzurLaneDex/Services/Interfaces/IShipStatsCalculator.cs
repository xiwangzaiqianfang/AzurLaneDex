using AzurLaneDex.Models;
using AzurLaneDex.ViewModels;
using System.Collections.Generic;

namespace AzurLaneDex.Services.Interfaces
{
    public interface IShipStatsCalculator
    {
        Dictionary<string, CampTechData> CalculateCampTech(IEnumerable<ShipViewModel> ships);
        int GetTotalTechPoints(IEnumerable<ShipViewModel> ships);
        int GetOwnedTechPoints(IEnumerable<ShipViewModel> ships);
        StatsData CalculateStats(IEnumerable<ShipViewModel> ships);
        Dictionary<(ShipType ShipType, AttributeType Attr), int> CalculateGlobalBonuses(IEnumerable<ShipViewModel> ships);
    }
}