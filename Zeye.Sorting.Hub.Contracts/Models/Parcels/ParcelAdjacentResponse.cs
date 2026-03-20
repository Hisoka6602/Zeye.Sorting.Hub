namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// Parcel 邻近查询响应合同。
/// </summary>
public sealed record ParcelAdjacentResponse {
    /// <summary>
    /// 邻近包裹列表（按扫码时间升序）。
    /// </summary>
    public required IReadOnlyList<ParcelListItemResponse> Items { get; init; }

    /// <summary>
    /// 基准时间前查询条数（归一化后）。
    /// </summary>
    public required int BeforeCount { get; init; }

    /// <summary>
    /// 基准时间后查询条数（归一化后）。
    /// </summary>
    public required int AfterCount { get; init; }
}
