namespace Zeye.Sorting.Hub.Application.Services.WriteBuffers;

/// <summary>
/// 批量缓冲写入结果。
/// </summary>
public sealed record BufferedWriteResult {
    /// <summary>
    /// 成功入队数量。
    /// </summary>
    public required int AcceptedCount { get; init; }

    /// <summary>
    /// 被拒绝数量。
    /// </summary>
    public required int RejectedCount { get; init; }

    /// <summary>
    /// 当前队列深度。
    /// </summary>
    public required int QueueDepth { get; init; }

    /// <summary>
    /// 是否触发背压拒绝。
    /// </summary>
    public required bool IsBackpressureTriggered { get; init; }

    /// <summary>
    /// 结果消息。
    /// </summary>
    public required string Message { get; init; }
}
