using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 自动调优观测输出抽象（日志/指标统一入口）。
/// </summary>
public interface IAutoTuningObservability {
    /// <summary>
    /// 发送数值指标。
    /// </summary>
    /// <param name="name">指标名称。</param>
    /// <param name="value">指标数值。</param>
    /// <param name="tags">可选标签集合。</param>
    void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null);

    /// <summary>
    /// 发送事件日志。
    /// </summary>
    /// <param name="name">事件名称。</param>
    /// <param name="level">NLog 日志级别。</param>
    /// <param name="message">事件消息。</param>
    /// <param name="tags">可选标签集合。</param>
    void EmitEvent(string name, LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null);
}
