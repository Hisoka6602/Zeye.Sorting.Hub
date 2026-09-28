namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analytics;

/// <summary>异常类型或工作台的真实分组计数。</summary>
public sealed record ParcelAnalyticsDistributionItem {
    /// <summary>稳定分组编码，未知值为空。</summary>
    public string? Code { get; init; }
    /// <summary>用于展示的分组名称。</summary>
    public required string Name { get; init; }
    /// <summary>分组件数。</summary>
    public long Count { get; init; }
}
