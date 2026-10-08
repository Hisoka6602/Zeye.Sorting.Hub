namespace Zeye.Sorting.Hub.Contracts.Models.Diagnostics;

/// <summary>显示采集覆盖范围、有效窗口和可见丢失状态，空列表不能代表没有延迟。</summary>
public sealed record SlowQueryCollectionStatusResponse {
    /// <summary>采集开关。</summary>
    public bool Enabled { get; init; }
    /// <summary>正常调用的慢阈值，单位毫秒，错误和取消不受该阈值限制。</summary>
    public int ThresholdMilliseconds { get; init; }
    /// <summary>内存有效窗口，单位分钟。</summary>
    public int WindowMinutes { get; init; }
    /// <summary>被容量上限淘汰的样本数量。</summary>
    public long CapacityEvictions { get; init; }
    /// <summary>已经过期的样本数量。</summary>
    public long ExpiredSamples { get; init; }
    /// <summary>采集器内部失败数量。</summary>
    public long CollectionFailures { get; init; }
    /// <summary>尚未完成的操作数量。</summary>
    public int ActiveOperations { get; init; }
    /// <summary>最久活动操作耗时，单位毫秒。</summary>
    public decimal OldestActiveMilliseconds { get; init; }
    /// <summary>最久活动操作的请求追踪标识。</summary>
    public string OldestActiveTraceId { get; init; } = "";
    /// <summary>后台历史归档开关。</summary>
    public bool ArchiveEnabled { get; init; }
    /// <summary>归档是否初始化完成并可写。</summary>
    public bool ArchiveReady { get; init; }
    /// <summary>等待后台批量归档的样本数量。</summary>
    public long ArchivePending { get; init; }
    /// <summary>归档失败或背压丢失的样本数量。</summary>
    public long ArchiveDropped { get; init; }
    /// <summary>本进程启动后已恢复的样本数量。</summary>
    public long RestoredSamples { get; init; }
}
