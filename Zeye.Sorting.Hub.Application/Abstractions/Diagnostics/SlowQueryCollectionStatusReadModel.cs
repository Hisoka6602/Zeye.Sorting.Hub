namespace Zeye.Sorting.Hub.Application.Abstractions.Diagnostics;

/// <summary>慢查询采集边界与可见丢失计数，避免空列表被误认为不存在数据库延迟。</summary>
public sealed record SlowQueryCollectionStatusReadModel {
    /// <summary>画像采集是否启用。</summary>
    public bool Enabled { get; init; } = true;
    /// <summary>采集阈值，单位毫秒。</summary>
    public int ThresholdMilliseconds { get; init; }
    /// <summary>当前列表的保留窗口，单位分钟。</summary>
    public int WindowMinutes { get; init; }
    /// <summary>因容量限制淘汰的样本和指纹数量。</summary>
    public long CapacityEvictions { get; init; }
    /// <summary>正常过期的样本数量。</summary>
    public long ExpiredSamples { get; init; }
    /// <summary>诊断发布失败数量；采集故障不改变业务结果。</summary>
    public long CollectionFailures { get; init; }
    /// <summary>正在执行或读取的操作数量。</summary>
    public int ActiveOperations { get; init; }
    /// <summary>最久未完成操作已持续的毫秒数。</summary>
    public decimal OldestActiveMilliseconds { get; init; }
    /// <summary>最久未完成操作的请求追踪标识。</summary>
    public string OldestActiveTraceId { get; init; } = "";
    /// <summary>历史归档是否启用。</summary>
    public bool ArchiveEnabled { get; init; }
    /// <summary>历史归档是否已恢复并可写。</summary>
    public bool ArchiveReady { get; init; }
    /// <summary>归档待写样本数量。</summary>
    public long ArchivePending { get; init; }
    /// <summary>归档背压或写入失败导致的丢失数量。</summary>
    public long ArchiveDropped { get; init; }
    /// <summary>历史归档恢复的样本数量。</summary>
    public long RestoredSamples { get; init; }
}
