using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;

/// <summary>唯一物理DWS测量，接收和绑定不重复计数。</summary>
public sealed record DwsMeasurementSample {
    /// <summary>来源、运行批次与消息身份组成的稳定键。</summary>
    public string Key { get; init; } = string.Empty;
    /// <summary>设备稳定测量消息身份。</summary>
    public string MessageIdentity { get; init; } = string.Empty;
    /// <summary>可追溯的原始处理记录编号。</summary>
    public string RecordId { get; init; } = string.Empty;
    /// <summary>已确认关联包裹编号，未绑定保持空。</summary>
    public string? ParcelId { get; init; }
    /// <summary>明确条码或经完整来源身份校验的包裹条码。</summary>
    public string Barcode { get; init; } = string.Empty;
    /// <summary>来源实例。</summary>
    public string SourceInstanceId { get; init; } = string.Empty;
    /// <summary>来源运行批次。</summary>
    public string SourceRunId { get; init; } = string.Empty;
    /// <summary>来源设备包裹号。</summary>
    public string? SourceParcelId { get; init; }
    /// <summary>来源工作台，不能当作独立DWS设备编号。</summary>
    public string? WorkstationName { get; init; }
    /// <summary>设备实际测量时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? MeasuredAt { get; init; }
    /// <summary>原始接收或关联事件时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime OccurredAt { get; init; }
    /// <summary>同一明确来源包裹的真实检测时间，缺失时不使用Hub入库时间替代。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? ScanStartedAt { get; init; }
    /// <summary>来源程序接收到有效条码的时间，区别于设备测量时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? ScanCompletedAt { get; init; }
    /// <summary>真实检测至有效条码接收耗时，单位毫秒，未知不补零。</summary>
    public decimal? ScanDurationMilliseconds { get; init; }
    /// <summary>结束端点来源：source-received或source-event。</summary>
    public string? ScanTimingBasis { get; init; }
    /// <summary>扫码时间不可用的原因代码；其他量测仍可独立参与分析。</summary>
    public string? ScanTimingUnavailableReason { get; init; }
    /// <summary>重量，单位克，未知不补零。</summary>
    public decimal? WeightGrams { get; init; }
    /// <summary>长度，单位毫米。</summary>
    public decimal? LengthMm { get; init; }
    /// <summary>宽度，单位毫米。</summary>
    public decimal? WidthMm { get; init; }
    /// <summary>高度，单位毫米。</summary>
    public decimal? HeightMm { get; init; }
    /// <summary>物理体积，单位立方厘米，区别于体积重量。</summary>
    public decimal? VolumeCm3 { get; init; }
    /// <summary>是否成功关联明确包裹。</summary>
    public bool BindingConfirmed { get; init; }
}
