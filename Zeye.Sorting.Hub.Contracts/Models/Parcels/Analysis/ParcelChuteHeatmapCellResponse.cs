namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>同一来源、工作台和原始格口编码的完整聚合，不依赖流向表的返回上限。</summary>
public sealed record ParcelChuteHeatmapCellResponse {
    /// <summary>来源实例，同名格口不跨来源合并。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>来源工作台，同名格口不跨工作台合并。</summary>
    public required string WorkstationName { get; init; }
    /// <summary>有效的原始格口编码，保留前导零和非数字字符。</summary>
    public required string ChuteCode { get; init; }
    /// <summary>当前格口包裹票数。</summary>
    public long Count { get; init; }
    /// <summary>目标和实际编码均有效且忽略大小写、首尾空格后不一致的票数。</summary>
    public long MismatchCount { get; init; }
    /// <summary>包裹明确记录采用兜底格口的票数。</summary>
    public long FallbackCount { get; init; }
}
