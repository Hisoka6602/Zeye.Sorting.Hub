namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// Parcel 容量阈值观测输入（结构化入口，便于未来接入数据库统计或监控采集）。
/// </summary>
public readonly record struct ParcelShardingVolumeObservation {
    /// <summary>
    /// 初始化容量观测输入。
    /// </summary>
    /// <param name="Source">观测数据来源标识。</param>
    /// <param name="EstimatedRowsPerShard">单分表估算行数观测值。</param>
    /// <param name="ObservedHotRatio">热点比例观测值（0~1）。</param>
    public ParcelShardingVolumeObservation(string Source, long? EstimatedRowsPerShard, decimal? ObservedHotRatio) {
        this.Source = Source;
        this.EstimatedRowsPerShard = EstimatedRowsPerShard;
        this.ObservedHotRatio = ObservedHotRatio;
    }

    /// <summary>
    /// 观测数据来源标识。
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// 单分表估算行数观测值。
    /// </summary>
    public long? EstimatedRowsPerShard { get; init; }

    /// <summary>
    /// 热点比例观测值（0~1）。
    /// </summary>
    public decimal? ObservedHotRatio { get; init; }
}
