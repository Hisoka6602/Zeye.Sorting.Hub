namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Workbench;

/// <summary>按扫码时间统计完整的滚动24小时窗口，与最近明细的分页上限无关。</summary>
public sealed record ParcelWorkbenchResponse {
    /// <summary>本地统计起点，包含边界。</summary>
    public required DateTime WindowStartLocal { get; init; }
    /// <summary>本地统计终点，包含边界。</summary>
    public required DateTime WindowEndLocal { get; init; }
    /// <summary>完整窗口内包裹总数，包含缺少来源标识的历史包裹。</summary>
    public long ParcelCount { get; init; }
    /// <summary>缺少来源实例与工作台名称的包裹数。</summary>
    public long UnassignedCount { get; init; }
    /// <summary>来源实例优先于名称，计数会话不拆分工作台。</summary>
    public required IReadOnlyList<ParcelWorkstationSummary> Workstations { get; init; }
}
