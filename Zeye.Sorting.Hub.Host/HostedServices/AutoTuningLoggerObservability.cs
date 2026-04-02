using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>自动调优观测默认日志实现（可被 Prometheus/Otel 实现替换）。</summary>
    public sealed class AutoTuningLoggerObservability : IAutoTuningObservability {
        /// <summary>
        /// NLog 静态日志器实例，用于输出自动调优观测指标与事件。
        /// </summary>
        private static readonly NLog.ILogger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 执行逻辑：EmitMetric。
        /// </summary>
        public void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null) {
            Logger.Debug("AutoTuningMetric: Name={Name}, Value={Value}, Tags={Tags}", name, value, FormatTags(tags));
        }

        /// <summary>
        /// 将 NLog 日志级别事件写入 NLog 日志器。
        /// </summary>
        public void EmitEvent(string name, NLog.LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
            Logger.Log(level, "AutoTuningEvent: Name={Name}, Message={Message}, Tags={Tags}", name, message, FormatTags(tags));
        }

        /// <summary>
        /// 将标签字典格式化为可读字符串。
        /// </summary>
        private static string FormatTags(IReadOnlyDictionary<string, string>? tags) {
            if (tags is null || tags.Count == 0) {
                return string.Empty;
            }

            return string.Join(", ", tags.Select(static pair => $"{pair.Key}={pair.Value}"));
        }
    }
}
