namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// 分表预建计划。
/// </summary>
public sealed record ShardingPrebuildPlan {
    /// <summary>
    /// 计划生成时间（本地时间）。
    /// </summary>
    public required DateTime GeneratedAtLocal { get; init; }

    /// <summary>
    /// 是否启用预建计划。
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// 是否为 dry-run。
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// 预建窗口（小时）。
    /// </summary>
    public required int PrebuildAheadHours { get; init; }

    /// <summary>
    /// 计划覆盖的物理表集合。
    /// </summary>
    public required IReadOnlyList<string> PlannedPhysicalTables { get; init; }

    /// <summary>
    /// 当前缺失的物理表集合。
    /// </summary>
    public required IReadOnlyList<string> MissingPhysicalTables { get; init; }

    /// <summary>
    /// 计划摘要消息。
    /// </summary>
    public required string Message { get; init; }
}
