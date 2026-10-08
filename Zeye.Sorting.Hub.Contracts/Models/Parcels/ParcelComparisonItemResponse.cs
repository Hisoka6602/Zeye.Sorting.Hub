namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>单票对比投影；量测沿用包裹当前持久化快照，不补造缺失值。</summary>
public sealed record ParcelComparisonItemResponse {
    /// <summary>身份、真实动作及精简调用窗口。</summary>
    public required ParcelTimingParcelResponse Timing { get; init; }
    /// <summary>重量，单位kg。</summary>
    public decimal? Weight { get; init; }
    /// <summary>长度，单位mm。</summary>
    public decimal? Length { get; init; }
    /// <summary>宽度，单位mm。</summary>
    public decimal? Width { get; init; }
    /// <summary>高度，单位mm。</summary>
    public decimal? Height { get; init; }
    /// <summary>已保存的物理体积，单位mm³，不由尺寸推算。</summary>
    public decimal? Volume { get; init; }
    /// <summary>原始目标格口编码，保留前导零。</summary>
    public string? TargetChuteCode { get; init; }
    /// <summary>实际格口编码，保留前导零。</summary>
    public string? ActualChuteCode { get; init; }
}
