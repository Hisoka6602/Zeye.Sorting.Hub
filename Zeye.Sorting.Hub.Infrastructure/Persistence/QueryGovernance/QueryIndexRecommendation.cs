namespace Zeye.Sorting.Hub.Infrastructure.Persistence.QueryGovernance;

/// <summary>
/// 查询治理索引建议。
/// </summary>
public sealed record class QueryIndexRecommendation {
    /// <summary>
    /// 模板名称。
    /// </summary>
    public required string TemplateName { get; init; }

    /// <summary>
    /// 慢查询指纹。
    /// </summary>
    public required string Fingerprint { get; init; }

    /// <summary>
    /// 目标表名。
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// 建议索引表达式。
    /// </summary>
    public required string RecommendedIndex { get; init; }

    /// <summary>
    /// 建议原因。
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// 风险等级。
    /// </summary>
    public required string RiskLevel { get; init; }

    /// <summary>
    /// 建议置信度。
    /// </summary>
    public required decimal Confidence { get; init; }

    /// <summary>
    /// 观测到的 P99 耗时（毫秒）。
    /// </summary>
    public required double ObservedP99Milliseconds { get; init; }

    /// <summary>
    /// 观测到的调用次数。
    /// </summary>
    public required int ObservedCallCount { get; init; }

    /// <summary>
    /// 归一化 SQL。
    /// </summary>
    public required string NormalizedSql { get; init; }

    /// <summary>
    /// 样例 SQL。
    /// </summary>
    public required string SampleSql { get; init; }
}
