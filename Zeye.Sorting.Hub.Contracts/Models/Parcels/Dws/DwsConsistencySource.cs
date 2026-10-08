namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;

/// <summary>按来源分别观察重复性，并保留同条码跨来源的中位数偏差。</summary>
public sealed record DwsConsistencySource {
    /// <summary>来源实例。</summary>
    public string SourceInstanceId { get; init; } = string.Empty;
    /// <summary>来源工作台。</summary>
    public string? WorkstationName { get; init; }
    /// <summary>唯一测量次数。</summary>
    public int MeasurementCount { get; init; }
    /// <summary>该来源内至少两次测量的条码数。</summary>
    public int RepeatedBarcodeCount { get; init; }
    /// <summary>该来源内超过阈值的条码数。</summary>
    public int DeviationBarcodeCount { get; init; }
    /// <summary>各条码重量中位数相对全部来源中位数的绝对偏差百分比中位数。</summary>
    public decimal? MedianWeightDeviationPercent { get; init; }
    /// <summary>各条码体积中位数相对全部来源中位数的绝对偏差百分比中位数。</summary>
    public decimal? MedianVolumeDeviationPercent { get; init; }
    /// <summary>共有条码的扫码耗时中位数相对总体中位数偏差的中位数，单位百分比。</summary>
    public decimal? MedianScanDurationDeviationPercent { get; init; }
}
