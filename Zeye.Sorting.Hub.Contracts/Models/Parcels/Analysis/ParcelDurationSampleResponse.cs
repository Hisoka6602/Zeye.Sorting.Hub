using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>一个真实阶段或一次独立调用的耗时，可继续查看包裹详情和动作时序。</summary>
public sealed record ParcelDurationSampleResponse {
    /// <summary>样本稳定身份，接口重试使用独立身份。</summary>
    public required string Key { get; init; }
    /// <summary>64位中心包裹编号以字符串返回。</summary>
    public required string ParcelId { get; init; }
    /// <summary>主条码。</summary>
    public required string BarCodes { get; init; }
    /// <summary>来源工作台。</summary>
    public required string WorkstationName { get; init; }
    /// <summary>来源实例。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>起点；仅有上报耗时时保持未知。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? StartedAt { get; init; }
    /// <summary>终点；不通过上报耗时倒推起点。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime? EndedAt { get; init; }
    /// <summary>精确时间差或来源明确上报的单调计时耗时。</summary>
    public decimal Milliseconds { get; init; }
    /// <summary>时间端点或来源上报，便于区分精度与证据。</summary>
    public required string TimingSource { get; init; }
    /// <summary>调用Provider。</summary>
    public string? Provider { get; init; }
    /// <summary>脱敏的接口地址。</summary>
    public string? RequestUrl { get; init; }
    /// <summary>调用尝试号；阶段样本为空。</summary>
    public int? AttemptNumber { get; init; }
    /// <summary>明确业务结果，不能用HTTP 200替代业务接受。</summary>
    public bool? IsSuccess { get; init; }
}

