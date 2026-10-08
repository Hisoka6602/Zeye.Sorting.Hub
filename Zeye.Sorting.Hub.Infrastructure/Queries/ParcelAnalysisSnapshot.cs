using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>分析查询的标量读模型，不加载Owned集合或原始报文。</summary>
public sealed record ParcelAnalysisSnapshot {
    /// <summary>中心包裹编号。</summary>
    public long Id { get; init; }
    /// <summary>固定的首次入库时间。</summary>
    public DateTime CreatedTime { get; init; }
    /// <summary>当前扫码时间。</summary>
    public DateTime ScannedTime { get; init; }
    /// <summary>首次检测时间。</summary>
    public DateTime? DetectedTime { get; init; }
    /// <summary>完成时间，用于拒绝倒序的耗时样本。</summary>
    public DateTime? CompletedTime { get; init; }
    /// <summary>来源设备包裹号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>来源实例。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>来源运行批次，防止同号设备事实串联。</summary>
    public string? SourceRunId { get; init; }
    /// <summary>来源工作台。</summary>
    public string WorkstationName { get; init; } = string.Empty;
    /// <summary>当前包裹状态。</summary>
    public ParcelStatus Status { get; init; }
    /// <summary>当前异常分类。</summary>
    public ParcelExceptionType? ExceptionType { get; init; }
    /// <summary>当前识读状态。</summary>
    public NoReadType NoReadType { get; init; }
    /// <summary>当前主条码。</summary>
    public string BarCodes { get; init; } = string.Empty;
    /// <summary>持久化完成耗时，单位毫秒。</summary>
    public long? LifecycleMilliseconds { get; init; }
    /// <summary>目标格口原始编码。</summary>
    public string? TargetChuteCode { get; init; }
    /// <summary>实际格口原始编码。</summary>
    public string? ActualChuteCode { get; init; }
    /// <summary>是否使用兜底格口。</summary>
    public bool? IsFallbackChuteAssigned { get; init; }
    /// <summary>是否阻断正常路由。</summary>
    public bool? IsRoutingBlocked { get; init; }
}
