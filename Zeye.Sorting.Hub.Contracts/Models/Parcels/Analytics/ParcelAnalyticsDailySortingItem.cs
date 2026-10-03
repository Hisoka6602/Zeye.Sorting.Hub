namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analytics;

/// <summary>单个本地日期实际完成分拣的包裹件数。</summary>
public sealed record ParcelAnalyticsDailySortingItem {
    /// <summary>本地日期，格式 yyyy-MM-dd。</summary>
    public required string Date { get; init; }
    /// <summary>当天完成分拣的来源包裹件数。</summary>
    public long SortedCount { get; init; }
}
