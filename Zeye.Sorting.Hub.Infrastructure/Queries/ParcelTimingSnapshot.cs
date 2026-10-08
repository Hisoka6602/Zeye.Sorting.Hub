using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>时序与有界对比所需的包裹标量，排除聚合附属集合。</summary>
internal sealed record ParcelTimingSnapshot {
    /// <summary>包裹编号。</summary>
    public long Id { get; init; }
    /// <summary>主条码。</summary>
    public string BarCodes { get; init; } = string.Empty;
    /// <summary>来源工作台。</summary>
    public string WorkstationName { get; init; } = string.Empty;
    /// <summary>来源实例。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>来源会话。</summary>
    public string? SourceRunId { get; init; }
    /// <summary>设备包裹号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>当前状态。</summary>
    public ParcelStatus Status { get; init; }
    /// <summary>扫码时间。</summary>
    public DateTime ScannedTime { get; init; }
    /// <summary>首次检测时间。</summary>
    public DateTime? DetectedTime { get; init; }
    /// <summary>首次入库时间。</summary>
    public DateTime CreatedTime { get; init; }
    /// <summary>实际落格时间。</summary>
    public DateTime? DischargeTime { get; init; }
    /// <summary>完成时间。</summary>
    public DateTime? CompletedTime { get; init; }
    /// <summary>重量，千克。</summary>
    public decimal? Weight { get; init; }
    /// <summary>长度，毫米。</summary>
    public decimal? Length { get; init; }
    /// <summary>宽度，毫米。</summary>
    public decimal? Width { get; init; }
    /// <summary>高度，毫米。</summary>
    public decimal? Height { get; init; }
    /// <summary>体积，立方毫米。</summary>
    public decimal? Volume { get; init; }
    /// <summary>目标格口。</summary>
    public string? TargetChuteCode { get; init; }
    /// <summary>实际格口。</summary>
    public string? ActualChuteCode { get; init; }
}
