using Zeye.Sorting.Hub.Domain.Enums.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>
/// Parcel finer-granularity 扩展规划结果。
/// </summary>
public readonly record struct ParcelFinerGranularityExtensionPlan {
    /// <summary>
    /// 初始化 finer-granularity 扩展规划结果。
    /// </summary>
    /// <param name="ShouldPlanExtension">是否需要规划下一层细粒度扩展。</param>
    /// <param name="SuggestedMode">建议的下一层细粒度模式。</param>
    /// <param name="Lifecycle">扩展治理生命周期（仅计划/仅告警/未来可执行）。</param>
    /// <param name="RequiresPrebuildGuard">是否需要预建守卫。</param>
    /// <param name="Reason">规划原因。</param>
    public ParcelFinerGranularityExtensionPlan(
        bool ShouldPlanExtension,
        ParcelFinerGranularityMode SuggestedMode,
        ParcelFinerGranularityPlanLifecycle Lifecycle,
        bool RequiresPrebuildGuard,
        string Reason) {
        this.ShouldPlanExtension = ShouldPlanExtension;
        this.SuggestedMode = SuggestedMode;
        this.Lifecycle = Lifecycle;
        this.RequiresPrebuildGuard = RequiresPrebuildGuard;
        this.Reason = Reason;
    }

    /// <summary>
    /// 是否需要规划下一层细粒度扩展。
    /// </summary>
    public bool ShouldPlanExtension { get; init; }

    /// <summary>
    /// 建议的下一层细粒度模式。
    /// </summary>
    public ParcelFinerGranularityMode SuggestedMode { get; init; }

    /// <summary>
    /// 扩展治理生命周期（仅计划/仅告警/未来可执行）。
    /// </summary>
    public ParcelFinerGranularityPlanLifecycle Lifecycle { get; init; }

    /// <summary>
    /// 是否需要预建守卫。
    /// </summary>
    public bool RequiresPrebuildGuard { get; init; }

    /// <summary>
    /// 规划原因。
    /// </summary>
    public string Reason { get; init; }
}
