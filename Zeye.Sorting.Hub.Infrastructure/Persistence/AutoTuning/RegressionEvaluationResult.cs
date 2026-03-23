namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 回归检测结果（支持 unavailable 标记）。
/// </summary>
public sealed record RegressionEvaluationResult(
    bool IsRegressed,
    bool IsSevereRegression,
    string Reason,
    string LockWaitStatus);
