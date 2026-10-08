namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>DWS一致性只读分析的查询参数，阈值仅用于当前查询。</summary>
public sealed class ParcelDwsConsistencyQueryParameters {
    /// <summary>记录首次入库起始日期，yyyy-MM-dd。</summary>
    public string? FromDate { get; init; }
    /// <summary>记录首次入库结束日期，包含当天。</summary>
    public string? ToDate { get; init; }
    /// <summary>区分大小写的精确条码。</summary>
    public string? Barcode { get; init; }
    /// <summary>来源实例精确筛选。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>来源工作台精确筛选。</summary>
    public string? WorkstationName { get; init; }
    /// <summary>重量绝对差阈值，克。</summary>
    public decimal? WeightToleranceGrams { get; init; }
    /// <summary>重量相对差阈值，百分比。</summary>
    public decimal? WeightTolerancePercent { get; init; }
    /// <summary>体积绝对差阈值，立方厘米。</summary>
    public decimal? VolumeToleranceCm3 { get; init; }
    /// <summary>体积相对差阈值，百分比。</summary>
    public decimal? VolumeTolerancePercent { get; init; }
    /// <summary>扫码耗时绝对差阈值，毫秒。</summary>
    public decimal? ScanDurationToleranceMilliseconds { get; init; }
    /// <summary>扫码耗时相对差阈值，百分比。</summary>
    public decimal? ScanDurationTolerancePercent { get; init; }
    /// <summary>指定条码的标准参考重量，克。</summary>
    public decimal? ReferenceWeightGrams { get; init; }
    /// <summary>指定条码的标准参考物理体积，立方厘米。</summary>
    public decimal? ReferenceVolumeCm3 { get; init; }
    /// <summary>仅查看超过双阈值的条码。</summary>
    public bool? OnlyDeviations { get; init; }
    /// <summary>排序：weight、volume、scan-duration、scan-duration-p95、count。</summary>
    public string? SortBy { get; init; }
    /// <summary>条码排行页码。</summary>
    public int? PageNumber { get; init; }
    /// <summary>查看量测明细的条码。</summary>
    public string? DetailBarcode { get; init; }
    /// <summary>量测明细页码。</summary>
    public int? MeasurementPageNumber { get; init; }
    /// <summary>强制重新获取只读量测快照。</summary>
    public bool? Refresh { get; init; }
}
