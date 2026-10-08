using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>一票包裹的真实时序；处理事实仅投影时间和诊断关联元数据，完整报文由详情页读取。</summary>
public sealed record ParcelTimingParcelResponse : ParcelTimingCandidateResponse {
    /// <summary>属于当前包裹来源身份的处理事实时序。</summary>
    public required IReadOnlyList<ParcelProcessingRecordResponse> ProcessingRecords { get; init; }
    /// <summary>既有接口请求的时间投影。</summary>
    public required IReadOnlyList<ParcelTimingApiRequestResponse> ApiRequests { get; init; }
}
