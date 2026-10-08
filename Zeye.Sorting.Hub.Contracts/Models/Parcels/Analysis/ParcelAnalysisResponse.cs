namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>数据库聚合的完整总体指标和有界下钻包裹，不能以当前页替代总体。</summary>
public sealed record ParcelAnalysisResponse {
    /// <summary>本次分析类型。</summary>
    public required string View { get; init; }
    /// <summary>包含两端的首次入库起始日期。</summary>
    public required string FromDate { get; init; }
    /// <summary>包含两端的首次入库结束日期。</summary>
    public required string ToDate { get; init; }
    /// <summary>日期及来源筛选后成功检测的包裹总数。</summary>
    public long ParcelCount { get; init; }
    /// <summary>总体内当前已完成票数。</summary>
    public long CompletedCount { get; init; }
    /// <summary>总体内当前异常票数。</summary>
    public long ExceptionCount { get; init; }
    /// <summary>总体内NoRead票数，可能与异常票重叠。</summary>
    public long NoReadCount { get; init; }
    /// <summary>总体内明确阻断路由的票数。</summary>
    public long RoutingBlockedCount { get; init; }
    /// <summary>总体内具有有效完成耗时的样本数。</summary>
    public long LifecycleSampleCount { get; init; }
    /// <summary>当前选中的耗时类型；默认完成耗时兼容既有接口。</summary>
    public string DurationType { get; init; } = "completion";
    /// <summary>扩展耗时类型的统计口径、有效样本及接口分组。</summary>
    public ParcelDurationAnalysisResponse? DurationAnalysis { get; init; }
    /// <summary>平均有效完成耗时，单位毫秒。</summary>
    public decimal? AverageMilliseconds { get; init; }
    /// <summary>完成耗时中位数，偶数样本取中间两项均值。</summary>
    public decimal? MedianMilliseconds { get; init; }
    /// <summary>完成耗时P95，按最近秩向上取整选择样本。</summary>
    public long? P95Milliseconds { get; init; }
    /// <summary>最短有效完成耗时，单位毫秒。</summary>
    public long? MinimumMilliseconds { get; init; }
    /// <summary>最长有效完成耗时，单位毫秒。</summary>
    public long? MaximumMilliseconds { get; init; }
    /// <summary>总体内目标与实际格口编码均有效的票数。</summary>
    public long ComparableChuteCount { get; init; }
    /// <summary>有效格口对中去除首尾空格、忽略大小写后不一致的票数。</summary>
    public long ChuteMismatchCount { get; init; }
    /// <summary>总体内明确采用兜底格口的票数。</summary>
    public long FallbackCount { get; init; }
    /// <summary>所有当前异常类型的数据库聚合。</summary>
    public IReadOnlyList<ParcelAnalysisGroupResponse> ExceptionTypes { get; init; } = [];
    /// <summary>全部有效完成样本的互不重叠耗时分布。</summary>
    public IReadOnlyList<ParcelDurationBucketResponse> DurationBuckets { get; init; } = [];
    /// <summary>格口流向分布，按票数降序并受返回行数预算限制。</summary>
    public IReadOnlyList<ParcelChuteRouteResponse> ChuteRoutes { get; init; } = [];
    /// <summary>流向分布超过返回行数预算；总体指标不受截断影响。</summary>
    public bool ChuteRoutesTruncated { get; init; }
    /// <summary>实际格口独立聚合的热力分布，格口页提供。</summary>
    public ParcelChuteHeatmapResponse? ActualChuteHeatmap { get; init; }
    /// <summary>目标格口独立聚合的热力分布，格口页提供。</summary>
    public ParcelChuteHeatmapResponse? TargetChuteHeatmap { get; init; }
    /// <summary>下钻筛选后的真实包裹总数。</summary>
    public long FilteredCount { get; init; }
    /// <summary>当前下钻页码。</summary>
    public int PageNumber { get; init; }
    /// <summary>固定每页20票。</summary>
    public int PageSize { get; init; } = 20;
    /// <summary>下钻筛选后的当前页包裹摘要。</summary>
    public IReadOnlyList<ParcelAnalysisParcelResponse> Items { get; init; } = [];
}
