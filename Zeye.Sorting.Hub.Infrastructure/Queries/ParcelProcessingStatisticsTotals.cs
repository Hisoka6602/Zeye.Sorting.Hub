namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>处理事实状态组的三项计数，组间求和得到总体，空窗口的汇总结果为零。</summary>
internal sealed record ParcelProcessingStatisticsTotals {
    /// <summary>发生在窗口内的全部处理事件。</summary>
    public long Count { get; init; }
    /// <summary>明确失败的尝试，未知结果不计为失败。</summary>
    public long Failed { get; init; }
    /// <summary>尚未关联包裹的 DWS 接收或绑定事实。</summary>
    public long UnboundDws { get; init; }
}
