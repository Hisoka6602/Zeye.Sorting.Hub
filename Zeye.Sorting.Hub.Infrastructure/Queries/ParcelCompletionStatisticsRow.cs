using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>按实际完成时间统计的窄投影，允许覆盖索引直接提供全部计数字段。</summary>
public sealed record ParcelCompletionStatisticsRow {
    /// <summary>实际完成时间。</summary>
    public DateTime? CompletedTime { get; init; }
    /// <summary>当前包裹状态。</summary>
    public ParcelStatus Status { get; init; }
    /// <summary>来源设备包裹号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>首次检测时间。</summary>
    public DateTime? DetectedTime { get; init; }
}
