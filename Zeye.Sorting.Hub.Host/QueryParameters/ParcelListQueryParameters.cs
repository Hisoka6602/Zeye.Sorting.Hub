namespace Zeye.Sorting.Hub.Host.QueryParameters;

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
    /// 是否返回精确总记录数；HTTP 接口默认关闭以降低查询开销。
    /// </summary>
    public bool? IncludeTotalCount { get; init; }

    /// <summary>
    /// 条码检索词（所有提供器使用一致的子串匹配语义）。
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

    /// <summary>精确来源实例过滤，同名的多个Fusion保持独立。</summary>
    public string? SourceInstanceId { get; init; }

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
