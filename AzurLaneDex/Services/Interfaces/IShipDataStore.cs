using System.Threading.Tasks;
using AzurLaneDex.Models;

namespace AzurLaneDex.Services.Interfaces
{
    public interface IShipDataStore
    {
        Task<StaticData> LoadStaticAsync();
        Task SaveStaticAsync(StaticData data);
        Task<StateList> LoadStateAsync(string accountName);
        Task SaveStateAsync(string accountName, StateList states);
        string GetUserStatePath(string accountName);
    }
}