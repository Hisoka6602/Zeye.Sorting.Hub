namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// Parcel 邻近查询请求合同。
/// </summary>
public sealed record ParcelAdjacentRequest {
    /// <summary>
    /// 锚点包裹 Id（必须大于 0）。
    /// </summary>
    public required long Id { get; init; }

    /// <summary>
    /// 锚点记录前查询条数。
    /// </summary>
    public int BeforeCount { get; init; } = 5;

    /// <summary>
    /// 锚点记录后查询条数。
    /// </summary>
    public int AfterCount { get; init; } = 5;
}
