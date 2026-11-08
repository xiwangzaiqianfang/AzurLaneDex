using System.Threading.Tasks;
using AzurLaneDex.Models;

namespace AzurLaneDex.Services.Interfaces
{
    public interface IShipDataStore
    {
        /// <summary>加载静态目录缓存（static/ships_static.json，新模型格式）。</summary>
        Task<StaticData> LoadStaticAsync();
        /// <summary>保存静态目录缓存。</summary>
        Task SaveStaticAsync(StaticData data);
        /// <summary>
        /// 离线缓存是否已与当前数据源一致（数据版本 + 舰船数）。
        /// 一致时应跳过 <see cref="SaveStaticAsync"/>，避免每次启动重写约 50MB 的 JSON。
        /// </summary>
        Task<bool> IsStaticCacheCurrentAsync(string dataVersion, int shipCount);
        Task<StateList> LoadStateAsync(string accountName);
        Task SaveStateAsync(string accountName, StateList states);
        /// <summary>加载账户配队列表（users/{account}/teams.json；缺失/损坏返回空表）。</summary>
        Task<TeamList> LoadTeamsAsync(string accountName);
        /// <summary>保存账户配队列表。</summary>
        Task SaveTeamsAsync(string accountName, TeamList teams);
        string GetUserStatePath(string accountName);
    }
}