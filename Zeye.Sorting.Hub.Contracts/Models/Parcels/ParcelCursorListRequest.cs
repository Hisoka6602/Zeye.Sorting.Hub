namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// Parcel 游标分页查询请求合同。
/// </summary>
public sealed record ParcelCursorListRequest {
    /// <summary>
    /// 游标字符串；为空表示查询首页。
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// 页大小。
    /// </summary>
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// 条码检索词。
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
    /// 包裹异常类型（枚举数值）。
    /// </summary>
    public int? ExceptionType { get; init; }

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
