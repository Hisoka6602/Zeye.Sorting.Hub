using Microsoft.Extensions.Configuration;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// 分表容量风险快照服务。
/// </summary>
public sealed class ShardingCapacitySnapshotService {
    /// <summary>
    /// 容量预警阈值比例。
    /// </summary>
    private const decimal CapacityWarningThresholdRatio = 0.8m;

    /// <summary>
    /// 热点比例预警阈值比例。
    /// </summary>
    private const decimal HotRatioWarningThresholdRatio = 0.8m;

    /// <summary>
    /// 配置根。
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 初始化分表容量风险快照服务。
    /// </summary>
    /// <param name="configuration">配置根。</param>
    public ShardingCapacitySnapshotService(IConfiguration configuration) {
        _configuration = configuration;
    }

    /// <summary>
    /// 构建容量与热点风险描述。
    /// </summary>
    /// <returns>风险描述集合。</returns>
    public IReadOnlyList<string> BuildWarnings() {
        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(_configuration);
        var decision = evaluation.Decision;
        var warnings = new List<string>();
        if (evaluation.ValidationErrors.Count > 0) {
            warnings.Add($"分表策略配置校验失败：{string.Join(" | ", evaluation.ValidationErrors)}");
            return warnings;
        }

        if (decision.ThresholdReached) {
            warnings.Add($"Parcel 分表容量或热点阈值已触发：{decision.Reason}");
        }

        var maxRowsPerShard = decision.ConfigSnapshot.MaxRowsPerShard;
        var estimatedRowsPerShard = decision.VolumeObservation.EstimatedRowsPerShard;
        if (maxRowsPerShard.HasValue && estimatedRowsPerShard.HasValue && estimatedRowsPerShard.Value >= maxRowsPerShard.Value * CapacityWarningThresholdRatio) {
            warnings.Add($"Parcel 单分表估算行数接近阈值：EstimatedRowsPerShard={estimatedRowsPerShard.Value}, MaxRowsPerShard={maxRowsPerShard.Value}");
        }

        var hotThresholdRatio = decision.ConfigSnapshot.HotThresholdRatio;
        var observedHotRatio = decision.VolumeObservation.ObservedHotRatio;
        if (hotThresholdRatio.HasValue && observedHotRatio.HasValue && observedHotRatio.Value >= hotThresholdRatio.Value * HotRatioWarningThresholdRatio) {
            warnings.Add($"Parcel 分表热点比例接近阈值：ObservedHotRatio={observedHotRatio.Value:F2}, HotThresholdRatio={hotThresholdRatio.Value:F2}");
        }

        return warnings;
    }
}
