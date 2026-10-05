using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>工作台窗口聚合的最小包裹快照，避免跨表读取无关包裹属性。</summary>
public sealed record ParcelWorkbenchSnapshot {
    /// <summary>包裹主键，用于同一创建时间的稳定排序。</summary>
    public long Id { get; init; }
    /// <summary>创建时间，用于选择工作台最近上报的名称。</summary>
    public DateTime CreatedTime { get; init; }
    /// <summary>最新扫码时间，用于实时观察窗口。</summary>
    public DateTime ScannedTime { get; init; }
    /// <summary>来源工作台实例。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>工作台名称。</summary>
    public string WorkstationName { get; init; } = string.Empty;
    /// <summary>当前包裹状态。</summary>
    public ParcelStatus Status { get; init; }
}
