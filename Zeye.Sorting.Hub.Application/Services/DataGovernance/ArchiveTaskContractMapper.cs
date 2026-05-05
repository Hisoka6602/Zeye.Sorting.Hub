using Zeye.Sorting.Hub.Contracts.Models.DataGovernance;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;

namespace Zeye.Sorting.Hub.Application.Services.DataGovernance;

/// <summary>
/// 归档任务合同映射器。
/// </summary>
internal static class ArchiveTaskContractMapper {
    /// <summary>
    /// 将领域聚合映射为响应合同。
    /// </summary>
    /// <param name="archiveTask">归档任务聚合。</param>
    /// <returns>响应合同。</returns>
    internal static ArchiveTaskResponse ToResponse(ArchiveTask archiveTask) {
        return new ArchiveTaskResponse {
            Id = archiveTask.Id,
            TaskType = archiveTask.TaskType.ToString(),
            Status = archiveTask.Status.ToString(),
            IsDryRun = archiveTask.IsDryRun,
            RetentionDays = archiveTask.RetentionDays,
            PlannedItemCount = archiveTask.PlannedItemCount,
            ProcessedItemCount = archiveTask.ProcessedItemCount,
            RequestedBy = archiveTask.RequestedBy,
            Remark = archiveTask.Remark,
            PlanSummary = archiveTask.PlanSummary,
            CheckpointPayload = archiveTask.CheckpointPayload,
            FailureMessage = archiveTask.FailureMessage,
            RetryCount = archiveTask.RetryCount,
            CreatedAt = archiveTask.CreatedAt,
            UpdatedAt = archiveTask.UpdatedAt,
            LastAttemptedAt = archiveTask.LastAttemptedAt,
            CompletedAt = archiveTask.CompletedAt
        };
    }
}
