using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 执行计划探针测试桩：记录调用次数并返回可用结果。
/// </summary>
internal sealed class CountingPlanProbe : IExecutionPlanRegressionProbe {
    /// <summary>
    /// 收集 Evaluate 调用次数，用于断言探针可用/不可用分支的执行频率。
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// 验证场景：Evaluate。
    /// </summary>
    public PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint) {
        CallCount++;
        return new(true, false, $"probe available: {providerName}/{sqlFingerprint}", "none");
    }
}
