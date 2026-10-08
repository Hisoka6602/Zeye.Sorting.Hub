using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>独立接口调用的耐久统计样本，缺失耗时也保留计数，不保存报文或包裹详情副本。</summary>
public sealed record ParcelDurationCall {
    /// <summary>类型、来源记录和包裹构成的稳定摘要键。</summary>
    public string Key { get; init; } = string.Empty;
    /// <summary>原始调用样本键，支持下钻追溯。</summary>
    public string SampleKey { get; init; } = string.Empty;
    /// <summary>中心包裹身份。</summary>
    public long ParcelId { get; init; }
    /// <summary>来源实例。</summary>
    public string SourceInstanceId { get; init; } = string.Empty;
    /// <summary>来源编号会话。</summary>
    public string SourceRunId { get; init; } = string.Empty;
    /// <summary>来源设备包裹号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>首次入库分表锚点。</summary>
    public DateTime PartitionTime { get; init; }
    /// <summary>明确业务分类。</summary>
    public string Type { get; init; } = string.Empty;
    /// <summary>来源请求起点。</summary>
    public DateTime? StartedAt { get; init; }
    /// <summary>来源响应终点。</summary>
    public DateTime? EndedAt { get; init; }
    /// <summary>有效耗时毫秒；未知保持空，真实零保留。</summary>
    public decimal? Milliseconds { get; init; }
    /// <summary>时间端点或来源明确上报。</summary>
    public string TimingSource { get; init; } = string.Empty;
    /// <summary>明确业务 Provider。</summary>
    public string? Provider { get; init; }
    /// <summary>去除参数和凭据的接口地址。</summary>
    public string? RequestUrl { get; init; }
    /// <summary>独立尝试次数。</summary>
    public int? AttemptNumber { get; init; }
    /// <summary>业务接受结果，HTTP 成功不冒充业务接受。</summary>
    public bool? IsSuccess { get; init; }

    /// <summary>从同一来源调用构造不带正文的统计样本。</summary>
    internal static ParcelDurationCall Create(string type, string key, ParcelDurationFactSnapshot record,
        DateTime? start, DateTime? end, decimal? elapsed, string timingSource, string? provider, int? attempt, bool? success) => new() {
        Key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(type + "\n" + key + "\n" + record.ParcelId!.Value.ToString(CultureInfo.InvariantCulture)))),
        SampleKey = key, ParcelId = record.ParcelId!.Value, SourceInstanceId = record.SourceInstanceId,
        SourceRunId = record.SourceRunId, SourceParcelId = record.SourceParcelId, PartitionTime = record.PartitionTime,
        Type = type, StartedAt = start, EndedAt = end, Milliseconds = elapsed, TimingSource = timingSource,
        Provider = provider, RequestUrl = ParcelDurationAnalysisReader.SafeUrl(record.RequestUrl), AttemptNumber = attempt, IsSuccess = success
    };
}
