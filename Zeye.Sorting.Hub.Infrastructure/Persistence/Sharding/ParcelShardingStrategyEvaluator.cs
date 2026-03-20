using System.Globalization;
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
    /// <param name="FinerGranularityExtensionPlan">下一层细粒度扩展规划结果。</param>
    /// <param name="Reason">决策原因摘要。</param>
    /// <param name="ConfigSnapshot">策略配置快照。</param>
    public readonly record struct ParcelShardingStrategyDecision(
        ParcelShardingStrategyMode Mode,
        ParcelTimeShardingGranularity TimeGranularity,
        ParcelVolumeThresholdAction ThresholdAction,
        ParcelShardingVolumeObservation VolumeObservation,
        bool ThresholdReached,
        ExpandByDateMode EffectiveDateMode,
        ParcelFinerGranularityExtensionPlan FinerGranularityExtensionPlan,
        string Reason,
        ParcelShardingStrategyConfigSnapshot ConfigSnapshot);

    /// <summary>
    /// Parcel 容量阈值观测输入（结构化入口，便于未来接入数据库统计或监控采集）。
    /// </summary>
    public readonly record struct ParcelShardingVolumeObservation {
        /// <summary>
        /// 初始化容量观测输入。
        /// </summary>
        /// <param name="Source">观测数据来源标识。</param>
        /// <param name="EstimatedRowsPerShard">单分表估算行数观测值。</param>
        /// <param name="ObservedHotRatio">热点比例观测值（0~1）。</param>
        public ParcelShardingVolumeObservation(string Source, long? EstimatedRowsPerShard, decimal? ObservedHotRatio) {
            this.Source = Source;
            this.EstimatedRowsPerShard = EstimatedRowsPerShard;
            this.ObservedHotRatio = ObservedHotRatio;
        }

        /// <summary>
        /// 观测数据来源标识。
        /// </summary>
        public string Source { get; init; }

        /// <summary>
        /// 单分表估算行数观测值。
        /// </summary>
        public long? EstimatedRowsPerShard { get; init; }

        /// <summary>
        /// 热点比例观测值（0~1）。
        /// </summary>
        public decimal? ObservedHotRatio { get; init; }
    }

    /// <summary>
    /// Parcel finer-granularity 策略配置快照。
    /// </summary>
    public readonly record struct ParcelFinerGranularityStrategySnapshot {
        /// <summary>
        /// 初始化 finer-granularity 配置快照。
        /// </summary>
        /// <param name="ModeWhenPerDayStillHot">当 PerDay 仍过热时推荐的下一层细粒度模式。</param>
        /// <param name="Lifecycle">扩展治理生命周期（仅计划/仅告警/未来可执行）。</param>
        /// <param name="RequirePrebuildGuard">是否要求治理守卫执行预建约束。</param>
        /// <param name="BucketCount">当模式为 BucketedPerDay 时建议的桶数量。</param>
        public ParcelFinerGranularityStrategySnapshot(
            ParcelFinerGranularityMode ModeWhenPerDayStillHot,
            ParcelFinerGranularityPlanLifecycle Lifecycle,
            bool RequirePrebuildGuard,
            int? BucketCount) {
            this.ModeWhenPerDayStillHot = ModeWhenPerDayStillHot;
            this.Lifecycle = Lifecycle;
            this.RequirePrebuildGuard = RequirePrebuildGuard;
            this.BucketCount = BucketCount;
        }

        /// <summary>
        /// 当 PerDay 仍过热时推荐的下一层细粒度模式。
        /// </summary>
        public ParcelFinerGranularityMode ModeWhenPerDayStillHot { get; init; }

        /// <summary>
        /// 扩展治理生命周期（仅计划/仅告警/未来可执行）。
        /// </summary>
        public ParcelFinerGranularityPlanLifecycle Lifecycle { get; init; }

        /// <summary>
        /// 是否要求治理守卫执行预建约束。
        /// </summary>
        public bool RequirePrebuildGuard { get; init; }

        /// <summary>
        /// 当模式为 BucketedPerDay 时建议的桶数量。
        /// </summary>
        public int? BucketCount { get; init; }
    }

    /// <summary>
    /// Parcel finer-granularity 扩展规划结果。
    /// </summary>
    public readonly record struct ParcelFinerGranularityExtensionPlan {
        /// <summary>
        /// 初始化 finer-granularity 扩展规划结果。
        /// </summary>
        /// <param name="ShouldPlanExtension">是否需要规划下一层细粒度扩展。</param>
        /// <param name="SuggestedMode">建议的下一层细粒度模式。</param>
        /// <param name="Lifecycle">扩展治理生命周期（仅计划/仅告警/未来可执行）。</param>
        /// <param name="RequiresPrebuildGuard">是否需要预建守卫。</param>
        /// <param name="Reason">规划原因。</param>
        public ParcelFinerGranularityExtensionPlan(
            bool ShouldPlanExtension,
            ParcelFinerGranularityMode SuggestedMode,
            ParcelFinerGranularityPlanLifecycle Lifecycle,
            bool RequiresPrebuildGuard,
            string Reason) {
            this.ShouldPlanExtension = ShouldPlanExtension;
            this.SuggestedMode = SuggestedMode;
            this.Lifecycle = Lifecycle;
            this.RequiresPrebuildGuard = RequiresPrebuildGuard;
            this.Reason = Reason;
        }

        /// <summary>
        /// 是否需要规划下一层细粒度扩展。
        /// </summary>
        public bool ShouldPlanExtension { get; init; }

        /// <summary>
        /// 建议的下一层细粒度模式。
        /// </summary>
        public ParcelFinerGranularityMode SuggestedMode { get; init; }

        /// <summary>
        /// 扩展治理生命周期（仅计划/仅告警/未来可执行）。
        /// </summary>
        public ParcelFinerGranularityPlanLifecycle Lifecycle { get; init; }

        /// <summary>
        /// 是否需要预建守卫。
        /// </summary>
        public bool RequiresPrebuildGuard { get; init; }

        /// <summary>
        /// 规划原因。
        /// </summary>
        public string Reason { get; init; }
    }

    /// <summary>
    /// Parcel 分表策略配置快照（用于审计与守卫复用）。
    /// </summary>
    public readonly record struct ParcelShardingStrategyConfigSnapshot {
        /// <summary>
        /// 初始化分表策略配置快照。
        /// </summary>
        /// <param name="Mode">策略模式。</param>
        /// <param name="TimeGranularity">时间粒度。</param>
        /// <param name="ThresholdAction">阈值动作。</param>
        /// <param name="MaxRowsPerShard">单分表最大行数阈值。</param>
        /// <param name="HotThresholdRatio">热点阈值。</param>
        /// <param name="VolumeObservation">容量观测输入。</param>
        /// <param name="FinerGranularity">finer-granularity 配置快照。</param>
        public ParcelShardingStrategyConfigSnapshot(
            ParcelShardingStrategyMode Mode,
            ParcelTimeShardingGranularity TimeGranularity,
            ParcelVolumeThresholdAction ThresholdAction,
            long? MaxRowsPerShard,
            decimal? HotThresholdRatio,
            ParcelShardingVolumeObservation VolumeObservation,
            ParcelFinerGranularityStrategySnapshot FinerGranularity) {
            this.Mode = Mode;
            this.TimeGranularity = TimeGranularity;
            this.ThresholdAction = ThresholdAction;
            this.MaxRowsPerShard = MaxRowsPerShard;
            this.HotThresholdRatio = HotThresholdRatio;
            this.VolumeObservation = VolumeObservation;
            this.FinerGranularity = FinerGranularity;
        }

        /// <summary>
        /// 策略模式。
        /// </summary>
        public ParcelShardingStrategyMode Mode { get; init; }

        /// <summary>
        /// 时间粒度。
        /// </summary>
        public ParcelTimeShardingGranularity TimeGranularity { get; init; }

        /// <summary>
        /// 阈值动作。
        /// </summary>
        public ParcelVolumeThresholdAction ThresholdAction { get; init; }

        /// <summary>
        /// 单分表最大行数阈值。
        /// </summary>
        public long? MaxRowsPerShard { get; init; }

        /// <summary>
        /// 热点阈值。
        /// </summary>
        public decimal? HotThresholdRatio { get; init; }

        /// <summary>
        /// 容量观测输入。
        /// </summary>
        public ParcelShardingVolumeObservation VolumeObservation { get; init; }

        /// <summary>
        /// finer-granularity 配置快照。
        /// </summary>
        public ParcelFinerGranularityStrategySnapshot FinerGranularity { get; init; }
    }

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
        /// legacy 单分表当前估算行数配置键（仅兼容旧配置，优先级低于 Observation）。
        /// </summary>
        private const string VolumeLegacyCurrentRowsConfigKey = "Persistence:Sharding:Strategy:Volume:CurrentEstimatedRowsPerShard";

        /// <summary>
        /// 热点阈值配置键（0~1）。
        /// </summary>
        private const string VolumeHotThresholdConfigKey = "Persistence:Sharding:Strategy:Volume:HotThresholdRatio";

        /// <summary>
        /// legacy 当前热点比例观测值配置键（0~1，仅兼容旧配置，优先级低于 Observation）。
        /// </summary>
        private const string VolumeLegacyCurrentHotRatioConfigKey = "Persistence:Sharding:Strategy:Volume:CurrentObservedHotRatio";

        /// <summary>
        /// 结构化观测来源配置键。
        /// </summary>
        private const string VolumeObservationSourceConfigKey = "Persistence:Sharding:Strategy:Volume:Observation:Source";

        /// <summary>
        /// 结构化观测单分表估算行数配置键。
        /// </summary>
        private const string VolumeObservationRowsConfigKey = "Persistence:Sharding:Strategy:Volume:Observation:EstimatedRowsPerShard";

        /// <summary>
        /// 结构化观测热点比例配置键（0~1）。
        /// </summary>
        private const string VolumeObservationHotRatioConfigKey = "Persistence:Sharding:Strategy:Volume:Observation:ObservedHotRatio";

        /// <summary>
        /// 结构化观测来源缺省值。
        /// </summary>
        private const string DefaultObservationSource = "config-static";

        /// <summary>
        /// finer-granularity 下一层模式配置键。
        /// </summary>
        private const string VolumeFinerModeConfigKey = "Persistence:Sharding:Strategy:Volume:FinerGranularity:ModeWhenPerDayStillHot";

        /// <summary>
        /// finer-granularity 生命周期配置键。
        /// </summary>
        private const string VolumeFinerLifecycleConfigKey = "Persistence:Sharding:Strategy:Volume:FinerGranularity:Lifecycle";

        /// <summary>
        /// finer-granularity 预建守卫配置键。
        /// </summary>
        private const string VolumeFinerRequirePrebuildConfigKey = "Persistence:Sharding:Strategy:Volume:FinerGranularity:RequirePrebuildGuard";

        /// <summary>
        /// finer-granularity bucket 数量配置键。
        /// </summary>
        private const string VolumeFinerBucketCountConfigKey = "Persistence:Sharding:Strategy:Volume:FinerGranularity:Bucket:BucketCount";

        /// <summary>
        /// BucketedPerDay 模式允许的最小桶数量。
        /// </summary>
        private const int MinBucketCount = 2;

        /// <summary>
        /// BucketedPerDay 模式允许的最大桶数量。
        /// </summary>
        private const int MaxBucketCount = 128;

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
            var thresholdAction = ParcelVolumeThresholdAction.AlertOnly;
            long? maxRowsPerShard = null;
            decimal? hotThresholdRatio = null;
            var finerGranularityStrategy = new ParcelFinerGranularityStrategySnapshot(
                ModeWhenPerDayStillHot: ParcelFinerGranularityMode.PerHour,
                Lifecycle: ParcelFinerGranularityPlanLifecycle.PlanOnly,
                RequirePrebuildGuard: true,
                BucketCount: null);
            var volumeObservation = new ParcelShardingVolumeObservation(
                Source: DefaultObservationSource,
                EstimatedRowsPerShard: null,
                ObservedHotRatio: null);
            if (mode is ParcelShardingStrategyMode.Volume or ParcelShardingStrategyMode.Hybrid) {
                thresholdAction = ResolveThresholdAction(configuration[VolumeActionConfigKey], validationErrors);
                maxRowsPerShard = ReadPositiveLong(configuration[VolumeMaxRowsConfigKey], VolumeMaxRowsConfigKey, mode, validationErrors);
                volumeObservation = ResolveVolumeObservation(configuration, validationErrors);
                hotThresholdRatio = ReadRatio(
                    configuration[VolumeHotThresholdConfigKey],
                    VolumeHotThresholdConfigKey,
                    requiredWhenMissing: true,
                    validationErrors: validationErrors);
                finerGranularityStrategy = ResolveFinerGranularityStrategy(configuration, validationErrors);
            }

            var thresholdReached = IsThresholdReached(
                maxRowsPerShard,
                volumeObservation.EstimatedRowsPerShard,
                hotThresholdRatio,
                volumeObservation.ObservedHotRatio);
            var thresholdTrigger = ResolveThresholdTrigger(
                maxRowsPerShard,
                volumeObservation.EstimatedRowsPerShard,
                hotThresholdRatio,
                volumeObservation.ObservedHotRatio);
            var effectiveDateMode = ResolveEffectiveDateMode(mode, timeGranularity, thresholdAction, thresholdReached);
            var finerGranularityExtensionPlan = BuildFinerGranularityExtensionPlan(
                mode,
                thresholdAction,
                thresholdReached,
                effectiveDateMode,
                thresholdTrigger,
                finerGranularityStrategy);
            var configSnapshot = new ParcelShardingStrategyConfigSnapshot(
                Mode: mode,
                TimeGranularity: timeGranularity,
                ThresholdAction: thresholdAction,
                MaxRowsPerShard: maxRowsPerShard,
                HotThresholdRatio: hotThresholdRatio,
                VolumeObservation: volumeObservation,
                FinerGranularity: finerGranularityStrategy);
            var reason = BuildReason(
                mode,
                timeGranularity,
                thresholdAction,
                thresholdReached,
                effectiveDateMode,
                volumeObservation.Source,
                thresholdTrigger,
                finerGranularityExtensionPlan);

            var decision = new ParcelShardingStrategyDecision(
                Mode: mode,
                TimeGranularity: timeGranularity,
                ThresholdAction: thresholdAction,
                VolumeObservation: volumeObservation,
                ThresholdReached: thresholdReached,
                EffectiveDateMode: effectiveDateMode,
                FinerGranularityExtensionPlan: finerGranularityExtensionPlan,
                Reason: reason,
                ConfigSnapshot: configSnapshot);
            return new ParcelShardingStrategyEvaluation(decision, Array.AsReadOnly(validationErrors.ToArray()));
        }

        /// <summary>
        /// 解析策略模式（默认 Time）。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>策略模式。</returns>
        private static ParcelShardingStrategyMode ResolveMode(string? raw, ICollection<string> validationErrors) {
            return ParseEnumOrDefault(
                raw,
                ModeConfigKey,
                ParcelShardingStrategyMode.Time,
                "Time/Volume/Hybrid",
                validationErrors);
        }

        /// <summary>
        /// 解析时间粒度（默认 PerMonth）。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>时间粒度。</returns>
        private static ParcelTimeShardingGranularity ResolveTimeGranularity(string? raw, ICollection<string> validationErrors) {
            return ParseEnumOrDefault(
                raw,
                TimeGranularityConfigKey,
                ParcelTimeShardingGranularity.PerMonth,
                "PerMonth/PerDay",
                validationErrors);
        }

        /// <summary>
        /// 解析容量阈值动作（默认 AlertOnly）。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>阈值动作。</returns>
        private static ParcelVolumeThresholdAction ResolveThresholdAction(string? raw, ICollection<string> validationErrors) {
            return ParseEnumOrDefault(
                raw,
                VolumeActionConfigKey,
                ParcelVolumeThresholdAction.AlertOnly,
                "AlertOnly/SwitchToPerDay",
                validationErrors);
        }

        /// <summary>
        /// 解析 finer-granularity 配置快照。
        /// </summary>
        /// <param name="configuration">配置源。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>配置快照。</returns>
        private static ParcelFinerGranularityStrategySnapshot ResolveFinerGranularityStrategy(
            IConfiguration configuration,
            ICollection<string> validationErrors) {
            var mode = ParseEnumOrDefault(
                configuration[VolumeFinerModeConfigKey],
                VolumeFinerModeConfigKey,
                ParcelFinerGranularityMode.PerHour,
                "PerHour/BucketedPerDay/None",
                validationErrors);
            var lifecycle = ParseEnumOrDefault(
                configuration[VolumeFinerLifecycleConfigKey],
                VolumeFinerLifecycleConfigKey,
                ParcelFinerGranularityPlanLifecycle.PlanOnly,
                "PlanOnly/AlertOnly/FutureExecutable",
                validationErrors);
            var requirePrebuildGuard = ReadBoolean(
                configuration[VolumeFinerRequirePrebuildConfigKey],
                VolumeFinerRequirePrebuildConfigKey,
                defaultValue: true,
                validationErrors: validationErrors);
            var bucketCount = ReadOptionalPositiveInt(configuration[VolumeFinerBucketCountConfigKey], VolumeFinerBucketCountConfigKey, validationErrors);
            ValidateBucketedPerDayConfiguration(mode, bucketCount, validationErrors);

            return new ParcelFinerGranularityStrategySnapshot(mode, lifecycle, requirePrebuildGuard, bucketCount);
        }

        /// <summary>
        /// 校验 BucketedPerDay 模式的必填参数完整性。
        /// </summary>
        /// <param name="mode">finer-granularity 模式。</param>
        /// <param name="bucketCount">bucket 数量。</param>
        /// <param name="validationErrors">错误集合。</param>
        private static void ValidateBucketedPerDayConfiguration(
            ParcelFinerGranularityMode mode,
            int? bucketCount,
            ICollection<string> validationErrors) {
            if (mode != ParcelFinerGranularityMode.BucketedPerDay && bucketCount.HasValue) {
                validationErrors.Add($"配置项 {VolumeFinerBucketCountConfigKey} 当前不会生效：仅当 {VolumeFinerModeConfigKey}=BucketedPerDay 时才会使用 BucketCount。");
                return;
            }

            if (mode != ParcelFinerGranularityMode.BucketedPerDay) {
                return;
            }

            if (!bucketCount.HasValue) {
                validationErrors.Add($"配置项 {VolumeFinerBucketCountConfigKey} 必填，且需为正整数（当前 ModeWhenPerDayStillHot=BucketedPerDay）。");
                return;
            }

            if (bucketCount.Value is < MinBucketCount or > MaxBucketCount) {
                validationErrors.Add($"配置项 {VolumeFinerBucketCountConfigKey} 值非法：{bucketCount.Value}。范围必须在 {MinBucketCount}~{MaxBucketCount}。");
            }
        }

        /// <summary>
        /// 解析容量观测输入：优先结构化 Observation 节，缺失时回退 legacy 字段。
        /// </summary>
        /// <param name="configuration">配置源。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>容量观测输入对象。</returns>
        private static ParcelShardingVolumeObservation ResolveVolumeObservation(
            IConfiguration configuration,
            ICollection<string> validationErrors) {
            var source = NormalizeObservationSource(configuration[VolumeObservationSourceConfigKey]);
            var estimatedRowsRaw = ResolveObservationRawValue(
                structuredRaw: configuration[VolumeObservationRowsConfigKey],
                legacyRaw: configuration[VolumeLegacyCurrentRowsConfigKey]);
            var observedHotRatioRaw = ResolveObservationRawValue(
                structuredRaw: configuration[VolumeObservationHotRatioConfigKey],
                legacyRaw: configuration[VolumeLegacyCurrentHotRatioConfigKey]);
            var estimatedRows = ReadNonNegativeLong(
                raw: estimatedRowsRaw,
                key: $"{VolumeObservationRowsConfigKey}|{VolumeLegacyCurrentRowsConfigKey}",
                validationErrors: validationErrors);
            var observedHotRatio = ReadRatio(
                raw: observedHotRatioRaw,
                key: $"{VolumeObservationHotRatioConfigKey}|{VolumeLegacyCurrentHotRatioConfigKey}",
                requiredWhenMissing: false,
                validationErrors: validationErrors);
            return new ParcelShardingVolumeObservation(source, estimatedRows, observedHotRatio);
        }

        /// <summary>
        /// 归一化观测来源文本。
        /// </summary>
        /// <param name="raw">原始来源文本。</param>
        /// <returns>来源标识。</returns>
        private static string NormalizeObservationSource(string? raw) {
            return string.IsNullOrWhiteSpace(raw) ? DefaultObservationSource : raw.Trim();
        }

        /// <summary>
        /// 读取观测配置：优先结构化键，若未配置则回退 legacy 键。
        /// </summary>
        /// <param name="structuredRaw">结构化配置值。</param>
        /// <param name="legacyRaw">历史配置值。</param>
        /// <returns>最终采用的原始文本。</returns>
        private static string? ResolveObservationRawValue(string? structuredRaw, string? legacyRaw) {
            return string.IsNullOrWhiteSpace(structuredRaw) ? legacyRaw : structuredRaw;
        }

        /// <summary>
        /// 判断枚举配置项是否使用了纯数字令牌。
        /// </summary>
        /// <param name="value">配置文本。</param>
        /// <returns>纯数字令牌返回 true，否则 false。</returns>
        private static bool IsNumericEnumToken(string value) {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        }

        /// <summary>
        /// 通用枚举配置解析（空值回退默认值、拒绝数字枚举、输出统一校验错误）。
        /// </summary>
        /// <typeparam name="TEnum">枚举类型。</typeparam>
        /// <param name="raw">原始配置。</param>
        /// <param name="key">配置键。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <param name="allowedValues">允许值文本。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>解析后的枚举值。</returns>
        private static TEnum ParseEnumOrDefault<TEnum>(
            string? raw,
            string key,
            TEnum defaultValue,
            string allowedValues,
            ICollection<string> validationErrors)
            where TEnum : struct, Enum {
            if (string.IsNullOrWhiteSpace(raw)) {
                return defaultValue;
            }

            var normalized = raw.Trim();
            if (IsNumericEnumToken(normalized)) {
                validationErrors.Add($"配置项 {key} 值非法：{normalized}。允许值：{allowedValues}。");
                return defaultValue;
            }

            if (Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var parsedValue)
                && Enum.IsDefined(parsedValue)) {
                return parsedValue;
            }

            validationErrors.Add($"配置项 {key} 值非法：{normalized}。允许值：{allowedValues}。");
            return defaultValue;
        }

        /// <summary>
        /// 读取布尔配置：缺失回退默认值；非法值输出校验错误并回退默认值。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="key">配置键。</param>
        /// <param name="defaultValue">默认值。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>解析结果。</returns>
        private static bool ReadBoolean(
            string? raw,
            string key,
            bool defaultValue,
            ICollection<string> validationErrors) {
            if (string.IsNullOrWhiteSpace(raw)) {
                return defaultValue;
            }

            var normalized = raw.Trim();
            if (bool.TryParse(normalized, out var parsedValue)) {
                return parsedValue;
            }

            validationErrors.Add($"配置项 {key} 值非法：{normalized}。允许值：true/false。");
            return defaultValue;
        }

        /// <summary>
        /// 读取可选正整数配置。
        /// </summary>
        /// <param name="raw">原始配置。</param>
        /// <param name="key">配置键。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>解析值；未配置返回 null。</returns>
        private static int? ReadOptionalPositiveInt(string? raw, string key, ICollection<string> validationErrors) {
            if (string.IsNullOrWhiteSpace(raw)) {
                return null;
            }

            if (!int.TryParse(raw.Trim(), out var value) || value <= 0) {
                validationErrors.Add($"配置项 {key} 值非法：{raw}。必须为正整数。");
                return null;
            }

            return value;
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
        /// <param name="requiredWhenMissing">当配置缺失时是否必填。</param>
        /// <param name="validationErrors">错误集合。</param>
        /// <returns>解析值；未配置返回 null。</returns>
        private static decimal? ReadRatio(string? raw, string key, bool requiredWhenMissing, ICollection<string> validationErrors) {
            if (string.IsNullOrWhiteSpace(raw)) {
                if (requiredWhenMissing) {
                    validationErrors.Add($"配置项 {key} 必填，且范围应在 0~1。");
                }
                return null;
            }

            var parsed = decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value);
            if (!parsed) {
                validationErrors.Add($"配置项 {key} 值格式非法：{raw}。必须为数字格式。");
                return null;
            }

            if (value < 0m || value > 1m) {
                validationErrors.Add($"配置项 {key} 值超出范围：{raw}。范围必须在 0~1。");
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
            var isRowThresholdReached = IsRowThresholdReached(maxRowsPerShard, currentEstimatedRowsPerShard);
            var isHotThresholdReached = IsHotThresholdReached(hotThresholdRatio, currentObservedHotRatio);
            return isRowThresholdReached || isHotThresholdReached;
        }

        /// <summary>
        /// 判断是否命中“单分表行数”阈值。
        /// </summary>
        /// <param name="maxRowsPerShard">单分表最大行数阈值。</param>
        /// <param name="currentEstimatedRowsPerShard">当前观测单分表估算行数。</param>
        /// <returns>命中返回 true，否则 false。</returns>
        private static bool IsRowThresholdReached(long? maxRowsPerShard, long? currentEstimatedRowsPerShard) {
            return maxRowsPerShard.HasValue
                && currentEstimatedRowsPerShard.HasValue
                && currentEstimatedRowsPerShard.Value >= maxRowsPerShard.Value;
        }

        /// <summary>
        /// 判断是否命中“热点比例”阈值。
        /// </summary>
        /// <param name="hotThresholdRatio">热点阈值。</param>
        /// <param name="currentObservedHotRatio">当前观测热点比例。</param>
        /// <returns>命中返回 true，否则 false。</returns>
        private static bool IsHotThresholdReached(decimal? hotThresholdRatio, decimal? currentObservedHotRatio) {
            return hotThresholdRatio.HasValue
                && currentObservedHotRatio.HasValue
                && currentObservedHotRatio.Value >= hotThresholdRatio.Value;
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
        /// 基于当前决策与配置构建 finer-granularity 扩展规划。
        /// </summary>
        /// <param name="mode">策略模式。</param>
        /// <param name="thresholdAction">阈值动作。</param>
        /// <param name="thresholdReached">阈值是否命中。</param>
        /// <param name="effectiveDateMode">当前生效分表粒度。</param>
        /// <param name="thresholdTrigger">阈值触发来源。</param>
        /// <param name="finerGranularityStrategy">finer-granularity 配置快照。</param>
        /// <returns>扩展规划结果。</returns>
        private static ParcelFinerGranularityExtensionPlan BuildFinerGranularityExtensionPlan(
            ParcelShardingStrategyMode mode,
            ParcelVolumeThresholdAction thresholdAction,
            bool thresholdReached,
            ExpandByDateMode effectiveDateMode,
            string thresholdTrigger,
            ParcelFinerGranularityStrategySnapshot finerGranularityStrategy) {
            var shouldPlanExtension = mode is ParcelShardingStrategyMode.Volume or ParcelShardingStrategyMode.Hybrid
                && thresholdAction == ParcelVolumeThresholdAction.SwitchToPerDay
                && thresholdReached
                && effectiveDateMode == ExpandByDateMode.PerDay
                && finerGranularityStrategy.ModeWhenPerDayStillHot != ParcelFinerGranularityMode.None;
            if (!shouldPlanExtension) {
                return new ParcelFinerGranularityExtensionPlan(
                    ShouldPlanExtension: false,
                    SuggestedMode: ParcelFinerGranularityMode.None,
                    Lifecycle: ParcelFinerGranularityPlanLifecycle.PlanOnly,
                    RequiresPrebuildGuard: false,
                    Reason: BuildNotTriggeredPlanReason(thresholdTrigger, effectiveDateMode));
            }

            return new ParcelFinerGranularityExtensionPlan(
                ShouldPlanExtension: true,
                SuggestedMode: finerGranularityStrategy.ModeWhenPerDayStillHot,
                Lifecycle: finerGranularityStrategy.Lifecycle,
                RequiresPrebuildGuard: finerGranularityStrategy.RequirePrebuildGuard,
                Reason: BuildTriggeredPlanReason(thresholdTrigger, finerGranularityStrategy));
        }

        /// <summary>
        /// 构建“未触发扩展规划”原因文本。
        /// </summary>
        /// <param name="thresholdTrigger">阈值触发来源。</param>
        /// <param name="effectiveDateMode">当前生效分表粒度。</param>
        /// <returns>原因文本。</returns>
        private static string BuildNotTriggeredPlanReason(string thresholdTrigger, ExpandByDateMode effectiveDateMode) {
            return $"not-triggered; Trigger={thresholdTrigger}; EffectiveDateMode={effectiveDateMode}";
        }

        /// <summary>
        /// 构建“已触发扩展规划”原因文本。
        /// </summary>
        /// <param name="thresholdTrigger">阈值触发来源。</param>
        /// <param name="finerGranularityStrategy">finer-granularity 配置快照。</param>
        /// <returns>原因文本。</returns>
        private static string BuildTriggeredPlanReason(
            string thresholdTrigger,
            ParcelFinerGranularityStrategySnapshot finerGranularityStrategy) {
            var reason = $"per-day-still-hot-planning; Trigger={thresholdTrigger}; SuggestedMode={finerGranularityStrategy.ModeWhenPerDayStillHot}; Lifecycle={finerGranularityStrategy.Lifecycle}";
            if (finerGranularityStrategy.ModeWhenPerDayStillHot == ParcelFinerGranularityMode.BucketedPerDay
                && finerGranularityStrategy.BucketCount.HasValue) {
                reason = $"{reason}; BucketCount={finerGranularityStrategy.BucketCount.Value}";
            }

            return reason;
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
            ExpandByDateMode effectiveDateMode,
            string observationSource,
            string thresholdTrigger,
            ParcelFinerGranularityExtensionPlan finerGranularityExtensionPlan) {
            return $"Mode={mode}; TimeGranularity={timeGranularity}; Action={thresholdAction}; ThresholdReached={thresholdReached}; Trigger={thresholdTrigger}; ObservationSource={observationSource}; EffectiveDateMode={effectiveDateMode}; FinerPlanNeeded={finerGranularityExtensionPlan.ShouldPlanExtension}; FinerSuggestedMode={finerGranularityExtensionPlan.SuggestedMode}; FinerLifecycle={finerGranularityExtensionPlan.Lifecycle}";
        }

        /// <summary>
        /// 解析容量阈值命中的触发来源。
        /// </summary>
        /// <param name="maxRowsPerShard">单分表最大行数阈值。</param>
        /// <param name="currentEstimatedRowsPerShard">当前观测单分表估算行数。</param>
        /// <param name="hotThresholdRatio">热点阈值。</param>
        /// <param name="currentObservedHotRatio">当前观测热点比例。</param>
        /// <returns>触发来源标识。</returns>
        private static string ResolveThresholdTrigger(
            long? maxRowsPerShard,
            long? currentEstimatedRowsPerShard,
            decimal? hotThresholdRatio,
            decimal? currentObservedHotRatio) {
            var byRows = IsRowThresholdReached(maxRowsPerShard, currentEstimatedRowsPerShard);
            var byHot = IsHotThresholdReached(hotThresholdRatio, currentObservedHotRatio);
            if (byRows && byHot) {
                return "rows-and-hot";
            }

            if (byRows) {
                return "rows";
            }

            if (byHot) {
                return "hot";
            }

            return "none";
        }
    }
}
