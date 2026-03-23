namespace Zeye.Sorting.Hub.Host;

/// <summary>
/// Parcel 列表查询参数模型。
/// </summary>
internal sealed record ParcelListQueryParameters {
    /// <summary>
    /// 页码（从 1 开始）。
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// 页大小。
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// 条码检索词（MySQL 走 FULLTEXT Boolean 模式，其他 Provider 走 Contains 子串匹配）。
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
    /// 包裹状态。
    /// </summary>
    public int? Status { get; init; }

    /// <summary>
    /// 包裹异常类型（对应 ParcelExceptionType 枚举数值，null 表示不限异常类型）。
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
    /// 扫码开始时间。
    /// </summary>
    public string? ScannedTimeStart { get; init; }

    /// <summary>
    /// 扫码结束时间。
    /// </summary>
    public string? ScannedTimeEnd { get; init; }
}
