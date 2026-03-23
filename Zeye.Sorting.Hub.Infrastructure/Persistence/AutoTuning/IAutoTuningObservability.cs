using Microsoft.Extensions.Logging;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 自动调优观测输出抽象（日志/指标统一入口）。
/// </summary>
public interface IAutoTuningObservability {
    void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null);
    void EmitEvent(string name, LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null);
}
