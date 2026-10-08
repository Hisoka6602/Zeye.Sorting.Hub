namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>量测缺少条码时按明确包裹编号校验来源，避免读取完整包裹或整段日期总体。</summary>
public sealed record ParcelDwsParcelIdentitySnapshot {
    /// <summary>中心包裹编号。</summary>
    public long Id { get; init; }
    /// <summary>不可变的首次入库时间。</summary>
    public DateTime CreatedTime { get; init; }
    /// <summary>来源实例。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>来源运行会话。</summary>
    public string? SourceRunId { get; init; }
    /// <summary>来源包裹编号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>已持久化主条码。</summary>
    public string BarCodes { get; init; } = string.Empty;
    /// <summary>来源工作台。</summary>
    public string WorkstationName { get; init; } = string.Empty;
}
