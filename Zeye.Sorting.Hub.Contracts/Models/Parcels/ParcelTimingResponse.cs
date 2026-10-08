using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>按稳定扫码顺序返回前5票、锚点与后5票；数量是实际返回数量。</summary>
public sealed record ParcelTimingResponse {
    /// <summary>本次查询目标的中心编号。</summary>
    public required string AnchorId { get; init; }
    /// <summary>锚点之前实际返回票数。</summary>
    public required int BeforeCount { get; init; }
    /// <summary>锚点之后实际返回票数。</summary>
    public required int AfterCount { get; init; }
    /// <summary>按扫码时间与中心编号升序排列的包裹时序。</summary>
    public required IReadOnlyList<ParcelTimingParcelResponse> Items { get; init; }
}
