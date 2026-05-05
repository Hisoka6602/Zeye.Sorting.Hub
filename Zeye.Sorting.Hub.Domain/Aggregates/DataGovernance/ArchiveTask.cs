using Zeye.Sorting.Hub.Domain.Abstractions;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;

namespace Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;

/// <summary>
/// 数据归档任务聚合根。
/// 当前阶段仅承载 dry-run 计划、状态记录、审计摘要与重试信息。
/// </summary>
public sealed class ArchiveTask : IEntity<long> {
    /// <summary>
    /// 任务主键。
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 任务类型。
    /// </summary>
    public ArchiveTaskType TaskType { get; private set; }

    /// <summary>
    /// 当前任务状态。
    /// </summary>
    public ArchiveTaskStatus Status { get; private set; }

    /// <summary>
    /// 是否仅执行 dry-run。
    /// </summary>
    public bool IsDryRun { get; private set; }

    /// <summary>
    /// 历史数据保留天数。
    /// </summary>
    public int RetentionDays { get; private set; }

    /// <summary>
    /// 计划命中的候选数量。
    /// </summary>
    public int PlannedItemCount { get; private set; }

    /// <summary>
    /// dry-run 执行完成时记录的处理数量。
    /// </summary>
    public int ProcessedItemCount { get; private set; }

    /// <summary>
    /// 请求发起人。
    /// </summary>
    public string RequestedBy { get; private set; } = string.Empty;

    /// <summary>
    /// 任务备注。
    /// </summary>
    public string Remark { get; private set; } = string.Empty;

    /// <summary>
    /// 计划摘要。
    /// </summary>
    public string PlanSummary { get; private set; } = string.Empty;

    /// <summary>
    /// 检查点载荷（JSON）。
    /// </summary>
    public string CheckpointPayload { get; private set; } = string.Empty;

    /// <summary>
    /// 最近一次失败信息。
    /// </summary>
    public string FailureMessage { get; private set; } = string.Empty;

    /// <summary>
    /// 已触发重试次数。
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// 创建时间（本地时间语义）。
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// 最近更新时间（本地时间语义）。
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// 最近一次执行时间（本地时间语义）。
    /// </summary>
    public DateTime? LastAttemptedAt { get; private set; }

    /// <summary>
    /// 执行完成时间（本地时间语义）。
    /// </summary>
    public DateTime? CompletedAt { get; private set; }

    /// <summary>
    /// EF Core 反序列化构造函数。
    /// </summary>
    private ArchiveTask() {
    }

    /// <summary>
    /// 创建新的 dry-run 归档任务。
    /// </summary>
    /// <param name="taskType">任务类型。</param>
    /// <param name="retentionDays">保留天数。</param>
    /// <param name="requestedBy">发起人。</param>
    /// <param name="remark">备注。</param>
    /// <returns>归档任务聚合。</returns>
    public static ArchiveTask CreateDryRun(
        ArchiveTaskType taskType,
        int retentionDays,
        string? requestedBy,
        string? remark) {
        if (retentionDays <= 0) {
            throw new ArgumentOutOfRangeException(nameof(retentionDays), "保留天数必须大于 0。");
        }

        var now = DateTime.Now;
        return new ArchiveTask {
            TaskType = taskType,
            Status = ArchiveTaskStatus.Pending,
            IsDryRun = true,
            RetentionDays = retentionDays,
            RequestedBy = string.IsNullOrWhiteSpace(requestedBy) ? "system" : requestedBy.Trim(),
            Remark = string.IsNullOrWhiteSpace(remark) ? string.Empty : remark.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// 标记任务进入执行中。
    /// </summary>
    public void MarkRunning() {
        Status = ArchiveTaskStatus.Running;
        LastAttemptedAt = DateTime.Now;
        FailureMessage = string.Empty;
        UpdatedAt = LastAttemptedAt.Value;
    }

    /// <summary>
    /// 标记任务执行完成。
    /// </summary>
    /// <param name="plannedItemCount">计划数量。</param>
    /// <param name="planSummary">计划摘要。</param>
    /// <param name="checkpointPayload">检查点 JSON。</param>
    public void MarkCompleted(int plannedItemCount, string planSummary, string checkpointPayload) {
        if (plannedItemCount < 0) {
            throw new ArgumentOutOfRangeException(nameof(plannedItemCount), "计划数量不能为负数。");
        }

        Status = ArchiveTaskStatus.Completed;
        PlannedItemCount = plannedItemCount;
        ProcessedItemCount = plannedItemCount;
        PlanSummary = planSummary?.Trim() ?? string.Empty;
        CheckpointPayload = checkpointPayload?.Trim() ?? string.Empty;
        FailureMessage = string.Empty;
        CompletedAt = DateTime.Now;
        UpdatedAt = CompletedAt.Value;
    }

    /// <summary>
    /// 标记任务执行失败。
    /// </summary>
    /// <param name="failureMessage">失败消息。</param>
    public void MarkFailed(string failureMessage) {
        Status = ArchiveTaskStatus.Failed;
        FailureMessage = string.IsNullOrWhiteSpace(failureMessage) ? "归档任务执行失败。" : failureMessage.Trim();
        CompletedAt = null;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>
    /// 将终态任务重新放回待执行队列。
    /// </summary>
    public void Requeue() {
        if (Status == ArchiveTaskStatus.Running) {
            throw new InvalidOperationException("执行中的任务不可重试。");
        }

        Status = ArchiveTaskStatus.Pending;
        RetryCount++;
        PlannedItemCount = 0;
        ProcessedItemCount = 0;
        PlanSummary = string.Empty;
        CheckpointPayload = string.Empty;
        FailureMessage = string.Empty;
        CompletedAt = null;
        UpdatedAt = DateTime.Now;
    }
}
