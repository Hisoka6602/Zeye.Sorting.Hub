using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

/// <summary>
/// 数据保留治理审计记录。
/// </summary>
public sealed record class DataRetentionAuditRecord {
    /// <summary>
    /// Disabled 状态文本。
    /// </summary>
    public const string DisabledStatus = "Disabled";

    /// <summary>
    /// NoPolicies 状态文本。
    /// </summary>
    public const string NoPoliciesStatus = "NoPolicies";

    /// <summary>
    /// Completed 状态文本。
    /// </summary>
    public const string CompletedStatus = "Completed";

    /// <summary>
    /// Failed 状态文本。
    /// </summary>
    public const string FailedStatus = "Failed";

    /// <summary>
    /// 记录时间（本地时间）。
    /// </summary>
    public required DateTime RecordedAtLocal { get; init; }

    /// <summary>
    /// 当前状态。
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// 是否启用数据保留治理。
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// 当前执行决策。
    /// </summary>
    public ActionIsolationDecision? Decision { get; init; }

    /// <summary>
    /// 是否为 dry-run。
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// 策略总数。
    /// </summary>
    public required int PolicyCount { get; init; }

    /// <summary>
    /// 计划处理总数。
    /// </summary>
    public required int PlannedCount { get; init; }

    /// <summary>
    /// 实际处理总数。
    /// </summary>
    public required int ExecutedCount { get; init; }

    /// <summary>
    /// 失败策略数。
    /// </summary>
    public required int FailedPolicyCount { get; init; }

    /// <summary>
    /// 摘要说明。
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// 分策略摘要。
    /// </summary>
    public required IReadOnlyList<string> PolicySummaries { get; init; }

    /// <summary>
    /// 创建“未启用”记录。
    /// </summary>
    /// <returns>审计记录。</returns>
    public static DataRetentionAuditRecord CreateDisabled() {
        return new DataRetentionAuditRecord {
            RecordedAtLocal = DateTime.Now,
            Status = DisabledStatus,
            IsEnabled = false,
            IsDryRun = false,
            PolicyCount = 0,
            PlannedCount = 0,
            ExecutedCount = 0,
            FailedPolicyCount = 0,
            Summary = "数据保留治理未启用。",
            PolicySummaries = []
        };
    }

    /// <summary>
    /// 创建“未配置策略”记录。
    /// </summary>
    /// <param name="isDryRun">是否为 dry-run。</param>
    /// <returns>审计记录。</returns>
    public static DataRetentionAuditRecord CreateNoPolicies(bool isDryRun) {
        return new DataRetentionAuditRecord {
            RecordedAtLocal = DateTime.Now,
            Status = NoPoliciesStatus,
            IsEnabled = true,
            IsDryRun = isDryRun,
            PolicyCount = 0,
            PlannedCount = 0,
            ExecutedCount = 0,
            FailedPolicyCount = 0,
            Summary = "数据保留治理未配置任何策略。",
            PolicySummaries = []
        };
    }
}
