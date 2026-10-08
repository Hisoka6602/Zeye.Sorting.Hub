using Microsoft.Extensions.Caching.Memory;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>一分钟有界只读量测缓存，热命中和阈值切换不重新读取数据库。</summary>
public sealed class ParcelDwsConsistencyCache : IDisposable {
    /// <summary>最多缓存20万条窄测量样本。</summary>
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 200000 });
    /// <summary>合并并发冷读，避免报表竞争数据库。</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);
    /// <summary>失败与取消不缓存半成品；显式刷新重新生成。</summary>
    internal async Task<ParcelDwsMeasurementDataset> GetAsync(string key, bool refresh, Func<Task<ParcelDwsMeasurementDataset>> read, CancellationToken token) {
        if (!refresh && _cache.TryGetValue<ParcelDwsMeasurementDataset>(key, out var hit)) return hit!;
        await _gate.WaitAsync(token);
        try {
            if (!refresh && _cache.TryGetValue<ParcelDwsMeasurementDataset>(key, out hit)) return hit!;
            var data = await read();
            _cache.Set(key, data, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1), Size = Math.Max(1, data.Samples.Count) });
            return data;
        } finally { _gate.Release(); }
    }
    /// <summary>释放当前缓存与并发门。</summary>
    public void Dispose() { _cache.Dispose(); _gate.Dispose(); }
}
