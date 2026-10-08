using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>分析下钻只需的标量摘要；完整事实在包裹详情和时序页按需读取。</summary>
public sealed record ParcelAnalysisParcelResponse {
    /// <summary>中心包裹编号，以字符串保留64位精度。</summary>
    public required string Id { get; init; }
    /// <summary>当前主条码。</summary>
    public required string BarCodes { get; init; }
    /// <summary>首次入库本地时间，决定本次统计总体。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime CreatedTime { get; init; }
    /// <summary>当前扫码本地时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime ScannedTime { get; init; }
    /// <summary>来源实例。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>来源工作台。</summary>
    public required string WorkstationName { get; init; }
    /// <summary>当前包裹状态。</summary>
    public int Status { get; init; }
    /// <summary>当前异常的中文说明，非异常状态为空。</summary>
    public string? ExceptionName { get; init; }
    /// <summary>有效完成耗时，单位毫秒；缺失或无效时为空。</summary>
    public long? LifecycleMilliseconds { get; init; }
    /// <summary>目标格口原始编码。</summary>
    public string? TargetChuteCode { get; init; }
    /// <summary>实际格口原始编码。</summary>
    public string? ActualChuteCode { get; init; }
    /// <summary>明确使用兜底格口，未知时为空。</summary>
    public bool? IsFallbackChuteAssigned { get; init; }
    /// <summary>明确阻断正常路由，未知时为空。</summary>
    public bool? IsRoutingBlocked { get; init; }
}
