using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>时序查询所需的包裹身份、业务节点与Hub入库时间；64位身份始终输出字符串。</summary>
public record ParcelTimingCandidateResponse {
    /// <summary>中心包裹编号。</summary>
    public required string Id { get; init; }
    /// <summary>已保存的主条码。</summary>
    public required string BarCodes { get; init; }
    /// <summary>来源工作台名称。</summary>
    public required string WorkstationName { get; init; }
    /// <summary>来源实例编码。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>来源运行批次。</summary>
    public string? SourceRunId { get; init; }
    /// <summary>来源包裹编号。</summary>
    public string? SourceParcelId { get; init; }
    /// <summary>包裹状态枚举。</summary>
    public int Status { get; init; }
    /// <summary>排序依据的实际扫码本地时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime ScannedTime { get; init; }
    /// <summary>首次检测本地时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? DetectedTime { get; init; }
    /// <summary>Hub首次入库本地时间，与来源业务检测时间分别展示。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime CreatedTime { get; init; }
    /// <summary>实际落格本地时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? DischargeTime { get; init; }
    /// <summary>处理完成本地时间。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? CompletedTime { get; init; }
}
