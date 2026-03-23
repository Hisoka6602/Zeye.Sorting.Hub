namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 自动验证单项指标对比快照。
/// </summary>
public sealed record AutoTuningVerificationMetricDiff(
    string Name,
    string Status,
    string Baseline,
    string Current,
    string Delta,
    string Reason);
