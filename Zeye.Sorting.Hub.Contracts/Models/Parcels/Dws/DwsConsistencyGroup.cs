using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;

/// <summary>相同条码的不同测量保留各自包裹和来源身份。</summary>
public sealed record DwsConsistencyGroup {
    /// <summary>精确条码。</summary>
    public string Barcode { get; init; } = string.Empty;
    /// <summary>去重后的明确测量次数。</summary>
    public int MeasurementCount { get; init; }
    /// <summary>不同已关联包裹数量。</summary>
    public int ParcelCount { get; init; }
    /// <summary>不同来源实例数量。</summary>
    public int SourceCount { get; init; }
    /// <summary>最早设备测量时间，未知不补造。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? FirstMeasuredAt { get; init; }
    /// <summary>最晚设备测量时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? LastMeasuredAt { get; init; }
    /// <summary>重量统计，单位克。</summary>
    public DwsMeasurementMetric Weight { get; init; } = new();
    /// <summary>物理体积统计，单位立方厘米。</summary>
    public DwsMeasurementMetric Volume { get; init; } = new();
    /// <summary>来源检测至接收到有效条码的耗时统计，单位毫秒。</summary>
    public DwsMeasurementMetric ScanDuration { get; init; } = new();
    /// <summary>重量差同时超过绝对与相对阈值。</summary>
    public bool WeightDeviates { get; init; }
    /// <summary>体积差同时超过绝对与相对阈值。</summary>
    public bool VolumeDeviates { get; init; }
    /// <summary>扫码耗时差同时超过绝对与相对阈值。</summary>
    public bool ScanDurationDeviates { get; init; }
    /// <summary>相对标准值的偏差超过当前阈值。</summary>
    public bool ReferenceDeviates { get; init; }
    /// <summary>重量、体积或扫码耗时至少有两次有效样本。</summary>
    public bool IsComparable { get; init; }
}
