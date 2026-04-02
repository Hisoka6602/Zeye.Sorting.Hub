using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 默认空实现（允许在未注册观测实现时保持兼容）。
/// </summary>
public sealed class NullAutoTuningObservability : IAutoTuningObservability {
    /// <summary>
    /// 空实现：不输出指标观测数据，用于禁用观测链路时保持调用兼容。
    /// </summary>
    public void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null) {
    }

    /// <summary>
    /// 空实现：不输出事件观测数据，用于禁用观测链路时保持调用兼容。
    /// </summary>
    public void EmitEvent(string name, LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
    }
}
