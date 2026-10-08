using System.Security.Cryptography;
using System.Text;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>接口耗时的耐久窄投影；只保留身份、端点及分类，不复制任何报文正文。</summary>
public sealed record ParcelDurationFact {
    /// <summary>与原始处理事实相同的幂等键。</summary>
    public string Key { get; init; } = string.Empty;
    /// <summary>来源记录标识，支持回到处理详情。</summary>
    public string RecordId { get; init; } = string.Empty;
    /// <summary>关联的中心包裹编号。</summary>
    public long? ParcelId { get; init; }
    /// <summary>来源工作台实例。</summary>
    public string SourceInstanceId { get; init; } = string.Empty;
    /// <summary>来源计数会话。</summary>
    public string SourceRunId { get; init; } = string.Empty;
    /// <summary>设备包裹编号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>首次入库锚点，晚到响应仍属于原分表。</summary>
    public DateTime PartitionTime { get; init; }
    /// <summary>来源真实发生时间。</summary>
    public DateTime OccurredAt { get; init; }
    /// <summary>原始领域阶段。</summary>
    public ParcelProcessingStage Stage { get; init; }
    /// <summary>来源声明的时间可靠性。</summary>
    public bool? HasReliableTimestamp { get; init; }
    /// <summary>明确请求时间，缺失时不倒推。</summary>
    public DateTime? RequestAt { get; init; }
    /// <summary>明确响应时间。</summary>
    public DateTime? ResponseAt { get; init; }
    /// <summary>来源明确上报的单调计时毫秒数，包括真实零值。</summary>
    public int? ElapsedMilliseconds { get; init; }
    /// <summary>同一操作的尝试次数。</summary>
    public int AttemptNumber { get; init; }
    /// <summary>业务类型，不从地址或正文猜测。</summary>
    public string Type { get; init; } = string.Empty;
    /// <summary>实际 HTTP 交互标识。</summary>
    public bool Transport { get; init; }
    /// <summary>稳定操作身份；过长身份使用无歧义摘要。</summary>
    public string OperationId { get; init; } = string.Empty;
    /// <summary>稳定尝试身份。</summary>
    public string AttemptId { get; init; } = string.Empty;
    /// <summary>来源操作状态，HTTP 成功不替代业务接受。</summary>
    public string Outcome { get; init; } = string.Empty;
    /// <summary>来源业务 Provider。</summary>
    public string Provider { get; init; } = string.Empty;
    /// <summary>已去除凭据、参数和片段的接口地址。</summary>
    public string? RequestUrl { get; init; }
    /// <summary>明确业务结果；未提供保持未知。</summary>
    public bool? IsSuccess { get; init; }
    /// <summary>配置明确禁用了操作。</summary>
    public bool Skipped { get; init; }
    /// <summary>后台已将该诊断归并为调用；新诊断默认待处理，页面仍能读到即时变化。</summary>
    public bool Projected { get; init; }

    /// <summary>只有接口和调用诊断进入窄投影，其他阶段仍使用原有覆盖索引。</summary>
    internal static bool Includes(ParcelProcessingStage stage) => stage is ParcelProcessingStage.ScanUploaded
        or ParcelProcessingStage.LandingReported or ParcelProcessingStage.ImageUploaded;

    /// <summary>纯内存派生；与原事实使用同一 EF 保存事务，不增加读库或文件操作。</summary>
    internal static ParcelDurationFact Create(ParcelProcessingRecord record) {
        var snapshot = new ParcelDurationFactSnapshot {
            RecordId = record.RecordId, ParcelId = record.ParcelId, SourceInstanceId = record.SourceInstanceId,
            SourceRunId = record.SourceRunId, SourceParcelId = record.SourceParcelId, PartitionTime = record.PartitionTime,
            OccurredAt = record.OccurredAt, Stage = record.Stage, IsSuccess = record.IsSuccess,
            HasReliableTimestamp = record.HasReliableTimestamp, AttemptNumber = record.AttemptNumber,
            Provider = record.Provider, RequestUrl = record.RequestUrl, RequestAt = record.RequestAt,
            ResponseAt = record.ResponseAt, ElapsedMilliseconds = record.ElapsedMilliseconds, ErrorMessage = record.ErrorMessage,
            RawPayload = record.RawPayload is { } raw ? raw[..Math.Min(raw.Length,
                record.ErrorMessage?.StartsWith("{\"operationId\"", StringComparison.Ordinal) == true ? 512 : 2048)] : null
        };
        var call = new ParcelDurationCallEvent(snapshot, ParcelDurationAnalysisReader.Metadata(snapshot));
        return new() {
            Key = record.Key, RecordId = record.RecordId, ParcelId = record.ParcelId,
            SourceInstanceId = record.SourceInstanceId, SourceRunId = record.SourceRunId, SourceParcelId = record.SourceParcelId,
            PartitionTime = record.PartitionTime, OccurredAt = record.OccurredAt, Stage = record.Stage,
            HasReliableTimestamp = record.HasReliableTimestamp, RequestAt = record.RequestAt, ResponseAt = record.ResponseAt,
            ElapsedMilliseconds = record.ElapsedMilliseconds, AttemptNumber = call.Record.AttemptNumber,
            Type = call.Type, Transport = call.Transport, OperationId = BoundIdentity(call.OperationId, 128),
            AttemptId = BoundIdentity(call.AttemptId, 128), Outcome = call.Outcome.Length <= 128 ? call.Outcome : "unrecognized",
            Provider = BoundIdentity(call.Provider, 512), RequestUrl = ParcelDurationAnalysisReader.SafeUrl(record.RequestUrl),
            IsSuccess = call.Success, Skipped = call.Skipped
        };
    }

    /// <summary>超出协议长度的身份不能简单截断，以免将独立操作错误合并。</summary>
    private static string BoundIdentity(string value, int limit) => value.Length <= limit ? value
        : "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
