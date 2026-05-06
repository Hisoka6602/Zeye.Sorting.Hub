namespace Zeye.Sorting.Hub.Infrastructure.Persistence.QueryGovernance;

/// <summary>
/// 查询治理报告。
/// </summary>
public sealed record class QueryGovernanceReport {
    /// <summary>
    /// 报告生成时间（本地时间）。
    /// </summary>
    public required DateTime GeneratedAtLocal { get; init; }

    /// <summary>
    /// 已登记模板总数。
    /// </summary>
    public required int RegisteredTemplateCount { get; init; }

    /// <summary>
    /// 当前窗口内的慢查询指纹总数。
    /// </summary>
    public required int ObservedSlowQueryFingerprintCount { get; init; }

    /// <summary>
    /// 已命中画像的模板数量。
    /// </summary>
    public required int MatchedTemplateCount { get; init; }

    /// <summary>
    /// 未命中画像的模板名称列表。
    /// </summary>
    public required IReadOnlyList<string> TemplatesWithoutObservedProfiles { get; init; }

    /// <summary>
    /// 未能映射到模板的慢查询指纹列表。
    /// </summary>
    public required IReadOnlyList<string> UnmatchedSlowQueryFingerprints { get; init; }

    /// <summary>
    /// 当前登记的全部模板。
    /// </summary>
    public required IReadOnlyList<QueryTemplateDescriptor> RegisteredTemplates { get; init; }

    /// <summary>
    /// 当前建议的索引列表。
    /// </summary>
    public required IReadOnlyList<QueryIndexRecommendation> Recommendations { get; init; }
}
