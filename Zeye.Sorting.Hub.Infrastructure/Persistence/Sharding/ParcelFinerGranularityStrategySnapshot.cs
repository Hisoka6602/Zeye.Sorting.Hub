using Zeye.Sorting.Hub.Domain.Enums.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// Parcel finer-granularity 策略配置快照。
/// </summary>
public readonly record struct ParcelFinerGranularityStrategySnapshot {
    /// <summary>
    /// 初始化 finer-granularity 配置快照。
    /// </summary>
    /// <param name="ModeWhenPerDayStillHot">当 PerDay 仍过热时推荐的下一层细粒度模式。</param>
    /// <param name="Lifecycle">扩展治理生命周期（仅计划/仅告警/未来可执行）。</param>
    /// <param name="RequirePrebuildGuard">是否要求治理守卫执行预建约束。</param>
    /// <param name="BucketCount">当模式为 BucketedPerDay 时建议的桶数量。</param>
    public ParcelFinerGranularityStrategySnapshot(
        ParcelFinerGranularityMode ModeWhenPerDayStillHot,
        ParcelFinerGranularityPlanLifecycle Lifecycle,
        bool RequirePrebuildGuard,
        int? BucketCount) {
        this.ModeWhenPerDayStillHot = ModeWhenPerDayStillHot;
        this.Lifecycle = Lifecycle;
        this.RequirePrebuildGuard = RequirePrebuildGuard;
        this.BucketCount = BucketCount;
    }

    /// <summary>
    /// 当 PerDay 仍过热时推荐的下一层细粒度模式。
    /// </summary>
    public ParcelFinerGranularityMode ModeWhenPerDayStillHot { get; init; }

    /// <summary>
    /// 扩展治理生命周期（仅计划/仅告警/未来可执行）。
    /// </summary>
    public ParcelFinerGranularityPlanLifecycle Lifecycle { get; init; }

    /// <summary>
    /// 是否要求治理守卫执行预建约束。
    /// </summary>
    public bool RequirePrebuildGuard { get; init; }

    /// <summary>
    /// 当模式为 BucketedPerDay 时建议的桶数量。
    /// </summary>
    public int? BucketCount { get; init; }
}
