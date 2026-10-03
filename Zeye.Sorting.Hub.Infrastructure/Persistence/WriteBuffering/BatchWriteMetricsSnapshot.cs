namespace Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

/// <summary>
/// 批量写入缓冲运行时指标快照。
/// </summary>
public sealed record BatchWriteMetricsSnapshot {
    /// <summary>
    /// 是否启用缓冲写入。
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// 当前队列深度。
    /// </summary>
    public required int QueueDepth { get; init; }

    /// <summary>
    /// 当前死信数量。
    /// </summary>
    public required int DeadLetterCount { get; init; }

    /// <summary>
    /// 通道丢弃计数。
    /// </summary>
    public required long DroppedCount { get; init; }

    /// <summary>死信容量不足造成的实际覆盖数量，与未接收请求的拒绝数量区分。</summary>
    public long DeadLetterDroppedCount { get; init; }

    /// <summary>消费循环是否已启动过。</summary>
    public bool HasWorkerStarted { get; init; }

    /// <summary>消费循环是否仍在运行。</summary>
    public bool IsWorkerRunning { get; init; }

    /// <summary>
    /// 最近一次成功刷新时间（本地时间）。
    /// </summary>
    public DateTime? LastSuccessfulFlushAtLocal { get; init; }

    /// <summary>
    /// 最近一次失败刷新时间（本地时间）。
    /// </summary>
    public DateTime? LastFailedFlushAtLocal { get; init; }

    /// <summary>
    /// 最近一次失败消息。
    /// </summary>
    public string? LastFailureMessage { get; init; }

    /// <summary>
    /// 累计成功刷新批次数。
    /// </summary>
    public required long SuccessfulFlushCount { get; init; }

    /// <summary>
    /// 累计失败刷新批次数。
    /// </summary>
    public required long FailedFlushCount { get; init; }

    /// <summary>
    /// 累计成功落库记录数。
    /// </summary>
    public required long TotalFlushedCount { get; init; }

    /// <summary>
    /// 当前是否触发背压状态。
    /// </summary>
    public required bool IsBackpressureTriggered { get; init; }
}
