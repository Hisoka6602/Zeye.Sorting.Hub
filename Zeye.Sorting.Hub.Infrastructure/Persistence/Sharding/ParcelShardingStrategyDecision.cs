using EFCore.Sharding;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// Parcel 分表策略决策快照。
/// </summary>
/// <param name="Mode">策略模式。</param>
/// <param name="TimeGranularity">时间粒度。</param>
/// <param name="ThresholdAction">阈值动作。</param>
/// <param name="ThresholdReached">是否命中容量阈值。</param>
/// <param name="EffectiveDateMode">最终用于注册的时间分表粒度。</param>
/// <param name="FinerGranularityExtensionPlan">下一层细粒度扩展规划结果。</param>
/// <param name="Reason">决策原因摘要。</param>
/// <param name="ConfigSnapshot">策略配置快照。</param>
public readonly record struct ParcelShardingStrategyDecision(
    ParcelShardingStrategyMode Mode,
    ParcelTimeShardingGranularity TimeGranularity,
    ParcelVolumeThresholdAction ThresholdAction,
    ParcelShardingVolumeObservation VolumeObservation,
    bool ThresholdReached,
    ExpandByDateMode EffectiveDateMode,
    ParcelFinerGranularityExtensionPlan FinerGranularityExtensionPlan,
    string Reason,
    ParcelShardingStrategyConfigSnapshot ConfigSnapshot);
