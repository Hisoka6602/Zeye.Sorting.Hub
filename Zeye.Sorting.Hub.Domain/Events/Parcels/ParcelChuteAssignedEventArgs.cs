namespace Zeye.Sorting.Hub.Domain.Events.Parcels;

/// <summary>
/// 包裹分配格口事件载荷。
/// 携带包裹完成格口分配动作时的关键业务字段，供下游处理程序使用。
/// </summary>
internal readonly record struct ParcelChuteAssignedEventArgs {
    /// <summary>
    /// 包裹数据库主键 Id。
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 系统路由分配的目标格口 Id（理论落格位置）。
    /// </summary>
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 包裹实际到达的格口 Id（真实落格位置）。
    /// </summary>
    public required long ActualChuteId { get; init; }

    /// <summary>
    /// 包裹扫码时间（本地时间语义，禁止 UTC），用于关联分拣流水线时间轴。
    /// </summary>
    public required DateTime ScannedTime { get; init; }
}
