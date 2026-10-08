namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>按来源和目标、实际格口编码隔离的流向分布。</summary>
public sealed record ParcelChuteRouteResponse {
    /// <summary>来源实例，避免不同设备的同名格口混淆。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>来源工作台。</summary>
    public required string WorkstationName { get; init; }
    /// <summary>目标格口原始编码，缺失时为空。</summary>
    public string? TargetChuteCode { get; init; }
    /// <summary>实际格口原始编码，缺失时为空。</summary>
    public string? ActualChuteCode { get; init; }
    /// <summary>该流向包裹数量。</summary>
    public long Count { get; init; }
    /// <summary>明确采用兜底格口的票数。</summary>
    public long FallbackCount { get; init; }
}
