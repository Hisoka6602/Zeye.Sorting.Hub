namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// 分表巡检报告。
/// </summary>
public sealed record ShardingInspectionReport {
    /// <summary>
    /// 巡检时间（本地时间）。
    /// </summary>
    public required DateTime CheckedAtLocal { get; init; }

    /// <summary>
    /// 数据库提供器名称。
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// 是否启用巡检。
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// 缺失物理表名集合。
    /// </summary>
    public required IReadOnlyList<string> MissingPhysicalTables { get; init; }

    /// <summary>
    /// 缺失关键索引描述集合。
    /// </summary>
    public required IReadOnlyList<string> MissingIndexes { get; init; }

    /// <summary>
    /// 容量或热点风险描述集合。
    /// </summary>
    public required IReadOnlyList<string> CapacityWarnings { get; init; }

    /// <summary>
    /// WebRequestAuditLog 热表与详情表不一致描述集合。
    /// </summary>
    public required IReadOnlyList<string> WebRequestAuditLogPairWarnings { get; init; }

    /// <summary>
    /// 是否健康。
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// 报告摘要消息。
    /// </summary>
    public required string Message { get; init; }
}
