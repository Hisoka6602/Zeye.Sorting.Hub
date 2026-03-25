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

    /// <summary>
    /// 生成验证结论：严重回归优先，其次普通回归，其他场景为通过。
    /// </summary>
    /// <param name="regressed">是否回归。</param>
    /// <param name="severeRegressed">是否严重回归。</param>
    /// <returns>验证结论标识。</returns>
    private static string BuildVerdict(bool regressed, bool severeRegressed) {
        if (severeRegressed) {
            return "severe-regressed";
        }

        if (regressed) {
            return "regressed";
        }

        return "pass";
    }

    /// <summary>
    /// 计算锁等待增量值，不可用或样本缺失时返回 unavailable。
    /// </summary>
    /// <param name="lockWaitBaseline">基线锁等待值。</param>
    /// <param name="lockWaitCurrent">当前锁等待值。</param>
    /// <param name="lockWaitUnavailable">锁等待指标是否不可用。</param>
    /// <returns>锁等待增量文本。</returns>
    private static string CalculateLockWaitDelta(int? lockWaitBaseline, int? lockWaitCurrent, bool lockWaitUnavailable) {
        if (lockWaitUnavailable || !lockWaitBaseline.HasValue || !lockWaitCurrent.HasValue) {
            return "unavailable";
        }

        return (lockWaitCurrent.Value - lockWaitBaseline.Value).ToString();
    }

    /// <summary>
    /// 构建百分比指标差异项。
    /// 步骤说明：
    /// 1. 根据增幅是否大于 0 判定状态为 regressed 或 pass；
    /// 2. 统一按两位小数格式化 Current 与 Delta；
    /// 3. 生成稳定或增长原因说明。
    /// </summary>
    /// <param name="name">指标名称。</param>
    /// <param name="increasePercent">指标增幅百分比。</param>
    /// <returns>结构化百分比指标差异项。</returns>
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
