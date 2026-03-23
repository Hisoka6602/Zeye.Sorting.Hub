using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 自动验证结构化结果构造器。
/// </summary>
public static class AutoTuningVerificationResultBuilder {
    /// <summary>
    /// 构建自动验证结果。
    /// </summary>
    public static AutoTuningVerificationResult Build(
        bool regressed,
        bool severeRegressed,
        string reason,
        decimal p95IncreasePercent,
        decimal p99IncreasePercent,
        decimal errorRateIncreasePercent,
        decimal timeoutRateIncreasePercent,
        int deadlockIncreaseCount,
        int? lockWaitBaseline,
        int? lockWaitCurrent,
        PlanRegressionSnapshot planRegression,
        bool lockWaitUnavailable,
        AutoTuningUnavailableReason lockWaitUnavailableReason) {
        var verdict = BuildVerdict(regressed, severeRegressed);
        var lockWaitStatus = lockWaitUnavailable ? "unavailable" : "available";
        var lockWaitReason = lockWaitUnavailable ? lockWaitUnavailableReason.ToTagValue() : AutoTuningUnavailableReason.Sampled.ToTagValue();
        var planStatus = planRegression.IsAvailable ? (planRegression.IsRegressed ? "regressed" : "pass") : "unavailable";
        var planReason = planRegression.IsAvailable ? "probe-sampled" : planRegression.UnavailableReason;
        return new AutoTuningVerificationResult(
            Verdict: verdict,
            Reason: reason,
            SnapshotDiff: [
                BuildPercentMetric("p95", p95IncreasePercent),
                BuildPercentMetric("p99", p99IncreasePercent),
                BuildPercentMetric("error-rate", errorRateIncreasePercent),
                BuildPercentMetric("timeout-rate", timeoutRateIncreasePercent),
                new AutoTuningVerificationMetricDiff(
                    Name: "deadlock",
                    Status: deadlockIncreaseCount > 0 ? "regressed" : "pass",
                    Baseline: "0",
                    Current: deadlockIncreaseCount.ToString(),
                    Delta: deadlockIncreaseCount.ToString(),
                    Reason: deadlockIncreaseCount > 0 ? "deadlock increased" : "stable"),
                new AutoTuningVerificationMetricDiff(
                    Name: "lock-wait",
                    Status: lockWaitStatus,
                    Baseline: lockWaitBaseline?.ToString() ?? "unavailable",
                    Current: lockWaitCurrent?.ToString() ?? "unavailable",
                    Delta: CalculateLockWaitDelta(lockWaitBaseline, lockWaitCurrent, lockWaitUnavailable),
                    Reason: lockWaitReason),
                new AutoTuningVerificationMetricDiff(
                    Name: "plan-regression",
                    Status: planStatus,
                    Baseline: "n/a",
                    Current: planRegression.Summary,
                    Delta: planRegression.IsRegressed ? "regressed" : "stable",
                    Reason: planReason)
            ]);
    }

    private static string BuildVerdict(bool regressed, bool severeRegressed) {
        if (severeRegressed) {
            return "severe-regressed";
        }

        if (regressed) {
            return "regressed";
        }

        return "pass";
    }

    private static string CalculateLockWaitDelta(int? lockWaitBaseline, int? lockWaitCurrent, bool lockWaitUnavailable) {
        if (lockWaitUnavailable || !lockWaitBaseline.HasValue || !lockWaitCurrent.HasValue) {
            return "unavailable";
        }

        return (lockWaitCurrent.Value - lockWaitBaseline.Value).ToString();
    }

    private static AutoTuningVerificationMetricDiff BuildPercentMetric(string name, decimal increasePercent) {
        return new AutoTuningVerificationMetricDiff(
            Name: name,
            Status: increasePercent > 0m ? "regressed" : "pass",
            Baseline: "0%",
            Current: $"{increasePercent:F2}%",
            Delta: $"{increasePercent:F2}%",
            Reason: increasePercent > 0m ? $"{name} increased" : "stable");
    }
}
