namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>一次明确尝试的调用窗口，用于业务分类与HTTP交互关联；超时后的迟到回执不延长窗口。</summary>
internal sealed class ParcelDurationCallWindow(ParcelDurationCallEvent[] events) {
    /// <summary>该次调用开始事实，缺失时保留第一个诊断。</summary>
    public ParcelDurationCallEvent Anchor { get; } = events.FirstOrDefault(item => item.Outcome == "started") ?? events[0];
    /// <summary>明确开始事实，不从响应或耗时倒推。</summary>
    public ParcelDurationCallEvent? Start { get; } = events.FirstOrDefault(item => item.Outcome == "started");
    /// <summary>第一个终态，迟到结果不替代超时终态。</summary>
    public ParcelDurationCallEvent? Terminal { get; } = events.FirstOrDefault(item => item.Outcome is "completed" or "accepted" or "failed" or "unknown" or "cancelled" or "failed-or-unknown");
    /// <summary>来源明确禁用了该操作。</summary>
    public bool Skipped => events.Any(item => item.Skipped);
    /// <summary>来源、Provider、业务类型和真实时间窗口都必须一致。</summary>
    public bool Contains(ParcelDurationCallEvent item) => Start is not null && Terminal is not null && item.Record.ParcelId == Anchor.Record.ParcelId
        && item.Record.SourceInstanceId == Anchor.Record.SourceInstanceId && item.Record.SourceRunId == Anchor.Record.SourceRunId
        && item.Record.SourceParcelId == Anchor.Record.SourceParcelId
        && (Anchor.Provider.Length > 0 ? item.Provider == Anchor.Provider : item.Type == Anchor.Type)
        && item.Record.OccurredAt >= Start.Record.OccurredAt && item.Record.OccurredAt <= Terminal.Record.OccurredAt
        && (item.Type == "other-api" || item.Type == Anchor.Type);
}
