namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>包裹分析的本地日期总体和可分页下钻条件。</summary>
public sealed record ParcelAnalysisRequest {
    /// <summary>分析类型：exceptions、duration 或 chutes。</summary>
    public string View { get; init; } = "exceptions";
    /// <summary>耗时类型：completion、dws、routing、sorting、scan-upload、chute-request、landing-report、image-upload、other-api。</summary>
    public string DurationType { get; init; } = "completion";
    /// <summary>显式刷新扩展耗时统计的短时快照。</summary>
    public bool RefreshDurationSnapshot { get; init; }
    /// <summary>首次入库起始日期，包含当天。</summary>
    public DateTime FromDate { get; init; }
    /// <summary>首次入库结束日期，包含当天。</summary>
    public DateTime ToDate { get; init; }
    /// <summary>精确匹配来源工作台，为空时查询全部。</summary>
    public string? WorkstationName { get; init; }
    /// <summary>精确匹配来源实例，为空时查询全部。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>异常页下钻范围：exception、noread 或 blocked。</summary>
    public string Issue { get; init; } = "exception";
    /// <summary>异常类型数值，仅用于异常票下钻。</summary>
    public int? ExceptionType { get; init; }
    /// <summary>有效完成耗时下限，单位毫秒，包含边界。</summary>
    public long? MinimumMilliseconds { get; init; }
    /// <summary>有效完成耗时上限，单位毫秒，不包含边界。</summary>
    public long? MaximumMilliseconds { get; init; }
    /// <summary>格口页只下钻目标与实际编码不一致的包裹。</summary>
    public bool MismatchOnly { get; init; }
    /// <summary>格口页只下钻明确采用兜底格口的包裹。</summary>
    public bool FallbackOnly { get; init; }
    /// <summary>下钻的目标格口原始编码。</summary>
    public string? TargetChuteCode { get; init; }
    /// <summary>下钻的实际格口原始编码。</summary>
    public string? ActualChuteCode { get; init; }
    /// <summary>下钻包裹页码，每页固定20票。</summary>
    public int PageNumber { get; init; } = 1;
}
