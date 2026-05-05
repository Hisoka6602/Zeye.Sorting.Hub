namespace Zeye.Sorting.Hub.Contracts.Models.DataGovernance;

/// <summary>
/// 归档任务响应合同。
/// </summary>
public sealed record ArchiveTaskResponse {
    /// <summary>
    /// 任务主键。
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// 任务类型。
    /// </summary>
    public required string TaskType { get; init; }

    /// <summary>
    /// 当前状态。
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// 是否为 dry-run。
    /// </summary>
    public bool IsDryRun { get; init; }

    /// <summary>
    /// 保留天数。
    /// </summary>
    public int RetentionDays { get; init; }

    /// <summary>
    /// 计划候选数量。
    /// </summary>
    public long PlannedItemCount { get; init; }

    /// <summary>
    /// 已处理数量。
    /// </summary>
    public long ProcessedItemCount { get; init; }

    /// <summary>
    /// 发起人。
    /// </summary>
    public required string RequestedBy { get; init; }

    /// <summary>
    /// 备注。
    /// </summary>
    public string Remark { get; init; } = string.Empty;

    /// <summary>
    /// 计划摘要。
    /// </summary>
    public string PlanSummary { get; init; } = string.Empty;

    /// <summary>
    /// 检查点载荷。
    /// </summary>
    public string CheckpointPayload { get; init; } = string.Empty;

    /// <summary>
    /// 最近一次失败信息。
    /// </summary>
    public string FailureMessage { get; init; } = string.Empty;

    /// <summary>
    /// 已重试次数。
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// 创建时间（本地时间语义）。
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// 更新时间（本地时间语义）。
    /// </summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>
    /// 最近执行时间（本地时间语义）。
    /// </summary>
    public DateTime? LastAttemptedAt { get; init; }

    /// <summary>
    /// 完成时间（本地时间语义）。
    /// </summary>
    public DateTime? CompletedAt { get; init; }
}
