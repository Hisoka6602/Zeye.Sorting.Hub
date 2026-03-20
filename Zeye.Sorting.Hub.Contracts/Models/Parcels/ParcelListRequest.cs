namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// Parcel 列表查询请求合同。
/// </summary>
public sealed record ParcelListRequest {
    /// <summary>
    /// 页码（从 1 开始）。
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// 页大小。
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// 主条码关键字。
    /// </summary>
    public string? BarCodeKeyword { get; init; }

    /// <summary>
    /// 集包号。
    /// </summary>
    public string? BagCode { get; init; }

    /// <summary>
    /// 工作台名称。
    /// </summary>
    public string? WorkstationName { get; init; }

    /// <summary>
    /// 包裹状态（枚举数值）。
    /// </summary>
    public int? Status { get; init; }

    /// <summary>
    /// 实际格口 Id。
    /// </summary>
    public long? ActualChuteId { get; init; }

    /// <summary>
    /// 目标格口 Id。
    /// </summary>
    public long? TargetChuteId { get; init; }

    /// <summary>
    /// 扫码开始时间（含边界）。
    /// </summary>
    public DateTime? ScannedTimeStart { get; init; }

    /// <summary>
    /// 扫码结束时间（含边界）。
    /// </summary>
    public DateTime? ScannedTimeEnd { get; init; }
}
