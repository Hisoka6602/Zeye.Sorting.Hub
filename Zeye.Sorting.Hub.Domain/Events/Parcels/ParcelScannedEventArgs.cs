namespace Zeye.Sorting.Hub.Domain.Events.Parcels;

/// <summary>
/// 包裹扫描事件载荷。
/// 携带包裹首次进入分拣流水线时的关键业务字段，供下游处理程序使用。
/// </summary>
internal readonly record struct ParcelScannedEventArgs {
    /// <summary>
    /// 包裹数据库主键 Id。
    /// </summary>
    public required long ParcelId { get; init; }

    /// <summary>
    /// 主条码字符串（原始扫码结果）。
    /// </summary>
    public required string BarCodes { get; init; }

    /// <summary>
    /// 工作台名称（发起扫码的工作站）。
    /// </summary>
    public required string WorkstationName { get; init; }

    /// <summary>
    /// 扫码时间（本地时间语义，禁止 UTC）。
    /// </summary>
    public required DateTime ScannedTime { get; init; }

    /// <summary>
    /// 集包号（可为空字符串，无集包场景传 string.Empty）。
    /// </summary>
    public required string BagCode { get; init; }

    /// <summary>
    /// 系统路由分配的目标格口 Id。
    /// </summary>
    public required long TargetChuteId { get; init; }
}
