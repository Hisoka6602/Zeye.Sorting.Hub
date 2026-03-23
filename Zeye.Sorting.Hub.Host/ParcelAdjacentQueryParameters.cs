namespace Zeye.Sorting.Hub.Host;

/// <summary>
/// Parcel 邻近查询参数模型。
/// </summary>
internal sealed record ParcelAdjacentQueryParameters {
    /// <summary>
    /// 锚点包裹 Id。
    /// </summary>
    public long? Id { get; init; }

    /// <summary>
    /// 锚点记录前查询条数。
    /// </summary>
    public int BeforeCount { get; init; } = 5;

    /// <summary>
    /// 锚点记录后查询条数。
    /// </summary>
    public int AfterCount { get; init; } = 5;
}
