using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;

namespace ParcelExceptionRepair;

/// <summary>质量修复字段快照；实际格口、完成时间、原始事实及去重凭据均不改变。</summary>
public sealed record ExceptionRepairSnapshot {
    /// <summary>生命周期状态。</summary>
    public ParcelStatus Status { get; init; }
    /// <summary>保留的异常分类。</summary>
    public ParcelExceptionType? ExceptionType { get; init; }
    /// <summary>是否曾采用兜底格口。</summary>
    public bool? IsFallbackChuteAssigned { get; init; }
    /// <summary>是否曾阻断路由。</summary>
    public bool? IsRoutingBlocked { get; init; }
    /// <summary>从已保存包裹读取修复涉及的字段。</summary>
    public static ExceptionRepairSnapshot Read(Parcel parcel) => new() { Status = parcel.Status, ExceptionType = parcel.ExceptionType,
        IsFallbackChuteAssigned = parcel.IsFallbackChuteAssigned, IsRoutingBlocked = parcel.IsRoutingBlocked };
}
