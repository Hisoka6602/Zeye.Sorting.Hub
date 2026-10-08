using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>耗时只读标量投影，不加载图片、量测及请求/响应正文。</summary>
public sealed record ParcelDurationFactSnapshot {
    /// <summary>来源稳定记录标识。</summary>
    public string RecordId { get; init; } = string.Empty;
    /// <summary>中心包裹编号，未关联事实不进入包裹分析。</summary>
    public long? ParcelId { get; init; }
    /// <summary>来源工作台实例。</summary>
    public string SourceInstanceId { get; init; } = string.Empty;
    /// <summary>来源编号会话。</summary>
    public string SourceRunId { get; init; } = string.Empty;
    /// <summary>设备侧包裹编号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>首次入库分表锚点，不按晚到响应发生日期截断。</summary>
    public DateTime PartitionTime { get; init; }
    /// <summary>来源真实事件时间。</summary>
    public DateTime OccurredAt { get; init; }
    /// <summary>领域处理阶段。</summary>
    public ParcelProcessingStage Stage { get; init; }
    /// <summary>明确业务执行结果。</summary>
    public bool? IsSuccess { get; init; }
    /// <summary>来源是否声明时间可靠。</summary>
    public bool? HasReliableTimestamp { get; init; }
    /// <summary>同阶段独立尝试次数。</summary>
    public int AttemptNumber { get; init; }
    /// <summary>业务接口Provider。</summary>
    public string? Provider { get; init; }
    /// <summary>接口地址，输出前去除认证参数。</summary>
    public string? RequestUrl { get; init; }
    /// <summary>明确请求起点。</summary>
    public DateTime? RequestAt { get; init; }
    /// <summary>明确响应终点。</summary>
    public DateTime? ResponseAt { get; init; }
    /// <summary>来源明确上报的单调计时耗时，单位毫秒。</summary>
    public int? ElapsedMilliseconds { get; init; }
    /// <summary>有界诊断元数据，用于操作和尝试关联。</summary>
    public string? ErrorMessage { get; init; }
    /// <summary>仅提取有界JSON前部的分类元数据，不加载正文。</summary>
    public string? RawPayload { get; init; }
}
