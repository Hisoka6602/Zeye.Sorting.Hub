namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Workbench;

/// <summary>一个来源实例在完整统计窗口内的当前包裹状态。</summary>
public sealed record ParcelWorkstationSummary {
    /// <summary>来源实例为空的历史包裹才按工作台名称归组。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>最近入库包裹携带的工作台名称。</summary>
    public required string WorkstationName { get; init; }
    /// <summary>包裹件数，重复条码和不同编号会话均独立计数。</summary>
    public long ParcelCount { get; init; }
    /// <summary>当前待分拣件数。</summary>
    public long PendingCount { get; init; }
    /// <summary>当前已完成件数。</summary>
    public long CompletedCount { get; init; }
    /// <summary>当前分拣异常件数。</summary>
    public long ExceptionCount { get; init; }
    /// <summary>其他状态件数，不冒充完成或异常。</summary>
    public long OtherCount { get; init; }
    /// <summary>该窗口总体内最近入库时间，不代表设备在线。</summary>
    public DateTime? LastParcelAt { get; init; }
}
