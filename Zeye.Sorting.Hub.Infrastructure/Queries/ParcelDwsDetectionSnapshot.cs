namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>扫码耗时所需的窄检测事实，不加载包裹聚合或原始报文。</summary>
public sealed record ParcelDwsDetectionSnapshot {
    /// <summary>不可变事实键。</summary>
    public string Key { get; init; } = string.Empty;
    /// <summary>明确关联的Hub包裹编号。</summary>
    public long? ParcelId { get; init; }
    /// <summary>来源实例。</summary>
    public string SourceInstanceId { get; init; } = string.Empty;
    /// <summary>设备运行会话。</summary>
    public string SourceRunId { get; init; } = string.Empty;
    /// <summary>来源设备包裹编号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>真实来源检测时间。</summary>
    public DateTime OccurredAt { get; init; }
    /// <summary>不可变入库分表锚点。</summary>
    public DateTime PartitionTime { get; init; }
    /// <summary>处理阶段，仅查询Detected。</summary>
    public Zeye.Sorting.Hub.Domain.Enums.Parcels.ParcelProcessingStage Stage { get; init; }
    /// <summary>明确不可靠的时间不能参与统计。</summary>
    public bool? HasReliableTimestamp { get; init; }
    /// <summary>明确失败的检测不作为扫码起点。</summary>
    public bool? IsSuccess { get; init; }
}
