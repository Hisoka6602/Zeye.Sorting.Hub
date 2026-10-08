namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;

/// <summary>按本地首次入库日期分析明确测量身份的DWS重复测量。</summary>
public sealed record ParcelDwsConsistencyRequest {
    /// <summary>起始本地日期，包含当天。</summary>
    public DateTime FromDate { get; init; }
    /// <summary>结束本地日期，包含当天。</summary>
    public DateTime ToDate { get; init; }
    /// <summary>精确匹配条码，区分大小写。</summary>
    public string? Barcode { get; init; }
    /// <summary>精确匹配来源实例。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>精确匹配来源工作台。</summary>
    public string? WorkstationName { get; init; }
    /// <summary>重量绝对差阈值，单位克。</summary>
    public decimal WeightToleranceGrams { get; init; } = 20m;
    /// <summary>重量相对差阈值，单位百分比。</summary>
    public decimal WeightTolerancePercent { get; init; } = 2m;
    /// <summary>物理体积绝对差阈值，单位立方厘米。</summary>
    public decimal VolumeToleranceCm3 { get; init; } = 100m;
    /// <summary>体积相对差阈值，单位百分比。</summary>
    public decimal VolumeTolerancePercent { get; init; } = 3m;
    /// <summary>扫码耗时绝对差阈值，单位毫秒；仅为本次分析容差。</summary>
    public decimal ScanDurationToleranceMilliseconds { get; init; } = 50m;
    /// <summary>扫码耗时相对差阈值，单位百分比。</summary>
    public decimal ScanDurationTolerancePercent { get; init; } = 20m;
    /// <summary>当前查询条码的标准重量，单位克。</summary>
    public decimal? ReferenceWeightGrams { get; init; }
    /// <summary>当前查询条码的标准物理体积，单位立方厘米。</summary>
    public decimal? ReferenceVolumeCm3 { get; init; }
    /// <summary>仅显示超过阈值的条码。</summary>
    public bool OnlyDeviations { get; init; }
    /// <summary>排序依据：weight、volume、scan-duration、scan-duration-p95或count。</summary>
    public string SortBy { get; init; } = "weight";
    /// <summary>条码排行页码，每页20组。</summary>
    public int PageNumber { get; init; } = 1;
    /// <summary>选择查看趋势与测量明细的条码。</summary>
    public string? DetailBarcode { get; init; }
    /// <summary>测量明细页码，每页20条。</summary>
    public int MeasurementPageNumber { get; init; } = 1;
    /// <summary>强制更新一分钟内的只读快照。</summary>
    public bool Refresh { get; init; }
}
