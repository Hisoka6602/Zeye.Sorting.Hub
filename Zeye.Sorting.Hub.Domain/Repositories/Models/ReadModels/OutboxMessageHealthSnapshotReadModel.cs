namespace Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;

/// <summary>
/// Outbox 健康快照读模型。
/// </summary>
public sealed record OutboxMessageHealthSnapshotReadModel {
    /// <summary>
    /// 待处理数量。
    /// </summary>
    public required long PendingCount { get; init; }

    /// <summary>
    /// 处理中数量。
    /// </summary>
    public required long ProcessingCount { get; init; }

    /// <summary>
    /// 可重试失败数量。
    /// </summary>
    public required long FailedCount { get; init; }

    /// <summary>
    /// 死信数量。
    /// </summary>
    public required long DeadLetteredCount { get; init; }

    /// <summary>
    /// 最早活动消息创建时间（本地时间语义）。
    /// </summary>
    public required DateTime? OldestActiveCreatedAt { get; init; }
}
