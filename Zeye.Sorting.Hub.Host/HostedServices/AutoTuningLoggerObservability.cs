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
        /// 将指标数值写入 NLog Info 日志，确保指标落盘（所有业务日志必须落盘）。
        /// </summary>
        public void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null) {
            Logger.Info("AutoTuningMetric: Name={Name}, Value={Value}, Tags={Tags}", name, value, FormatTags(tags));
        }

        /// <summary>
        /// 将 NLog 日志级别事件写入 NLog 日志器。
        /// </summary>
        public void EmitEvent(string name, NLog.LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
            Logger.Log(level, "AutoTuningEvent: Name={Name}, Message={Message}, Tags={Tags}", name, message, FormatTags(tags));
        }

        /// <summary>
        /// 将标签字典格式化为可读字符串。
        /// 采用手写循环代替 LINQ，减少热路径内存分配。
        /// </summary>
        private static string FormatTags(IReadOnlyDictionary<string, string>? tags) {
            if (tags is null || tags.Count == 0) {
                return string.Empty;
            }

            // 步骤 1：预估容量（每个条目约 32 字符），避免 StringBuilder 多次扩容。
            var sb = new System.Text.StringBuilder(tags.Count * 32);
            var first = true;
            foreach (var pair in tags) {
                if (!first) {
                    sb.Append(", ");
                }

                sb.Append(pair.Key).Append('=').Append(pair.Value);
                first = false;
            }

            // 步骤 2：返回最终字符串。
            return sb.ToString();
        }
    }
}
