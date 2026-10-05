using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>报表只需的包裹标量快照，跨物理表查询不加载完整聚合和明细字段。</summary>
public sealed record ParcelAnalyticsSnapshot {
    /// <summary>首次成功创建时间，本地分表锚点。</summary>
    public DateTime CreatedTime { get; init; }
    /// <summary>来源设备包裹号，用于排除没有成功检测的记录。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>首次检测时间。</summary>
    public DateTime? DetectedTime { get; init; }
    /// <summary>当前包裹状态。</summary>
    public ParcelStatus Status { get; init; }
    /// <summary>当前条码识读状态。</summary>
    public NoReadType NoReadType { get; init; }
    /// <summary>用于兼容历史 NoRead 数据的条码文本。</summary>
    public string BarCodes { get; init; } = string.Empty;
    /// <summary>目标格口编码。</summary>
    public string? TargetChuteCode { get; init; }
    /// <summary>实际格口编码。</summary>
    public string? ActualChuteCode { get; init; }
    /// <summary>完成耗时，单位毫秒。</summary>
    public long? LifecycleMilliseconds { get; init; }
    /// <summary>异常分类。</summary>
    public ParcelExceptionType? ExceptionType { get; init; }
    /// <summary>来源工作台名称。</summary>
    public string WorkstationName { get; init; } = string.Empty;
}
