using Microsoft.Extensions.Caching.Memory;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>有界阶段快照和一分钟接口快照；显式刷新重新读取，不缓存半成品。</summary>
public sealed class ParcelDurationAnalysisCache : IDisposable {
    /// <summary>最多保留25万条轻量耗时样本。</summary>
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 250000 });
    /// <summary>合并并发冷读，热快照无需等待。</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>阶段总体15秒内复用，五类接口共用一分钟快照；失败或取消不会缓存半成品。</summary>
    internal async Task<ParcelDurationDataset> GetAsync(string key, bool refresh, Func<Task<ParcelDurationDataset>> read,
        CancellationToken cancellationToken) {
        if (!refresh && _cache.TryGetValue<ParcelDurationDataset>(key, out var hit)) return hit!;
        await _gate.WaitAsync(cancellationToken);
        try {
            if (!refresh && _cache.TryGetValue<ParcelDurationDataset>(key, out hit)) return hit!;
            var data = await read();
            var size = Math.Max(1, data.Samples.Count);
            if (size <= 250000) _cache.Set(key, data, new MemoryCacheEntryOptions {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(data.ByType is null ? 15 : 60), Size = size
            });
            return data;
        }
        finally { _gate.Release(); }
    }

    /// <summary>释放当前服务的有界缓存与并发门。</summary>
    public void Dispose() { _cache.Dispose(); _gate.Dispose(); }
}
