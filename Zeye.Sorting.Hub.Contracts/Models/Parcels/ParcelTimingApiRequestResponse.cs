using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>兼容既有包裹的接口时序，不读取请求头和报文正文。</summary>
public sealed record ParcelTimingApiRequestResponse {
    /// <summary>既有接口业务类型。</summary>
    public int ApiType { get; init; }
    /// <summary>既有接口请求状态。</summary>
    public int RequestStatus { get; init; }
    /// <summary>实际请求本地时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime RequestTime { get; init; }
    /// <summary>实际响应本地时间，尚无响应时为空。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? ResponseTime { get; init; }
    /// <summary>来源记录的耗时，不能据此补造响应时间。</summary>
    public int ElapsedMilliseconds { get; init; }
}
