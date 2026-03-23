namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 自动验证标准化输出结构。
/// </summary>
public sealed record AutoTuningVerificationResult(
    string Verdict,
    string Reason,
    IReadOnlyList<AutoTuningVerificationMetricDiff> SnapshotDiff);
