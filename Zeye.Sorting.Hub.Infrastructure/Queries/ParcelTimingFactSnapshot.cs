using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>少量已选包裹的时序事实标量，按包裹编号在物理分表内检索。</summary>
internal sealed record ParcelTimingFactSnapshot {
    /// <summary>记录编号。</summary>
    public string RecordId { get; init; } = string.Empty;
    /// <summary>关联包裹。</summary>
    public long? ParcelId { get; init; }
    /// <summary>来源实例。</summary>
    public string SourceInstanceId { get; init; } = string.Empty;
    /// <summary>来源会话。</summary>
    public string SourceRunId { get; init; } = string.Empty;
    /// <summary>来源包裹号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>业务阶段。</summary>
    public ParcelProcessingStage Stage { get; init; }
    /// <summary>发生时间。</summary>
    public DateTime OccurredAt { get; init; }
    /// <summary>记录时间。</summary>
    public DateTime RecordedAt { get; init; }
    /// <summary>固定分表时间。</summary>
    public DateTime PartitionTime { get; init; }
    /// <summary>成功标志。</summary>
    public bool? IsSuccess { get; init; }
    /// <summary>尝试次数。</summary>
    public int AttemptNumber { get; init; }
    /// <summary>接口供应方。</summary>
    public string? Provider { get; init; }
    /// <summary>用于提取诊断元数据的来源报文。</summary>
    public string? RawPayload { get; init; }
    /// <summary>来源错误信息。</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>业务决策说明。</summary>
    public string? DecisionReason { get; init; }
    /// <summary>请求时间。</summary>
    public DateTime? RequestAt { get; init; }
    /// <summary>响应时间。</summary>
    public DateTime? ResponseAt { get; init; }
    /// <summary>来源耗时。</summary>
    public int? ElapsedMilliseconds { get; init; }
    /// <summary>目标格口。</summary>
    public string? TargetChuteCode { get; init; }
    /// <summary>实际格口。</summary>
    public string? ActualChuteCode { get; init; }
    /// <summary>时间可靠性。</summary>
    public bool? HasReliableTimestamp { get; init; }
}
