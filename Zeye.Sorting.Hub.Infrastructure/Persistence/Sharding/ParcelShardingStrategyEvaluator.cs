using EFCore.Sharding;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding {

    /// <summary>
    /// Parcel 分表策略配置评估结果。
    /// </summary>
    /// <param name="Decision">策略决策。</param>
    /// <param name="ValidationErrors">结构化配置校验错误。</param>
    public readonly record struct ParcelShardingStrategyEvaluation(
        ParcelShardingStrategyDecision Decision,
        IReadOnlyList<string> ValidationErrors);

    /// <summary>
    /// Parcel 分表策略决策快照。
    /// </summary>
    /// <param name="Mode">策略模式。</param>
    /// <param name="TimeGranularity">时间粒度。</param>
    /// <param name="ThresholdAction">阈值动作。</param>
    /// <param name="ThresholdReached">是否命中容量阈值。</param>
    /// <param name="EffectiveDateMode">最终用于注册的时间分表粒度。</param>
    /// <param name="Reason">决策原因摘要。</param>
    public readonly record struct ParcelShardingStrategyDecision(
        ParcelShardingStrategyMode Mode,
        ParcelTimeShardingGranularity TimeGranularity,
        ParcelVolumeThresholdAction ThresholdAction,
        bool ThresholdReached,
        ExpandByDateMode EffectiveDateMode,
        string Reason);

    /// <summary>
    /// Parcel 分表策略评估器（配置模型 + 规则决策 + 结构化校验）。
    /// </summary>
    public static class ParcelShardingStrategyEvaluator {
        /// <summary>
        /// 策略模式配置键。
        /// </summary>
        private const string ModeConfigKey = "Persistence:Sharding:Strategy:Mode";

        /// <summary>
        /// 时间粒度配置键。
        /// </summary>
        private const string TimeGranularityConfigKey = "Persistence:Sharding:Strategy:Time:Granularity";

        /// <summary>
        /// 容量阈值动作配置键。
        /// </summary>
        private const string VolumeActionConfigKey = "Persistence:Sharding:Strategy:Volume:ActionOnThreshold";

        /// <summary>
        /// 单分表最大行数阈值配置键。
        /// </summary>
        private const string VolumeMaxRowsConfigKey = "Persistence:Sharding:Strategy:Volume:MaxRowsPerShard";

        /// <summary>
        /// 单分表当前估算行数配置键。
        /// </summary>
        private const string VolumeCurrentRowsConfigKey = "Persistence:Sharding:Strategy:Volume:CurrentEstimatedRowsPerShard";

        /// <summary>
        /// 热点阈值配置键（0~1）。
        /// </summary>
        private const string VolumeHotThresholdConfigKey = "Persistence:Sharding:Strategy:Volume:HotThresholdRatio";

        /// <summary>
        /// 当前热点比例观测值配置键（0~1）。
        /// </summary>
        private const string VolumeCurrentHotRatioConfigKey = "Persistence:Sharding:Strategy:Volume:CurrentObservedHotRatio";

        /// <summary>
        /// 评估分表策略配置并产出决策与校验结果。
        /// </summary>
        /// <param name="configuration">配置源。</param>
        /// <returns>评估结果。</returns>
        public static ParcelShardingStrategyEvaluation Evaluate(IConfiguration configuration) {
            ArgumentNullException.ThrowIfNull(configuration);

            var validationErrors = new List<string>();
            var mode = ResolveMode(configuration[ModeConfigKey], validationErrors);
            var timeGranularity = ResolveTimeGranularity(configuration[TimeGranularityConfigKey], validationErrors);
            var thresholdAction = ResolveThresholdAction(configuration[VolumeActionConfigKey], validationErrors);
            var maxRowsPerShard = ReadPositiveLong(configuration[VolumeMaxRowsConfigKey], VolumeMaxRowsConfigKey, mode, validationErrors);
            var currentEstimatedRowsPerShard = ReadNonNegativeLong(configuration[VolumeCurrentRowsConfigKey], VolumeCurrentRowsConfigKey, validationErrors);
            var hotThresholdRatio = ReadRatio(configuration[VolumeHotThresholdConfigKey], VolumeHotThresholdConfigKey, mode, validationErrors);
            var currentObservedHotRatio = ReadRatio(configuration[VolumeCurrentHotRatioConfigKey], VolumeCurrentHotRatioConfigKey, ParcelShardingStrategyMode.Time, validationErrors);

            var thresholdReached = IsThresholdReached(
                maxRowsPerShard,
                currentEstimatedRowsPerShard,
                hotThresholdRatio,
                currentObservedHotRatio);

            var effectiveDateMode = ResolveEffectiveDateMode(mode, timeGranularity, thresholdAction, thresholdReached);
            var reason = BuildReason(mode, timeGranularity, thresholdAction, thresholdReached, effectiveDateMode);

            var decision = new ParcelShardingStrategyDecision(
                Mode: mode,
                TimeGranularity: timeGranularity,
                ThresholdAction: thresholdAction,
                ThresholdReached: thresholdReached,
                EffectiveDateMode: effectiveDateMode,
                Reason: reason);
            return new ParcelShardingStrategyEvaluation(decision, validationErrors);
        }

        /// <summary>
        /// 解析策略模式（默认 Time）。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>策略模式。</returns>
        private static ParcelShardingStrategyMode ResolveMode(string? raw, ICollection<string> validationErrors) {
            if (string.IsNullOrWhiteSpace(raw)) {
                return ParcelShardingStrategyMode.Time;
            }

            var normalized = raw.Trim();
            if (Enum.TryParse<ParcelShardingStrategyMode>(normalized, ignoreCase: true, out var mode)) {
                return mode;
            }

            validationErrors.Add($"配置项 {ModeConfigKey} 值非法：{normalized}。允许值：Time/Volume/Hybrid。");
            return ParcelShardingStrategyMode.Time;
        }

        /// <summary>
        /// 解析时间粒度（默认 PerMonth）。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>时间粒度。</returns>
        private static ParcelTimeShardingGranularity ResolveTimeGranularity(string? raw, ICollection<string> validationErrors) {
            if (string.IsNullOrWhiteSpace(raw)) {
                return ParcelTimeShardingGranularity.PerMonth;
            }

            var normalized = raw.Trim();
            if (Enum.TryParse<ParcelTimeShardingGranularity>(normalized, ignoreCase: true, out var granularity)) {
                return granularity;
            }

            validationErrors.Add($"配置项 {TimeGranularityConfigKey} 值非法：{normalized}。允许值：PerMonth/PerDay。");
            return ParcelTimeShardingGranularity.PerMonth;
        }

        /// <summary>
        /// 解析容量阈值动作（默认 AlertOnly）。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>阈值动作。</returns>
        private static ParcelVolumeThresholdAction ResolveThresholdAction(string? raw, ICollection<string> validationErrors) {
            if (string.IsNullOrWhiteSpace(raw)) {
                return ParcelVolumeThresholdAction.AlertOnly;
            }

            var normalized = raw.Trim();
            if (Enum.TryParse<ParcelVolumeThresholdAction>(normalized, ignoreCase: true, out var action)) {
                return action;
            }

            validationErrors.Add($"配置项 {VolumeActionConfigKey} 值非法：{normalized}。允许值：AlertOnly/SwitchToPerDay。");
            return ParcelVolumeThresholdAction.AlertOnly;
        }

        /// <summary>
        /// 读取正整数阈值，按模式校验必填性。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="key">配置键。</param>
        /// <param name="mode">策略模式。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>解析值；未配置返回 null。</returns>
        private static long? ReadPositiveLong(string? raw, string key, ParcelShardingStrategyMode mode, ICollection<string> validationErrors) {
            if (string.IsNullOrWhiteSpace(raw)) {
                if (mode is ParcelShardingStrategyMode.Volume or ParcelShardingStrategyMode.Hybrid) {
                    validationErrors.Add($"配置项 {key} 必填，且需为正整数。");
                }
                return null;
            }

            if (!long.TryParse(raw.Trim(), out var value) || value <= 0) {
                validationErrors.Add($"配置项 {key} 值非法：{raw}。必须为正整数。");
                return null;
            }

            return value;
        }

        /// <summary>
        /// 读取非负整数观测值。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="key">配置键。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>解析值；未配置返回 null。</returns>
        private static long? ReadNonNegativeLong(string? raw, string key, ICollection<string> validationErrors) {
            if (string.IsNullOrWhiteSpace(raw)) {
                return null;
            }

            if (!long.TryParse(raw.Trim(), out var value) || value < 0) {
                validationErrors.Add($"配置项 {key} 值非法：{raw}。必须为非负整数。");
                return null;
            }

            return value;
        }

        /// <summary>
        /// 读取比例值（0~1）。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="key">配置键。</param>
        /// <param name="mode">策略模式。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>解析值；未配置返回 null。</returns>
        private static decimal? ReadRatio(string? raw, string key, ParcelShardingStrategyMode mode, ICollection<string> validationErrors) {
            if (string.IsNullOrWhiteSpace(raw)) {
                if (mode is ParcelShardingStrategyMode.Volume or ParcelShardingStrategyMode.Hybrid && key == VolumeHotThresholdConfigKey) {
                    validationErrors.Add($"配置项 {key} 必填，且范围应在 0~1。");
                }
                return null;
            }

            if (!decimal.TryParse(raw.Trim(), out var value) || value < 0m || value > 1m) {
                validationErrors.Add($"配置项 {key} 值非法：{raw}。范围必须在 0~1。");
                return null;
            }

            return value;
        }

        /// <summary>
        /// 评估容量阈值是否命中。
        /// </summary>
        /// <param name="maxRowsPerShard">容量阈值。</param>
        /// <param name="currentEstimatedRowsPerShard">当前观测行数。</param>
        /// <param name="hotThresholdRatio">热点阈值。</param>
        /// <param name="currentObservedHotRatio">当前观测热点比例。</param>
        /// <returns>命中返回 true，否则 false。</returns>
        private static bool IsThresholdReached(
            long? maxRowsPerShard,
            long? currentEstimatedRowsPerShard,
            decimal? hotThresholdRatio,
            decimal? currentObservedHotRatio) {
            var rowsReached = maxRowsPerShard.HasValue
                && currentEstimatedRowsPerShard.HasValue
                && currentEstimatedRowsPerShard.Value >= maxRowsPerShard.Value;
            var hotReached = hotThresholdRatio.HasValue
                && currentObservedHotRatio.HasValue
                && currentObservedHotRatio.Value >= hotThresholdRatio.Value;
            return rowsReached || hotReached;
        }

        /// <summary>
        /// 解析最终时间分表粒度。
        /// </summary>
        /// <param name="mode">策略模式。</param>
        /// <param name="timeGranularity">时间粒度。</param>
        /// <param name="thresholdAction">阈值动作。</param>
        /// <param name="thresholdReached">是否命中阈值。</param>
        /// <returns>最终时间分表粒度。</returns>
        private static ExpandByDateMode ResolveEffectiveDateMode(
            ParcelShardingStrategyMode mode,
            ParcelTimeShardingGranularity timeGranularity,
            ParcelVolumeThresholdAction thresholdAction,
            bool thresholdReached) {
            var configuredMode = timeGranularity == ParcelTimeShardingGranularity.PerDay
                ? ExpandByDateMode.PerDay
                : ExpandByDateMode.PerMonth;
            if (mode is not (ParcelShardingStrategyMode.Volume or ParcelShardingStrategyMode.Hybrid)) {
                return configuredMode;
            }

            if (thresholdReached && thresholdAction == ParcelVolumeThresholdAction.SwitchToPerDay) {
                return ExpandByDateMode.PerDay;
            }

            return configuredMode;
        }

        /// <summary>
        /// 构建策略决策原因摘要。
        /// </summary>
        /// <param name="mode">策略模式。</param>
        /// <param name="timeGranularity">时间粒度。</param>
        /// <param name="thresholdAction">阈值动作。</param>
        /// <param name="thresholdReached">是否命中阈值。</param>
        /// <param name="effectiveDateMode">最终时间分表粒度。</param>
        /// <returns>原因摘要。</returns>
        private static string BuildReason(
            ParcelShardingStrategyMode mode,
            ParcelTimeShardingGranularity timeGranularity,
            ParcelVolumeThresholdAction thresholdAction,
            bool thresholdReached,
            ExpandByDateMode effectiveDateMode) {
            return $"Mode={mode}; TimeGranularity={timeGranularity}; ThresholdReached={thresholdReached}; Action={thresholdAction}; EffectiveDateMode={effectiveDateMode}";
        }
    }
}
