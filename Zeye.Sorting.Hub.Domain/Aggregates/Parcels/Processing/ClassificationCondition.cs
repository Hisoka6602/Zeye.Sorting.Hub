namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
/// <summary>用户配置的单个分类条件，数值使用明确单位。</summary>
public sealed record ClassificationCondition {
    /// <summary>来源字段。</summary>
    public string Field { get; init; } = string.Empty;
    /// <summary>比较运算符。</summary>
    public string Operator { get; init; } = string.Empty;
    /// <summary>数值阈值或兼容的单项文本。</summary>
    public string? Value { get; init; }
    /// <summary>多个文本匹配值。</summary>
    public string[]? Values { get; init; }
    /// <summary>数值单位。</summary>
    public string? Unit { get; init; }
}
