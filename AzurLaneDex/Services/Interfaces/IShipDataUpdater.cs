using System;
using System.Threading.Tasks;
using AzurLaneDex.Models;

namespace AzurLaneDex.Services.Interfaces
{
    public interface IShipDataUpdater
    {
        Task<string> GetRemoteVersionAsync(string url, string proxy = null);
        Task<bool> DownloadAndApplyUpdateAsync(string url, string proxy, Action<StaticData> onDataReceived);
    }
}