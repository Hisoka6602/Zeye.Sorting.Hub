using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

/// <summary>
/// 危险批量动作执行结果。
/// </summary>
public readonly record struct DangerousBatchActionResult {
    /// <summary>
    /// 动作名称（用于审计检索）。
    /// </summary>
    public required string ActionName { get; init; }

    /// <summary>
    /// 隔离器决策结果。
    /// </summary>
    public required ActionIsolationDecision Decision { get; init; }

    /// <summary>
    /// 计划处理数量（受单次上限保护）。
    /// </summary>
    public required int PlannedCount { get; init; }

    /// <summary>
    /// 实际执行数量（dry-run 或阻断时为 0）。
    /// </summary>
    public required int ExecutedCount { get; init; }

    /// <summary>
    /// 是否为 dry-run。
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// 是否被隔离守卫阻断。
    /// </summary>
    public required bool IsBlockedByGuard { get; init; }

    /// <summary>
    /// 补偿说明（用于明确回滚边界）。
    /// </summary>
    public required string CompensationBoundary { get; init; }
}
