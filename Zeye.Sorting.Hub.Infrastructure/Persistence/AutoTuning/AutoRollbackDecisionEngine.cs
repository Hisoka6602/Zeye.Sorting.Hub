namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 自动回滚决策。
/// </summary>
public static class AutoRollbackDecisionEngine {
    /// <summary>
    /// 评估自动回滚结果。
    /// </summary>
    public static RegressionEvaluationResult Evaluate(
        decimal p99IncreasePercent,
        decimal timeoutRateIncreasePercent,
        string lockWaitStatus,
        decimal severeRollbackP99IncreasePercent,
        decimal severeRollbackTimeoutIncreasePercent,
        bool regressed,
        string reason) {
        var severeRegression = (severeRollbackP99IncreasePercent > 0m && p99IncreasePercent >= severeRollbackP99IncreasePercent)
            || (severeRollbackTimeoutIncreasePercent > 0m && timeoutRateIncreasePercent >= severeRollbackTimeoutIncreasePercent);
        return new RegressionEvaluationResult(regressed, severeRegression, reason, lockWaitStatus);
    }
}
