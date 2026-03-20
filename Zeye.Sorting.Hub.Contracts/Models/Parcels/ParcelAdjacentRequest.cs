namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// Parcel 邻近查询请求合同。
/// </summary>
public sealed record ParcelAdjacentRequest {
    /// <summary>
    /// 基准扫码时间。
    /// </summary>
    public required DateTime ScannedTime { get; init; }

    /// <summary>
    /// 基准时间前查询条数。
    /// </summary>
    public int BeforeCount { get; init; } = 5;

    /// <summary>
    /// 基准时间后查询条数。
    /// </summary>
    public int AfterCount { get; init; } = 5;
}
