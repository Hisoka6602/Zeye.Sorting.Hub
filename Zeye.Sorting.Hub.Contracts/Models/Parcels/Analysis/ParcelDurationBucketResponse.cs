namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>有效完成耗时的互不重叠区间，含下限且不含上限。</summary>
public sealed record ParcelDurationBucketResponse {
    /// <summary>区间下限，单位毫秒。</summary>
    public long MinimumMilliseconds { get; init; }
    /// <summary>区间上限，单位毫秒；为空表示没有上限。</summary>
    public long? MaximumMilliseconds { get; init; }
    /// <summary>该区间内的有效完成票数。</summary>
    public long Count { get; init; }
}
