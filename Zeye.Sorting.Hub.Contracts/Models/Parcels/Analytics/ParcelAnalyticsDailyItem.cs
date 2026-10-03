namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analytics;

/// <summary>单个首次入库本地日期的包裹快照指标。</summary>
public sealed record ParcelAnalyticsDailyItem {
    /// <summary>本地日期，yyyy-MM-dd。</summary>
    public required string Date { get; init; }
    /// <summary>检测入库件数。</summary>
    public long DetectedCount { get; init; }
    /// <summary>当前完成件数。</summary>
    public long CompletedCount { get; init; }
    /// <summary>当前异常件数。</summary>
    public long ExceptionCount { get; init; }
    /// <summary>当前 NoRead 件数。</summary>
    public long NoReadCount { get; init; }
    /// <summary>当前目标与实际格口不一致件数。</summary>
    public long ChuteMismatchCount { get; init; }
    /// <summary>有有效生命周期的已完成包裹平均耗时，单位秒。</summary>
    public decimal? AverageLifecycleSeconds { get; init; }
    /// <summary>用于平均完成耗时的有效包裹件数。</summary>
    public long LifecycleSampleCount { get; init; }
}
