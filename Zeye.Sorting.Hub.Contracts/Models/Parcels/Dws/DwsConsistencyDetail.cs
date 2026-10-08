namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;

/// <summary>条码下钻明细与有界趋势，分页不改变完整统计。</summary>
public sealed record DwsConsistencyDetail {
    /// <summary>完整条码总体统计。</summary>
    public DwsConsistencyGroup Summary { get; init; } = new();
    /// <summary>完整测量次数。</summary>
    public int MeasurementCount { get; init; }
    /// <summary>当前20条测量明细。</summary>
    public IReadOnlyList<DwsMeasurementSample> Items { get; init; } = [];
    /// <summary>最多200个按设备时间排列的真实测量点。</summary>
    public IReadOnlyList<DwsMeasurementSample> Trend { get; init; } = [];
    /// <summary>趋势是否抽取了真实代表点，明细仍包含全部。</summary>
    public bool TrendTruncated { get; init; }
    /// <summary>按真实扫码完成时间抽取的最多200个原始点，不受设备测量时间缺失影响。</summary>
    public IReadOnlyList<DwsMeasurementSample> ScanTrend { get; init; } = [];
    /// <summary>扫码耗时趋势是否抽样，统计始终使用全部有效样本。</summary>
    public bool ScanTrendTruncated { get; init; }
    /// <summary>当前条码各来源统计。</summary>
    public IReadOnlyList<DwsConsistencySource> Sources { get; init; } = [];
}
