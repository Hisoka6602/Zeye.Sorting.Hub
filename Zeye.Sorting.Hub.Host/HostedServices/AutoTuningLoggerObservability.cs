using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>自动调优观测默认日志实现（可被 Prometheus/Otel 实现替换）。</summary>
    public sealed class AutoTuningLoggerObservability : IAutoTuningObservability {
        /// <summary>
        /// 日志记录器实例，用于输出自动调优观测指标与事件。
        /// </summary>
        private readonly ILogger _logger;

        public AutoTuningLoggerObservability() {
            _logger = LogManager.GetCurrentClassLogger();
        }

        /// <summary>
        /// 执行逻辑：EmitMetric。
        /// </summary>
        public void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null) {
            _logger.Debug("AutoTuningMetric: Name={Name}, Value={Value}, Tags={Tags}", name, value, FormatTags(tags));
        }

        /// <summary>
        /// 执行逻辑：EmitEvent。
        /// </summary>
        public void EmitEvent(string name, Microsoft.Extensions.Logging.LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
            var eventLevel = ConvertLogLevel(level);
            _logger.Log(eventLevel, "AutoTuningEvent: Name={Name}, Message={Message}, Tags={Tags}", name, message, FormatTags(tags));
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
        /// 执行逻辑：ConvertLogLevel。
        /// </summary>
        private static LogLevel ConvertLogLevel(Microsoft.Extensions.Logging.LogLevel level) {
            return level switch {
                Microsoft.Extensions.Logging.LogLevel.Trace => LogLevel.Trace,
                Microsoft.Extensions.Logging.LogLevel.Debug => LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Information => LogLevel.Info,
                Microsoft.Extensions.Logging.LogLevel.Warning => LogLevel.Warn,
                Microsoft.Extensions.Logging.LogLevel.Error => LogLevel.Error,
                Microsoft.Extensions.Logging.LogLevel.Critical => LogLevel.Fatal,
                _ => LogLevel.Info
            };
        }
    }
}
