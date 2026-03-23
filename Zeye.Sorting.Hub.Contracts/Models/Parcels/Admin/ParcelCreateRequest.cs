namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;

/// <summary>
/// 管理端新增 Parcel 请求合同。
/// </summary>
public sealed record ParcelCreateRequest {
    /// <summary>
    /// 包裹 Id，由调用方传入，必须大于 0，且全局唯一。
    /// </summary>
    public required long Id { get; init; }

    /// <summary>
    /// 包裹时间戳（Unix Ticks）。
    /// </summary>
    public required long ParcelTimestamp { get; init; }

    /// <summary>
    /// 包裹类型（对应 Domain.ParcelType 枚举数值）。
    /// </summary>
    public required int Type { get; init; }

    /// <summary>
    /// 主条码（不允许空字符串）。
    /// </summary>
    public required string BarCodes { get; init; }

    /// <summary>
    /// 重量（千克，精度 3 位小数）。
    /// </summary>
    public required decimal Weight { get; init; }

    /// <summary>
    /// 工作台名称（不允许空字符串）。
    /// </summary>
    public required string WorkstationName { get; init; }

    /// <summary>
    /// 扫码时间（本地时间字符串，不允许 UTC/offset 格式）。
    /// 格式支持：yyyy-MM-dd / yyyy-MM-dd HH:mm:ss / yyyy-MM-ddTHH:mm:ss 等，不允许 Z 或 offset 表达。
    /// </summary>
    public required string ScannedTime { get; init; }

    /// <summary>
    /// 落格时间（本地时间字符串，不允许 UTC/offset 格式）。
    /// 格式支持：yyyy-MM-dd / yyyy-MM-dd HH:mm:ss / yyyy-MM-ddTHH:mm:ss 等，不允许 Z 或 offset 表达。
    /// </summary>
    public required string DischargeTime { get; init; }

    /// <summary>
    /// 目标格口 Id（必须大于 0）。
    /// </summary>
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 实际格口 Id（必须大于 0）。
    /// </summary>
    public required long ActualChuteId { get; init; }

    /// <summary>
    /// 外部接口访问状态（对应 Domain.ApiRequestStatus 枚举数值）。
    /// </summary>
    public required int RequestStatus { get; init; }

    /// <summary>
    /// 集包号（可为空字符串）。
    /// </summary>
    public string BagCode { get; init; } = string.Empty;

    /// <summary>
    /// 是否叠包。
    /// </summary>
    public bool IsSticking { get; init; }

    /// <summary>
    /// 长度（毫米，精度 3 位小数）。
    /// </summary>
    public decimal Length { get; init; }

    /// <summary>
    /// 宽度（毫米，精度 3 位小数）。
    /// </summary>
    public decimal Width { get; init; }

    /// <summary>
    /// 高度（毫米，精度 3 位小数）。
    /// </summary>
    public decimal Height { get; init; }

    /// <summary>
    /// 体积（立方毫米，精度 3 位小数）。
    /// </summary>
    public decimal Volume { get; init; }

    /// <summary>
    /// 是否有图片。
    /// </summary>
    public bool HasImages { get; init; }

    /// <summary>
    /// 是否有视频。
    /// </summary>
    public bool HasVideos { get; init; }

    /// <summary>
    /// 包裹坐标位置（原始字符串，可为空）。
    /// </summary>
    public string Coordinate { get; init; } = string.Empty;

    /// <summary>
    /// NoRead 类型（对应 Domain.NoReadType 枚举数值，默认为 0=None）。
    /// </summary>
    public int NoReadType { get; init; }

    /// <summary>
    /// 小车编号（可空）。
    /// </summary>
    public int? SorterCarrierId { get; init; }

    /// <summary>
    /// 三段码（可空）。
    /// </summary>
    public string? SegmentCodes { get; init; }

    /// <summary>
    /// 生命周期（毫秒，可空）。
    /// </summary>
    public long? LifecycleMilliseconds { get; init; }
}
