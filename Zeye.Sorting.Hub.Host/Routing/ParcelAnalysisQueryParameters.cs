namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>包裹分析只读接口的查询字符串参数。</summary>
public sealed class ParcelAnalysisQueryParameters {
    /// <summary>分析类型：exceptions、duration、chutes。</summary>
    public string? View { get; init; }
    /// <summary>耗时类型，默认 completion；DWS、阶段及接口调用分别统计。</summary>
    public string? DurationType { get; init; }
    /// <summary>绕过扩展耗时短时快照，重新读取已持久化事实。</summary>
    public bool? RefreshDurationSnapshot { get; init; }
    /// <summary>包含起始日的本地日期，格式yyyy-MM-dd。</summary>
    public string? FromDate { get; init; }
    /// <summary>包含结束日的本地日期，格式yyyy-MM-dd。</summary>
    public string? ToDate { get; init; }
    /// <summary>工作台精确筛选。</summary>
    public string? WorkstationName { get; init; }
    /// <summary>来源实例精确筛选。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>异常页下钻范围。</summary>
    public string? Issue { get; init; }
    /// <summary>异常类型数值。</summary>
    public int? ExceptionType { get; init; }
    /// <summary>完成耗时包含下限，单位毫秒。</summary>
    public long? MinimumMilliseconds { get; init; }
    /// <summary>完成耗时不包含上限，单位毫秒。</summary>
    public long? MaximumMilliseconds { get; init; }
    /// <summary>是否只查看格口编码不一致的包裹。</summary>
    public bool? MismatchOnly { get; init; }
    /// <summary>是否只查看明确采用兜底格口的包裹。</summary>
    public bool? FallbackOnly { get; init; }
    /// <summary>下钻目标格口编码。</summary>
    public string? TargetChuteCode { get; init; }
    /// <summary>下钻实际格口编码。</summary>
    public string? ActualChuteCode { get; init; }
    /// <summary>当前下钻页码。</summary>
    public int? PageNumber { get; init; }
}
