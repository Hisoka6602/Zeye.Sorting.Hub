using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>自动调优配置读取辅助方法（集中管理，避免多处影分身副本）。</summary>
    public static class AutoTuningConfigurationHelper {
        private const string AutoTuningConfigPrefix = "Persistence:AutoTuning";
        private const string AutonomousConfigPrefix = "Persistence:AutoTuning:Autonomous";

        /// <summary>构建 AutoTuning 配置全路径键名。</summary>
        public static string BuildAutoTuningKey(string suffix) => $"{AutoTuningConfigPrefix}:{suffix}";

        /// <summary>构建 Autonomous 配置全路径键名。</summary>
        public static string BuildAutonomousKey(string suffix) => $"{AutonomousConfigPrefix}:{suffix}";

        /// <summary>读取正整数配置，非法值回退默认值。</summary>
        public static int GetPositiveIntOrDefault(IConfiguration configuration, string key, int fallback) {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        }

        /// <summary>读取非负整数配置，非法值回退默认值。</summary>
        public static int GetNonNegativeIntOrDefault(IConfiguration configuration, string key, int fallback) {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : fallback;
        }

        /// <summary>读取非负小数配置，非法值回退默认值。</summary>
        public static decimal GetNonNegativeDecimalOrDefault(IConfiguration configuration, string key, decimal fallback) {
            var value = configuration[key];
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0m ? parsed : fallback;
        }

        /// <summary>读取指定区间内的小数配置，非法值回退默认值。</summary>
        public static decimal GetDecimalInRangeOrDefault(IConfiguration configuration, string key, decimal fallback, decimal min, decimal max) {
            var value = configuration[key];
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) {
                return fallback;
            }

            if (parsed < min || parsed > max) {
                return fallback;
            }

            return parsed;
        }

        /// <summary>读取小数配置，非法值回退默认值，合法值按区间钳制。</summary>
        public static decimal GetDecimalClampedOrDefault(IConfiguration configuration, string key, decimal fallback, decimal min, decimal max) {
            var value = configuration[key];
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) {
                return fallback;
            }

            return decimal.Clamp(parsed, min, max);
        }

        /// <summary>读取布尔配置，非法值回退默认值。</summary>
        public static bool GetBoolOrDefault(IConfiguration configuration, string key, bool fallback) {
            var value = configuration[key];
            return bool.TryParse(value, out var parsed) ? parsed : fallback;
        }

        /// <summary>读取正整数秒数并转换为 TimeSpan，非法值回退默认值。</summary>
        public static TimeSpan GetPositiveSecondsAsTimeSpanOrDefault(IConfiguration configuration, string key, TimeSpan fallback) {
            var value = configuration[key];
            if (!int.TryParse(value, out var seconds) || seconds <= 0) {
                return fallback;
            }

            return TimeSpan.FromSeconds(seconds);
        }

        /// <summary>读取本地时间（HH:mm 或 HH:mm:ss）配置。</summary>
        public static TimeSpan GetTimeOfDayOrDefault(IConfiguration configuration, string key, TimeSpan fallback) {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value)) {
                return fallback;
            }
            value = value.Trim();
            if (!TimeSpan.TryParseExact(
                    value,
                    ["HH\\:mm\\:ss", "HH\\:mm"],
                    CultureInfo.InvariantCulture,
                    TimeSpanStyles.None,
                    out var parsed)) {
                return fallback;
            }

            if (parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1)) {
                return fallback;
            }

            return parsed;
        }

        /// <summary>
        /// 将时间值标准化为本地时间语义。
        /// </summary>
        /// <param name="value">待归一化的时间值。</param>
        /// <returns>带 <see cref="DateTimeKind.Local"/> 的时间值。</returns>
        /// <remarks>
        /// 规则：
        /// 1) Unspecified：按本地时间解释并补齐 Kind；
        /// 2) Local：原样返回；
        /// 3) 其他：视为非法输入并抛错（禁止 UTC/offset 语义进入链路）。
        /// </remarks>
        public static DateTime NormalizeToLocalTime(DateTime value) {
            return value.Kind switch {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Local),
                DateTimeKind.Local => value,
                _ => throw new InvalidOperationException("仅支持本地时间语义，请勿传入 UTC 时间值。")
            };
        }
    }
}
