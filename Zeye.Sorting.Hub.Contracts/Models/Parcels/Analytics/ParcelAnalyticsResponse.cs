namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analytics;

/// <summary>按首次入库日期统计的包裹快照，以及按事件发生日期统计的独立处理事实。</summary>
public sealed record ParcelAnalyticsResponse {
    /// <summary>本地日期起点，包含当天。</summary>
    public required string FromDate { get; init; }
    /// <summary>本地日期终点，包含当天。</summary>
    public required string ToDate { get; init; }
    /// <summary>在窗口内首次入库且已检测的来源包裹数。</summary>
    public long DetectedCount { get; init; }
    /// <summary>上述包裹当前已完成的件数。</summary>
    public long CompletedCount { get; init; }
    /// <summary>上述包裹当前处于分拣异常状态的件数。</summary>
    public long ExceptionCount { get; init; }
    /// <summary>上述包裹当前 NoRead 状态或条码为 NoRead 的件数。</summary>
    public long NoReadCount { get; init; }
    /// <summary>上述包裹目标和实际格口均已知但不一致的件数。</summary>
    public long ChuteMismatchCount { get; init; }
    /// <summary>有有效生命周期的已完成包裹平均耗时，单位秒；无样本时为空。</summary>
    public decimal? AverageLifecycleSeconds { get; init; }
    /// <summary>窗口内成功入库的来源包裹，相邻首次创建时间的正间隔中位数，单位毫秒；无有效间隔时为空。</summary>
    public decimal? MedianCreationIntervalMilliseconds { get; init; }
    /// <summary>窗口内成功入库的来源包裹，最短的正相邻创建间隔，单位毫秒；无有效间隔时为空。</summary>
    public decimal? MinimumCreationIntervalMilliseconds { get; init; }
    /// <summary>实际每小时分拣票数：3600000 / 创建间隔中位数；无有效间隔时为空。</summary>
    public decimal? ActualSortingThroughputPerHour { get; init; }
    /// <summary>理论每小时分拣票数：3600000 / 最短正创建间隔；无有效间隔时为空。</summary>
    public decimal? TheoreticalSortingThroughputPerHour { get; init; }
    /// <summary>用于计算实际与理论时效的有效相邻间隔数，重试和非正间隔不计入。</summary>
    public long CreationIntervalSampleCount { get; init; }
    /// <summary>按首次入库本地日期分组的快照汇总。</summary>
    public required IReadOnlyList<ParcelAnalyticsDailyItem> Daily { get; init; }
    /// <summary>按实际完成分拣本地日期分组的包裹件数。</summary>
    public required IReadOnlyList<ParcelAnalyticsDailySortingItem> DailySorting { get; init; }
    /// <summary>当前分拣异常类型分布。</summary>
    public required IReadOnlyList<ParcelAnalyticsDistributionItem> ExceptionTypes { get; init; }
    /// <summary>来源工作台分布。</summary>
    public required IReadOnlyList<ParcelAnalyticsDistributionItem> Workstations { get; init; }
    /// <summary>工作台分布是否因查询行数预算被截断。</summary>
    public bool WorkstationsTruncated { get; init; }
    /// <summary>在窗口内发生的处理事实总数，与包裹快照不是同一总体。</summary>
    public long ProcessingEventCount { get; init; }
    /// <summary>上述处理事实中明确失败的尝试数。</summary>
    public long FailedAttemptCount { get; init; }
    /// <summary>上述处理事实中未绑定包裹的 DWS 接收或绑定事实数。</summary>
    public long UnboundDwsEventCount { get; init; }
}
