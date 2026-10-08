using System.Text.Json.Serialization;
using Zeye.Sorting.Hub.Contracts.Serialization;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>当前耗时类型的完整统计总体，与下方区间分页分开。</summary>
public sealed record ParcelDurationAnalysisResponse {
    /// <summary>耗时类型稳定编码。</summary>
    public required string Type { get; init; }
    /// <summary>类型中文名称。</summary>
    public required string Name { get; init; }
    /// <summary>真实时间端点与纳入规则。</summary>
    public required string Description { get; init; }
    /// <summary>票或次；接口失败与重试各自为一次样本。</summary>
    public required string Unit { get; init; }
    /// <summary>统计读取时间，短时缓存不会伪装成新快照。</summary>
    [JsonConverter(typeof(LocalDateTimeJsonConverter))]
    public DateTime GeneratedAt { get; init; }
    /// <summary>应观测的包裹或已观测调用总数。</summary>
    public long ObservedCount { get; init; }
    /// <summary>时间缺失、不可靠或倒序的样本数。</summary>
    public long UnavailableCount { get; init; }
    /// <summary>有有效耗时的样本数。</summary>
    public long SampleCount { get; init; }
    /// <summary>有效样本覆盖的独立包裹数。</summary>
    public long ParcelCount { get; init; }
    /// <summary>有效样本中明确失败的调用次数。</summary>
    public long FailedCount { get; init; }
    /// <summary>有效样本中业务结果未知的调用次数。</summary>
    public long UnknownResultCount { get; init; }
    /// <summary>平均耗时，保留亚毫秒精度。</summary>
    public decimal? AverageMilliseconds { get; init; }
    /// <summary>中位数，偶数取中间两项均值。</summary>
    public decimal? MedianMilliseconds { get; init; }
    /// <summary>P95采用最近秩。</summary>
    public decimal? P95Milliseconds { get; init; }
    /// <summary>最短有效耗时。</summary>
    public decimal? MinimumMilliseconds { get; init; }
    /// <summary>最长有效耗时。</summary>
    public decimal? MaximumMilliseconds { get; init; }
    /// <summary>按完整有效样本统计的分布。</summary>
    public IReadOnlyList<ParcelDurationBucketResponse> Buckets { get; init; } = [];
    /// <summary>Provider与地址分组；非接口类型为空。</summary>
    public IReadOnlyList<ParcelDurationInterfaceResponse> Interfaces { get; init; } = [];
    /// <summary>接口分组超出有界返回预算。</summary>
    public bool InterfacesTruncated { get; init; }
    /// <summary>下钻区间内的真实样本数。</summary>
    public long FilteredCount { get; init; }
    /// <summary>当前页样本，每页20条，重试不会因包裹Id相同而合并。</summary>
    public IReadOnlyList<ParcelDurationSampleResponse> Items { get; init; } = [];
}


