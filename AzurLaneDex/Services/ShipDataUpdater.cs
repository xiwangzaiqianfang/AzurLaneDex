using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AzurLaneDex.Models;
using AzurLaneDex.Services.Interfaces;

namespace AzurLaneDex.Services
{
    public class ShipDataUpdater : IShipDataUpdater
    {
        private HttpClient CreateHttpClient(string proxy)
        {
            if (string.IsNullOrEmpty(proxy))
                return new HttpClient();
            var handler = new HttpClientHandler
            {
                Proxy = new System.Net.WebProxy(proxy),
                UseProxy = true
            };
            return new HttpClient(handler);
        }

        public async Task<string> GetRemoteVersionAsync(string url, string proxy = null)
        {
            using var client = CreateHttpClient(proxy);
            var json = await client.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
        }

        public async Task<bool> DownloadAndApplyUpdateAsync(string url, string proxy, Action<StaticData> onDataReceived)
        {
            using var client = CreateHttpClient(proxy);
            var json = await client.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<StaticData>(json);
            if (data?.Ships == null) return false;
            onDataReceived?.Invoke(data);
            return true;
        }
    }
}