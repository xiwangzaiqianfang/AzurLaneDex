using System.Text.Json;
using System.Threading.Tasks;
using AzurLaneDex.Models;

namespace AzurLaneDex.Services.Interfaces
{
    public interface IShipMigrator
    {
        bool IsOldFormat(string jsonContent);
        Task<bool> MigrateAsync();
        ShipStatic MigrateSingleShip(JsonElement old);
    }
}