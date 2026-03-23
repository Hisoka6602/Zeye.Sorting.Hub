using Zeye.Sorting.Hub.Domain.Enums.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// Parcel 分表策略配置快照（用于审计与守卫复用）。
/// </summary>
public readonly record struct ParcelShardingStrategyConfigSnapshot {
    /// <summary>
    /// 初始化分表策略配置快照。
    /// </summary>
    /// <param name="Mode">策略模式。</param>
    /// <param name="TimeGranularity">时间粒度。</param>
    /// <param name="ThresholdAction">阈值动作。</param>
    /// <param name="MaxRowsPerShard">单分表最大行数阈值。</param>
    /// <param name="HotThresholdRatio">热点阈值。</param>
    /// <param name="VolumeObservation">容量观测输入。</param>
    /// <param name="FinerGranularity">finer-granularity 配置快照。</param>
    public ParcelShardingStrategyConfigSnapshot(
        ParcelShardingStrategyMode Mode,
        ParcelTimeShardingGranularity TimeGranularity,
        ParcelVolumeThresholdAction ThresholdAction,
        long? MaxRowsPerShard,
        decimal? HotThresholdRatio,
        ParcelShardingVolumeObservation VolumeObservation,
        ParcelFinerGranularityStrategySnapshot FinerGranularity) {
        this.Mode = Mode;
        this.TimeGranularity = TimeGranularity;
        this.ThresholdAction = ThresholdAction;
        this.MaxRowsPerShard = MaxRowsPerShard;
        this.HotThresholdRatio = HotThresholdRatio;
        this.VolumeObservation = VolumeObservation;
        this.FinerGranularity = FinerGranularity;
    }

    /// <summary>
    /// 策略模式。
    /// </summary>
    public ParcelShardingStrategyMode Mode { get; init; }

    /// <summary>
    /// 时间粒度。
    /// </summary>
    public ParcelTimeShardingGranularity TimeGranularity { get; init; }

    /// <summary>
    /// 阈值动作。
    /// </summary>
    public ParcelVolumeThresholdAction ThresholdAction { get; init; }

    /// <summary>
    /// 单分表最大行数阈值。
    /// </summary>
    public long? MaxRowsPerShard { get; init; }

    /// <summary>
    /// 热点阈值。
    /// </summary>
    public decimal? HotThresholdRatio { get; init; }

    /// <summary>
    /// 容量观测输入。
    /// </summary>
    public ParcelShardingVolumeObservation VolumeObservation { get; init; }

    /// <summary>
    /// finer-granularity 配置快照。
    /// </summary>
    public ParcelFinerGranularityStrategySnapshot FinerGranularity { get; init; }
}
