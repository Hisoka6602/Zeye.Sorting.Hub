using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Domain.Repositories.Models;

/// <summary>
/// Parcel 列表摘要读模型。
/// </summary>
public sealed record ParcelSummaryReadModel {
    /// <summary>
    /// 包裹 Id。
    /// </summary>
    public required long Id { get; init; }

    /// <summary>
    /// 包裹时间戳。
    /// </summary>
    public required long ParcelTimestamp { get; init; }

    /// <summary>
    /// 包裹主条码。
    /// </summary>
    public required string BarCodes { get; init; }

    /// <summary>
    /// 包裹状态。
    /// </summary>
    public required ParcelStatus Status { get; init; }

    /// <summary>
    /// 包裹异常类型。
    /// </summary>
    public required ParcelExceptionType? ExceptionType { get; init; }

    /// <summary>
    /// 集包号。
    /// </summary>
    public required string BagCode { get; init; }

    /// <summary>
    /// 工作台名称。
    /// </summary>
    public required string WorkstationName { get; init; }

    /// <summary>
    /// 实际格口 Id。
    /// </summary>
    public required long ActualChuteId { get; init; }

    /// <summary>
    /// 目标格口 Id。
    /// </summary>
    public required long TargetChuteId { get; init; }

    /// <summary>
    /// 扫码时间。
    /// </summary>
    public required DateTime ScannedTime { get; init; }

    /// <summary>
    /// 创建时间。
    /// </summary>
    public required DateTime CreatedTime { get; init; }
}
