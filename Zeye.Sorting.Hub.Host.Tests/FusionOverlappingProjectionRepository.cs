using System.Collections.Concurrent;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>测试专属可控等待仓储；同票重叠立即失败，二十组均进入后才放行第一轮。</summary>
internal sealed class FusionOverlappingProjectionRepository : IParcelProcessingRepository {
    /// <summary>检测同一包裹的并发重入。</summary>
    private readonly ConcurrentDictionary<long, int> _activeByParcel = new();
    /// <summary>分别记录每票的调用阶段。</summary>
    private readonly ConcurrentDictionary<long, int> _calls = new();
    /// <summary>模拟并行数据库IO完成前的等待。</summary>
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>当前活动提交总数。</summary>
    private int _active;
    /// <summary>已经进入第一阶段的独立包裹数。</summary>
    private int _first;
    /// <summary>实际观察到的并发提交峰值。</summary>
    private int _peak;
    /// <summary>供测试断言并发确实超过原八组上限。</summary>
    public int Peak => _peak;
    /// <summary>供测试断言每票完整阶段顺序。</summary>
    public ConcurrentQueue<(long Parcel, ParcelProcessingStage Stage)> Writes { get; } = new();

    /// <summary>可控模拟提交；完成前不返回成功，每票活动提交始终只能为一。</summary>
    public async Task<RepositoryResult<ParcelProcessingWriteResult>> AppendAsync(ParcelProcessingRecord record, CancellationToken token) {
        var id = record.SourceParcelId!.Value;
        Assert.Equal(1, _activeByParcel.AddOrUpdate(id, 1, (_, count) => count + 1));
        var active = Interlocked.Increment(ref _active);
        int previous;
        do { previous = _peak; if (previous >= active) break; }
        while (Interlocked.CompareExchange(ref _peak, active, previous) != previous);
        try {
            if (_calls.AddOrUpdate(id, 1, (_, count) => count + 1) == 1) {
                if (Interlocked.Increment(ref _first) == 20) _release.TrySetResult();
                await _release.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
            }
            await Task.Yield();
            Writes.Enqueue((id, record.Stage));
            return RepositoryResult.Success<ParcelProcessingWriteResult>(new() { ParcelId = id, PartitionSuffix = "" });
        }
        finally { Interlocked.Decrement(ref _active); _activeByParcel.AddOrUpdate(id, 0, (_, count) => count - 1); }
    }
    /// <summary>此测试不包含未绑定历史读取。</summary>
    public Task<IReadOnlyList<ParcelProcessingRecord>> GetUnboundAsync(int limit, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<ParcelProcessingRecord>>([]);
}
