using System;
using System.Threading.Tasks;
using AzurLaneDex.Models;

namespace AzurLaneDex.Services.Interfaces
{
    public interface IUpdateService
    {
        UpdateChannel CurrentChannel { get; set; }
        UpdateSource CurrentSource { get; set; }
        string CurrentAppVersion { get; }
        Task<UpdateInfo> CheckForUpdateAsync();
        Task<bool> DownloadAndInstallUpdateAsync(UpdateInfo update, IProgress<double> progress);
        Task<string> GetRemoteDataVersionAsync(string url, string proxy = "");
        Task<bool> UpdateDataFromUrlAsync(string url, string proxy = "");
    }

    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string LatestVersion { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public UpdateChannel Channel { get; set; }
        public string Error { get; set; } = "";
    }
}