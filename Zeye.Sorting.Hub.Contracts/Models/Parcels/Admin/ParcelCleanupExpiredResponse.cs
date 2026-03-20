namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;

/// <summary>
/// 过期包裹清理治理接口响应合同（与 Domain.DangerousBatchActionResult 对应的外部合同）。
/// </summary>
public sealed record ParcelCleanupExpiredResponse {
    /// <summary>
    /// 动作名称（用于审计检索）。
    /// </summary>
    public required string ActionName { get; init; }

    /// <summary>
    /// 当前决策（blocked=被守卫阻断 / dry-run=演练不执行 / execute=已真实执行）。
    /// </summary>
    public required string Decision { get; init; }

    /// <summary>
    /// 计划处理数量（受仓储单次上限保护）。
    /// </summary>
    public required int PlannedCount { get; init; }

    /// <summary>
    /// 实际执行数量（dry-run 或被阻断时为 0）。
    /// </summary>
    public required int ExecutedCount { get; init; }

    /// <summary>
    /// 是否为 dry-run 演练。
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// 是否被隔离守卫阻断。
    /// </summary>
    public required bool IsBlockedByGuard { get; init; }

    /// <summary>
    /// 补偿边界说明（明确本次操作的回滚范围）。
    /// </summary>
    public required string CompensationBoundary { get; init; }
}
