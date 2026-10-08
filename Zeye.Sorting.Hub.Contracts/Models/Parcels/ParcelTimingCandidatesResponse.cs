using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>精确查询时序锚点的分页候选，保留重复条码对应的每一票。</summary>
public sealed record ParcelTimingCandidatesResponse {
    /// <summary>当前页候选包裹。</summary>
    public required IReadOnlyList<ParcelTimingCandidateResponse> Items { get; init; }
    /// <summary>从1开始的候选页码。</summary>
    public required int PageNumber { get; init; }
    /// <summary>固定候选页大小。</summary>
    public required int PageSize { get; init; }
    /// <summary>精确命中的包裹总数。</summary>
    public required long TotalCount { get; init; }
}
