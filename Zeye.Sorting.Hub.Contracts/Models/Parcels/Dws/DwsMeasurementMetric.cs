namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;

/// <summary>单个量测的完整有效总体统计，缺失值不补零。</summary>
public sealed record DwsMeasurementMetric {
    /// <summary>有效量测次数。</summary>
    public int Count { get; init; }
    /// <summary>最小值。</summary>
    public decimal? Minimum { get; init; }
    /// <summary>最大值。</summary>
    public decimal? Maximum { get; init; }
    /// <summary>中位数。</summary>
    public decimal? Median { get; init; }
    /// <summary>全部有效样本的平均值。</summary>
    public decimal? Average { get; init; }
    /// <summary>全部有效样本的最近秩P95。</summary>
    public decimal? P95 { get; init; }
    /// <summary>最大值与最小值之差，至少两次才可比较。</summary>
    public decimal? Spread { get; init; }
    /// <summary>差值占中位数的百分比，零基准保持未知。</summary>
    public decimal? SpreadPercent { get; init; }
    /// <summary>用户输入的当前标准值。</summary>
    public decimal? Reference { get; init; }
    /// <summary>对标准值的最大绝对偏差。</summary>
    public decimal? MaximumReferenceDeviation { get; init; }
    /// <summary>对标准值的最大相对偏差。</summary>
    public decimal? MaximumReferenceDeviationPercent { get; init; }
}
