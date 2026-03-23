using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 执行计划探针测试桩：始终返回可用且无回归。
/// </summary>
internal sealed class FixedPlanProbe : IExecutionPlanRegressionProbe {
    /// <summary>
    /// 验证场景：Evaluate。
    /// </summary>
    public PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint) =>
        new(true, false, $"probe available: {providerName}/{sqlFingerprint}", "none");
}
