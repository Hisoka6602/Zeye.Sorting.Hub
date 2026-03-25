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
        /// 执行逻辑：EmitEvent。
        /// </summary>
        public void EmitEvent(string name, Microsoft.Extensions.Logging.LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
            var eventLevel = ConvertLogLevel(level);
            Logger.Log(eventLevel, "AutoTuningEvent: Name={Name}, Message={Message}, Tags={Tags}", name, message, FormatTags(tags));
        }

        /// <summary>
        /// 执行逻辑：FormatTags。
        /// </summary>
        private static string FormatTags(IReadOnlyDictionary<string, string>? tags) {
            if (tags is null || tags.Count == 0) {
                return string.Empty;
            }

            return string.Join(", ", tags.Select(static pair => $"{pair.Key}={pair.Value}"));
        }

        /// <summary>
        /// 将 Microsoft.Extensions.Logging.LogLevel 枚举转换为 NLog.LogLevel 枚举。
        /// </summary>
        private static NLog.LogLevel ConvertLogLevel(Microsoft.Extensions.Logging.LogLevel level) {
            return level switch {
                Microsoft.Extensions.Logging.LogLevel.Trace => NLog.LogLevel.Trace,
                Microsoft.Extensions.Logging.LogLevel.Debug => NLog.LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Information => NLog.LogLevel.Info,
                Microsoft.Extensions.Logging.LogLevel.Warning => NLog.LogLevel.Warn,
                Microsoft.Extensions.Logging.LogLevel.Error => NLog.LogLevel.Error,
                Microsoft.Extensions.Logging.LogLevel.Critical => NLog.LogLevel.Fatal,
                _ => NLog.LogLevel.Info
            };
        }
    }
}
