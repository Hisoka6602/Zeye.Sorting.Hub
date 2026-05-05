namespace Zeye.Sorting.Hub.Infrastructure.Persistence.QueryGovernance;

/// <summary>
/// 查询模板描述。
/// </summary>
public sealed record class QueryTemplateDescriptor {
    /// <summary>
    /// 模板名称。
    /// </summary>
    public required string TemplateName { get; init; }

    /// <summary>
    /// 业务用途。
    /// </summary>
    public required string Purpose { get; init; }

    /// <summary>
    /// 对应的应用服务名称。
    /// </summary>
    public required string ServiceName { get; init; }

    /// <summary>
    /// 涉及的数据表列表。
    /// </summary>
    public required IReadOnlyList<string> TableNames { get; init; }

    /// <summary>
    /// 过滤字段列表。
    /// </summary>
    public required IReadOnlyList<string> FilterColumns { get; init; }

    /// <summary>
    /// 排序字段列表。
    /// </summary>
    public required IReadOnlyList<string> SortColumns { get; init; }

    /// <summary>
    /// 建议索引列表。
    /// </summary>
    public required IReadOnlyList<string> RecommendedIndexes { get; init; }

    /// <summary>
    /// 最大允许时间范围（小时）。
    /// </summary>
    public required int MaxTimeRangeHours { get; init; }

    /// <summary>
    /// 是否允许执行 Count。
    /// </summary>
    public required bool IsCountAllowed { get; init; }

    /// <summary>
    /// 是否允许深分页。
    /// </summary>
    public required bool IsDeepPagingAllowed { get; init; }
}
