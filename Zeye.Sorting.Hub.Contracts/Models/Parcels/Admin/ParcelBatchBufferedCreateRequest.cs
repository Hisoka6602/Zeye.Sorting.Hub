namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;

/// <summary>
/// Parcel 批量缓冲写入请求合同。
/// </summary>
public sealed record ParcelBatchBufferedCreateRequest {
    /// <summary>
    /// 待缓冲写入的包裹集合。
    /// </summary>
    public required ParcelCreateRequest[] Parcels { get; init; }
}
