namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 执行计划回退快照（默认 unavailable）。
/// </summary>
public sealed record PlanRegressionSnapshot(
    bool IsAvailable,
    bool IsRegressed,
    string Summary,
    string UnavailableReason);
