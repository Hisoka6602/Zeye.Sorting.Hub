namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 小车信息响应合同。
/// </summary>
public sealed record SorterCarrierInfoResponse {
    /// <summary>
    /// 小车编号。
    /// </summary>
    public required int SorterCarrierId { get; init; }

    /// <summary>
    /// 包裹上车时间。
    /// </summary>
    public required DateTime LoadedTime { get; init; }

    /// <summary>
    /// 上车时输送带速度（单位：mm/s）。
    /// </summary>
    public required decimal ConveyorSpeedWhenLoaded { get; init; }

    /// <summary>
    /// 联动小车数量。
    /// </summary>
    public required int LinkedCarrierCount { get; init; }
}
