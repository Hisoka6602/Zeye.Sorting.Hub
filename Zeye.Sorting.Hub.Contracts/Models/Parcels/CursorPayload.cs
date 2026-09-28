namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// 游标序列化载荷。
/// </summary>
internal sealed record CursorPayload {
    /// <summary>
    /// 最后一条记录扫码时间的本地时钟刻度。
    /// </summary>
    public long LastScannedTimeTicks { get; init; }

    /// <summary>
    /// 最后一条记录的主键 Id。
    /// </summary>
    public long LastId { get; init; }
}
