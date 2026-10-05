using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>处理质量聚合所需的最小字段，统计不读取报文、图片地址或 Provider 原文。</summary>
public sealed record ParcelProcessingStatisticsRow {
    /// <summary>事件实际发生时间，可以晚于物理分表的锚点。</summary>
    public DateTime OccurredAt { get; init; }
    /// <summary>尝试结果，空值不计为失败。</summary>
    public bool? IsSuccess { get; init; }
    /// <summary>关联包裹编号，未绑定事实为空。</summary>
    public long? ParcelId { get; init; }
    /// <summary>处理阶段。</summary>
    public ParcelProcessingStage Stage { get; init; }
}
