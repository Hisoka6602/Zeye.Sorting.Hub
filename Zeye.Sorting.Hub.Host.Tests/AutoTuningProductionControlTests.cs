using System.Buffers.Binary;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using EFCore.Sharding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>自动调优生产控制相关功能的集成测试集合。</summary>
public sealed class AutoTuningProductionControlTests {
    /// <summary>浮点精度比较容差（用于断言 double 近似相等）。</summary>
    private const double DoublePrecisionTolerance = 0.0001d;

    /// <summary>
    /// NLog 全局配置切换互斥锁：防止并行测试对全局 <see cref="NLog.LogManager.Configuration"/> 的竞争写入。
    /// </summary>
    private static readonly object NLogConfigLock = new();
    /// <summary>
    /// 验证场景：ParcelStatus_ShouldOnlyContainThreeValues。
    /// </summary>
    [Fact]
    public void ParcelStatus_ShouldOnlyContainThreeValues() {
        var values = Enum.GetValues<ParcelStatus>();
        Assert.Equal(3, values.Length);
        Assert.Contains(ParcelStatus.Pending, values);
        Assert.Contains(ParcelStatus.Completed, values);
        Assert.Contains(ParcelStatus.SortingException, values);
    }

    /// <summary>
    /// 验证场景：Parcel_CreateAndMarkSortingException_ShouldKeepExceptionTypeConsistent。
    /// </summary>
    [Fact]
    public void Parcel_CreateAndMarkSortingException_ShouldKeepExceptionTypeConsistent() {
        var parcel = Parcel.Create(
            id: 9001,
            parcelTimestamp: 1,
            type: ParcelType.Normal,
            barCodes: "BC001",
            weight: 1.1m,
            workstationName: "WS-01",
            scannedTime: DateTime.Now,
            dischargeTime: DateTime.Now,
            targetChuteId: 100,
            actualChuteId: 101,
            requestStatus: ApiRequestStatus.Success,
            bagCode: "BAG-01",
            isSticking: false,
            length: 1,
            width: 1,
            height: 1,
            volume: 1,
            hasImages: false,
            hasVideos: false,
            coordinate: "0,0");

        Assert.Equal(ParcelStatus.Pending, parcel.Status);
        Assert.Null(parcel.ExceptionType);

        parcel.MarkSortingException(ParcelExceptionType.WaitDwsDataTimeout);
        Assert.Equal(ParcelStatus.SortingException, parcel.Status);
        Assert.Equal(ParcelExceptionType.WaitDwsDataTimeout, parcel.ExceptionType);

        parcel.MarkCompleted(DateTime.Now);
        Assert.Equal(ParcelStatus.Completed, parcel.Status);
        Assert.Null(parcel.ExceptionType);
        Assert.Throws<InvalidOperationException>(() => parcel.MarkSortingException(ParcelExceptionType.ParcelLost));
    }

    /// <summary>
    /// 验证场景：MigrationFailStartupPolicy_DefaultsToFalse_WhenConfigMissing。
    /// </summary>
    [Fact]
    public void MigrationFailStartupPolicy_DefaultsToFalse_WhenConfigMissing() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var failStartup = DatabaseInitializerHostedService.ResolveFailStartupOnMigrationError(configuration);
        Assert.False(failStartup);
    }

    /// <summary>
    /// 验证场景：MigrationFailStartupPolicy_ReturnsTrue_WhenConfigEnabled。
    /// </summary>
    [Fact]
    public void MigrationFailStartupPolicy_ReturnsTrue_WhenConfigEnabled() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Migration:FailStartupOnError"] = "true"
            })
            .Build();

        var failStartup = DatabaseInitializerHostedService.ResolveFailStartupOnMigrationError(configuration);
        Assert.True(failStartup);
    }

    /// <summary>
    /// 验证场景：MigrationFailStartupPolicy_ReturnsFalse_WhenConfigIsInvalid。
    /// </summary>
    [Fact]
    public void MigrationFailStartupPolicy_ReturnsFalse_WhenConfigIsInvalid() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Migration:FailStartupOnError"] = "invalid"
            })
            .Build();

        var failStartup = DatabaseInitializerHostedService.ResolveFailStartupOnMigrationError(configuration);
        Assert.False(failStartup);
    }

    /// <summary>
    /// 验证场景：MigrationFailureMode_DefaultsToFailFast_InProduction。
    /// </summary>
    [Fact]
    public void MigrationFailureMode_DefaultsToFailFast_InProduction() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var mode = DatabaseInitializerHostedService.ResolveMigrationFailureMode(configuration, isProductionEnvironment: true);
        Assert.Equal(MigrationFailureMode.FailFast, mode);
    }

    /// <summary>
    /// 验证场景：MigrationFailureMode_DefaultsToDegraded_InNonProduction。
    /// </summary>
    [Fact]
    public void MigrationFailureMode_DefaultsToDegraded_InNonProduction() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var mode = DatabaseInitializerHostedService.ResolveMigrationFailureMode(configuration, isProductionEnvironment: false);
        Assert.Equal(MigrationFailureMode.Degraded, mode);
    }

    /// <summary>
    /// 验证场景：MigrationFailureMode_UsesEnvironmentSpecificConfig。
    /// </summary>
    [Fact]
    public void MigrationFailureMode_UsesEnvironmentSpecificConfig() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Migration:FailureStrategy:Production"] = "Degraded",
                ["Persistence:Migration:FailureStrategy:NonProduction"] = "FailFast"
            })
            .Build();

        var productionMode = DatabaseInitializerHostedService.ResolveMigrationFailureMode(configuration, isProductionEnvironment: true);
        var nonProductionMode = DatabaseInitializerHostedService.ResolveMigrationFailureMode(configuration, isProductionEnvironment: false);
        Assert.Equal(MigrationFailureMode.Degraded, productionMode);
        Assert.Equal(MigrationFailureMode.FailFast, nonProductionMode);
    }

    /// <summary>
    /// 验证场景：MigrationFailureMode_FallbacksToLegacyBooleanSwitch。
    /// </summary>
    [Fact]
    public void MigrationFailureMode_FallbacksToLegacyBooleanSwitch() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Migration:FailStartupOnError"] = "true"
            })
            .Build();

        var mode = DatabaseInitializerHostedService.ResolveMigrationFailureMode(configuration, isProductionEnvironment: false);
        Assert.Equal(MigrationFailureMode.FailFast, mode);
    }

    /// <summary>
    /// 验证场景：ShardingGovernanceTextNormalization_UsesPlaceholderForWhitespace。
    /// </summary>
    [Fact]
    public void ShardingGovernanceTextNormalization_UsesPlaceholderForWhitespace() {
        var normalized = DatabaseInitializerHostedService.NormalizeOptionalTextOrPlaceholder("   ", "未配置");
        Assert.Equal("未配置", normalized);
        Assert.Equal("runbook-path", DatabaseInitializerHostedService.NormalizeOptionalTextOrPlaceholder("  runbook-path  ", "未配置"));
    }

    /// <summary>
    /// 验证场景：ShardingGovernance_ResolvesStructuredExpansionStages。
    /// </summary>
    [Fact]
    public void ShardingGovernance_ResolvesStructuredExpansionStages() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:HashSharding:ExpansionPlan:Stages:0"] = "warmup",
                ["Persistence:Sharding:HashSharding:ExpansionPlan:Stages:1"] = "verification",
                ["Persistence:Sharding:HashSharding:ExpansionPlan:Stages:2"] = "cutover"
            })
            .Build();

        var stages = DatabaseInitializerHostedService.ResolveShardingExpansionPlanStages(configuration);
        Assert.Equal(["warmup", "verification", "cutover"], stages);
    }

    /// <summary>
    /// 验证场景：ShardingGovernance_BuildExpansionPlanSummary_PrefersStructuredStages。
    /// </summary>
    [Fact]
    public void ShardingGovernance_BuildExpansionPlanSummary_PrefersStructuredStages() {
        var summary = DatabaseInitializerHostedService.BuildExpansionPlanSummary(
            currentMod: 16,
            targetMod: 32,
            stages: ["warmup", "dual-write", "cutover"],
            legacyPlan: "16->32 text",
            placeholder: "未配置");
        Assert.Equal("16->32: warmup -> dual-write -> cutover", summary);

        var fallbackSummary = DatabaseInitializerHostedService.BuildExpansionPlanSummary(
            currentMod: 16,
            targetMod: 32,
            stages: [],
            legacyPlan: "16->32 text",
            placeholder: "未配置");
        Assert.Equal("16->32 text", fallbackSummary);
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_HybridModeSwitchesToPerDay_WhenThresholdReached。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_HybridModeSwitchesToPerDay_WhenThresholdReached() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Hybrid",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:CurrentEstimatedRowsPerShard"] = "1500",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:CurrentObservedHotRatio"] = "0.5"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.Empty(evaluation.ValidationErrors);
        Assert.Equal(ParcelShardingStrategyMode.Hybrid, evaluation.Decision.Mode);
        Assert.True(evaluation.Decision.ThresholdReached);
        Assert.Equal(ExpandByDateMode.PerDay, evaluation.Decision.EffectiveDateMode);
        Assert.True(evaluation.Decision.FinerGranularityExtensionPlan.ShouldPlanExtension);
        Assert.Equal(ParcelFinerGranularityMode.PerHour, evaluation.Decision.FinerGranularityExtensionPlan.SuggestedMode);
        Assert.Equal(ParcelFinerGranularityPlanLifecycle.PlanOnly, evaluation.Decision.FinerGranularityExtensionPlan.Lifecycle);
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_HybridModeSwitchesToPerDay_WhenHotThresholdReachedOnly。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_HybridModeSwitchesToPerDay_WhenHotThresholdReachedOnly() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Hybrid",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:CurrentEstimatedRowsPerShard"] = "900",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:CurrentObservedHotRatio"] = "0.9"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.Empty(evaluation.ValidationErrors);
        Assert.True(evaluation.Decision.ThresholdReached);
        Assert.Equal(ExpandByDateMode.PerDay, evaluation.Decision.EffectiveDateMode);
        Assert.Contains("Trigger=hot", evaluation.Decision.Reason, StringComparison.Ordinal);
        Assert.True(evaluation.Decision.FinerGranularityExtensionPlan.ShouldPlanExtension);
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_TimeModeUsesConfiguredGranularity。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_TimeModeUsesConfiguredGranularity() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Time",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerDay"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.Empty(evaluation.ValidationErrors);
        Assert.Equal(ParcelShardingStrategyMode.Time, evaluation.Decision.Mode);
        Assert.Equal(ExpandByDateMode.PerDay, evaluation.Decision.EffectiveDateMode);
        Assert.False(evaluation.Decision.ThresholdReached);
        Assert.False(evaluation.Decision.FinerGranularityExtensionPlan.ShouldPlanExtension);
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_TimeMode_ShouldIgnoreInvalidVolumeConfig。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_TimeMode_ShouldIgnoreInvalidVolumeConfig() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Time",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "invalid",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "-1",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "invalid"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.Empty(evaluation.ValidationErrors);
        Assert.Equal(ParcelShardingStrategyMode.Time, evaluation.Decision.Mode);
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_ShouldRejectNumericEnumValues。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_ShouldRejectNumericEnumValues() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "1",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "1"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.NotEmpty(evaluation.ValidationErrors);
        Assert.Contains(evaluation.ValidationErrors, message => message.Contains("Strategy:Mode", StringComparison.Ordinal));
        Assert.Contains(evaluation.ValidationErrors, message => message.Contains("Strategy:Time:Granularity", StringComparison.Ordinal));
        Assert.Equal(ParcelShardingStrategyMode.Time, evaluation.Decision.Mode);
        Assert.Equal(ExpandByDateMode.PerMonth, evaluation.Decision.EffectiveDateMode);
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_ShouldRejectNumericThresholdAction。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_ShouldRejectNumericThresholdAction() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Hybrid",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "1",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:CurrentEstimatedRowsPerShard"] = "0",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.Contains(evaluation.ValidationErrors, message => message.Contains("ActionOnThreshold", StringComparison.Ordinal));
        Assert.Equal(ParcelVolumeThresholdAction.AlertOnly, evaluation.Decision.ThresholdAction);
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_ValidationErrors_ShouldBeImmutableSnapshot。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_ValidationErrors_ShouldBeImmutableSnapshot() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Volume"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.True(evaluation.ValidationErrors.Count > 0);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)evaluation.ValidationErrors)[0] = "mutate");
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_ReportsValidationErrors_WhenVolumeConfigMissing。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_ReportsValidationErrors_WhenVolumeConfigMissing() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Volume",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "AlertOnly"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.NotEmpty(evaluation.ValidationErrors);
        Assert.Contains(evaluation.ValidationErrors, message => message.Contains("MaxRowsPerShard", StringComparison.Ordinal));
        Assert.Contains(evaluation.ValidationErrors, message => message.Contains("HotThresholdRatio", StringComparison.Ordinal));
        Assert.Equal(ExpandByDateMode.PerMonth, evaluation.Decision.EffectiveDateMode);
    }

    /// <summary>
    /// 验证场景：AddSortingHubPersistence_ShouldFailFast_WhenShardingStrategyInvalid。
    /// </summary>
    [Fact]
    public void AddSortingHubPersistence_ShouldFailFast_WhenShardingStrategyInvalid() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Provider"] = "MySql",
                ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Port=3306;Database=test;Uid=validation_user;Password=placeholder;",
                ["Persistence:Sharding:Strategy:Mode"] = "9"
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSortingHubPersistence(configuration));
        Assert.Contains("分表策略配置校验失败", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_PrefersStructuredObservationInput。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_PrefersStructuredObservationInput() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Hybrid",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:Observation:Source"] = "db-metrics",
                ["Persistence:Sharding:Strategy:Volume:Observation:EstimatedRowsPerShard"] = "1500",
                ["Persistence:Sharding:Strategy:Volume:Observation:ObservedHotRatio"] = "0.2",
                ["Persistence:Sharding:Strategy:Volume:CurrentEstimatedRowsPerShard"] = "1",
                ["Persistence:Sharding:Strategy:Volume:CurrentObservedHotRatio"] = "0.99"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.Empty(evaluation.ValidationErrors);
        Assert.Equal("db-metrics", evaluation.Decision.VolumeObservation.Source);
        Assert.Equal(1500, evaluation.Decision.VolumeObservation.EstimatedRowsPerShard);
        Assert.Equal(0.2m, evaluation.Decision.VolumeObservation.ObservedHotRatio);
        Assert.True(evaluation.Decision.ThresholdReached);
        Assert.Equal(ExpandByDateMode.PerDay, evaluation.Decision.EffectiveDateMode);
        Assert.True(evaluation.Decision.FinerGranularityExtensionPlan.ShouldPlanExtension);
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_OutputsBucketedPerDayExtensionPlan_WhenConfigured。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_OutputsBucketedPerDayExtensionPlan_WhenConfigured() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Hybrid",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:Observation:EstimatedRowsPerShard"] = "2000",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:ModeWhenPerDayStillHot"] = "BucketedPerDay",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:Lifecycle"] = "FutureExecutable",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:Bucket:BucketCount"] = "16"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.Empty(evaluation.ValidationErrors);
        Assert.True(evaluation.Decision.FinerGranularityExtensionPlan.ShouldPlanExtension);
        Assert.Equal(ParcelFinerGranularityMode.BucketedPerDay, evaluation.Decision.FinerGranularityExtensionPlan.SuggestedMode);
        Assert.Equal(ParcelFinerGranularityPlanLifecycle.FutureExecutable, evaluation.Decision.FinerGranularityExtensionPlan.Lifecycle);
        Assert.Contains("BucketCount=16", evaluation.Decision.FinerGranularityExtensionPlan.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 PerHour 模式下缺失 BucketCount 时应通过校验。
    /// </summary>
    [Fact]
    public void PerHourMode_MissingBucketCount_ShouldPassValidation() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Hybrid",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:ModeWhenPerDayStillHot"] = "PerHour"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
        Assert.Empty(evaluation.ValidationErrors);
    }

    /// <summary>
    /// 验证 None 模式下缺失 BucketCount 时应通过校验。
    /// </summary>
    [Fact]
    public void NoneMode_MissingBucketCount_ShouldPassValidation() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Hybrid",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:ModeWhenPerDayStillHot"] = "None"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
        Assert.Empty(evaluation.ValidationErrors);
    }

    /// <summary>
    /// 验证 BucketedPerDay 模式下缺失 BucketCount 时应触发校验错误。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_ShouldFailValidation_WhenBucketedPerDayMissingBucketCount() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Volume",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:ModeWhenPerDayStillHot"] = "BucketedPerDay"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);

        Assert.Contains(evaluation.ValidationErrors, message => message.Contains("BucketCount", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_ShouldFailValidation_WhenBucketCountOutOfRange。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_ShouldFailValidation_WhenBucketCountOutOfRange() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Volume",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:ModeWhenPerDayStillHot"] = "BucketedPerDay",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:Bucket:BucketCount"] = "1"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
        Assert.Contains(evaluation.ValidationErrors, message => message.Contains("范围必须在 2~128", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_ShouldFailValidation_WhenRequirePrebuildGuardInvalidBoolean。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_ShouldFailValidation_WhenRequirePrebuildGuardInvalidBoolean() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Volume",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:RequirePrebuildGuard"] = "not-bool"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
        Assert.Contains(evaluation.ValidationErrors, message => message.Contains("允许值：true/false", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证场景：ParcelShardingStrategyEvaluator_ShouldFailValidation_WhenBucketCountConfiguredButModeIsNotBucketed。
    /// </summary>
    [Fact]
    public void ParcelShardingStrategyEvaluator_ShouldFailValidation_WhenBucketCountConfiguredButModeIsNotBucketed() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Hybrid",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "1000",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:ModeWhenPerDayStillHot"] = "PerHour",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:Bucket:BucketCount"] = "8"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
        Assert.Contains(evaluation.ValidationErrors, message => message.Contains("当前不会生效", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证与默认示例关键字段组合一致的内存配置不会触发分表策略校验错误。
    /// </summary>
    [Fact]
    public void DefaultExampleKeyCombinationInMemory_ShouldPassValidation() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Hybrid",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth",
                ["Persistence:Sharding:Strategy:Volume:ActionOnThreshold"] = "SwitchToPerDay",
                ["Persistence:Sharding:Strategy:Volume:MaxRowsPerShard"] = "10000000",
                ["Persistence:Sharding:Strategy:Volume:HotThresholdRatio"] = "0.8",
                ["Persistence:Sharding:Strategy:Volume:Observation:Source"] = "config-static",
                ["Persistence:Sharding:Strategy:Volume:Observation:EstimatedRowsPerShard"] = "0",
                ["Persistence:Sharding:Strategy:Volume:Observation:ObservedHotRatio"] = "0",
                ["Persistence:Sharding:Strategy:Volume:CurrentEstimatedRowsPerShard"] = "0",
                ["Persistence:Sharding:Strategy:Volume:CurrentObservedHotRatio"] = "0",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:ModeWhenPerDayStillHot"] = "PerHour",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:Lifecycle"] = "PlanOnly",
                ["Persistence:Sharding:Strategy:Volume:FinerGranularity:RequirePrebuildGuard"] = "true"
            })
            .Build();

        var evaluation = ParcelShardingStrategyEvaluator.Evaluate(configuration);
        Assert.Empty(evaluation.ValidationErrors);
    }

    /// <summary>
    /// 验证场景：ShouldEnforcePerDayPrebuildGuard_UsesUnifiedFinerPlan。
    /// </summary>
    [Fact]
    public void ShouldEnforcePerDayPrebuildGuard_UsesUnifiedFinerPlan() {
        var decision = new ParcelShardingStrategyDecision(
            Mode: ParcelShardingStrategyMode.Hybrid,
            TimeGranularity: ParcelTimeShardingGranularity.PerMonth,
            ThresholdAction: ParcelVolumeThresholdAction.SwitchToPerDay,
            VolumeObservation: new ParcelShardingVolumeObservation("config-static", 2000, 0.9m),
            ThresholdReached: true,
            EffectiveDateMode: ExpandByDateMode.PerDay,
            FinerGranularityExtensionPlan: new ParcelFinerGranularityExtensionPlan(
                ShouldPlanExtension: true,
                SuggestedMode: ParcelFinerGranularityMode.PerHour,
                Lifecycle: ParcelFinerGranularityPlanLifecycle.PlanOnly,
                RequiresPrebuildGuard: true,
                Reason: "test"),
            Reason: "test",
            ConfigSnapshot: new ParcelShardingStrategyConfigSnapshot(
                Mode: ParcelShardingStrategyMode.Hybrid,
                TimeGranularity: ParcelTimeShardingGranularity.PerMonth,
                ThresholdAction: ParcelVolumeThresholdAction.SwitchToPerDay,
                MaxRowsPerShard: 1000,
                HotThresholdRatio: 0.8m,
                VolumeObservation: new ParcelShardingVolumeObservation("config-static", 2000, 0.9m),
                FinerGranularity: new ParcelFinerGranularityStrategySnapshot(
                    ModeWhenPerDayStillHot: ParcelFinerGranularityMode.PerHour,
                    Lifecycle: ParcelFinerGranularityPlanLifecycle.PlanOnly,
                    RequirePrebuildGuard: true,
                    BucketCount: null)));

        var shouldEnforce = DatabaseInitializerHostedService.ShouldEnforcePerDayPrebuildGuard(decision);
        Assert.True(shouldEnforce);
    }

    /// <summary>
    /// 验证场景：ShouldEnforcePerDayPrebuildGuard_ReturnsTrue_WhenPerDayAndNoExtensionPlan。
    /// </summary>
    [Fact]
    public void ShouldEnforcePerDayPrebuildGuard_ReturnsTrue_WhenPerDayAndNoExtensionPlan() {
        var decision = new ParcelShardingStrategyDecision(
            Mode: ParcelShardingStrategyMode.Time,
            TimeGranularity: ParcelTimeShardingGranularity.PerDay,
            ThresholdAction: ParcelVolumeThresholdAction.AlertOnly,
            VolumeObservation: new ParcelShardingVolumeObservation("config-static", null, null),
            ThresholdReached: false,
            EffectiveDateMode: ExpandByDateMode.PerDay,
            FinerGranularityExtensionPlan: new ParcelFinerGranularityExtensionPlan(
                ShouldPlanExtension: false,
                SuggestedMode: ParcelFinerGranularityMode.None,
                Lifecycle: ParcelFinerGranularityPlanLifecycle.PlanOnly,
                RequiresPrebuildGuard: false,
                Reason: "not-triggered"),
            Reason: "test",
            ConfigSnapshot: new ParcelShardingStrategyConfigSnapshot(
                Mode: ParcelShardingStrategyMode.Time,
                TimeGranularity: ParcelTimeShardingGranularity.PerDay,
                ThresholdAction: ParcelVolumeThresholdAction.AlertOnly,
                MaxRowsPerShard: null,
                HotThresholdRatio: null,
                VolumeObservation: new ParcelShardingVolumeObservation("config-static", null, null),
                FinerGranularity: new ParcelFinerGranularityStrategySnapshot(
                    ModeWhenPerDayStillHot: ParcelFinerGranularityMode.None,
                    Lifecycle: ParcelFinerGranularityPlanLifecycle.PlanOnly,
                    RequirePrebuildGuard: false,
                    BucketCount: null)));

        var shouldEnforce = DatabaseInitializerHostedService.ShouldEnforcePerDayPrebuildGuard(decision);
        Assert.True(shouldEnforce);
    }

    /// <summary>
    /// 验证场景：ShouldEnforcePerDayPrebuildGuard_ReturnsFalse_WhenNotPerDay。
    /// </summary>
    [Fact]
    public void ShouldEnforcePerDayPrebuildGuard_ReturnsFalse_WhenNotPerDay() {
        var decision = new ParcelShardingStrategyDecision(
            Mode: ParcelShardingStrategyMode.Hybrid,
            TimeGranularity: ParcelTimeShardingGranularity.PerMonth,
            ThresholdAction: ParcelVolumeThresholdAction.SwitchToPerDay,
            VolumeObservation: new ParcelShardingVolumeObservation("config-static", 2000, 0.9m),
            ThresholdReached: true,
            EffectiveDateMode: ExpandByDateMode.PerMonth,
            FinerGranularityExtensionPlan: new ParcelFinerGranularityExtensionPlan(
                ShouldPlanExtension: true,
                SuggestedMode: ParcelFinerGranularityMode.PerHour,
                Lifecycle: ParcelFinerGranularityPlanLifecycle.FutureExecutable,
                RequiresPrebuildGuard: true,
                Reason: "test"),
            Reason: "test",
            ConfigSnapshot: new ParcelShardingStrategyConfigSnapshot(
                Mode: ParcelShardingStrategyMode.Hybrid,
                TimeGranularity: ParcelTimeShardingGranularity.PerMonth,
                ThresholdAction: ParcelVolumeThresholdAction.SwitchToPerDay,
                MaxRowsPerShard: 1000,
                HotThresholdRatio: 0.8m,
                VolumeObservation: new ParcelShardingVolumeObservation("config-static", 2000, 0.9m),
                FinerGranularity: new ParcelFinerGranularityStrategySnapshot(
                    ModeWhenPerDayStillHot: ParcelFinerGranularityMode.PerHour,
                    Lifecycle: ParcelFinerGranularityPlanLifecycle.FutureExecutable,
                    RequirePrebuildGuard: true,
                    BucketCount: null)));

        var shouldEnforce = DatabaseInitializerHostedService.ShouldEnforcePerDayPrebuildGuard(decision);
        Assert.False(shouldEnforce);
    }

    /// <summary>
    /// 验证场景：ShouldEnforcePerDayPrebuildGuard_ReturnsTrue_WhenPerDayEvenIfPlanSaysNoPrebuild。
    /// </summary>
    [Fact]
    public void ShouldEnforcePerDayPrebuildGuard_ReturnsTrue_WhenPerDayEvenIfPlanSaysNoPrebuild() {
        var decision = new ParcelShardingStrategyDecision(
            Mode: ParcelShardingStrategyMode.Hybrid,
            TimeGranularity: ParcelTimeShardingGranularity.PerMonth,
            ThresholdAction: ParcelVolumeThresholdAction.SwitchToPerDay,
            VolumeObservation: new ParcelShardingVolumeObservation("config-static", 2000, 0.9m),
            ThresholdReached: true,
            EffectiveDateMode: ExpandByDateMode.PerDay,
            FinerGranularityExtensionPlan: new ParcelFinerGranularityExtensionPlan(
                ShouldPlanExtension: true,
                SuggestedMode: ParcelFinerGranularityMode.PerHour,
                Lifecycle: ParcelFinerGranularityPlanLifecycle.FutureExecutable,
                RequiresPrebuildGuard: false,
                Reason: "test"),
            Reason: "test",
            ConfigSnapshot: new ParcelShardingStrategyConfigSnapshot(
                Mode: ParcelShardingStrategyMode.Hybrid,
                TimeGranularity: ParcelTimeShardingGranularity.PerMonth,
                ThresholdAction: ParcelVolumeThresholdAction.SwitchToPerDay,
                MaxRowsPerShard: 1000,
                HotThresholdRatio: 0.8m,
                VolumeObservation: new ParcelShardingVolumeObservation("config-static", 2000, 0.9m),
                FinerGranularity: new ParcelFinerGranularityStrategySnapshot(
                    ModeWhenPerDayStillHot: ParcelFinerGranularityMode.PerHour,
                    Lifecycle: ParcelFinerGranularityPlanLifecycle.FutureExecutable,
                    RequirePrebuildGuard: false,
                    BucketCount: null)));

        var shouldEnforce = DatabaseInitializerHostedService.ShouldEnforcePerDayPrebuildGuard(decision);
        Assert.True(shouldEnforce);
    }

    /// <summary>
    /// 验证场景：ShardingGovernanceGuard_AutoPath_ShouldNotRequirePrebuiltDates。
    /// </summary>
    [Fact]
    public async Task ShardingGovernanceGuard_AutoPath_ShouldNotRequirePrebuiltDates() {
        var configuration = BuildPerDayGovernanceConfiguration(
            createShardingTableOnStarting: false,
            timeGranularity: "PerDay",
            prebuildWindowHours: 48);
        var probe = new AlwaysExistsShardingPhysicalTableProbe();
        var service = CreateDatabaseInitializerHostedService(configuration, probe);

        var exception = await Record.ExceptionAsync(() => InvokeShardingGovernanceGuardAsync(service));
        Assert.Null(exception);
    }

    /// <summary>
    /// 验证场景：DatabaseInitializer_RetryableExceptionPath_ShouldKeepRetrySemantics。
    /// </summary>
    [Fact]
    public async Task DatabaseInitializer_RetryableExceptionPath_ShouldKeepRetrySemantics() {
        var logger = new TestLogger<DatabaseInitializerHostedService>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Migration:FailureStrategy:NonProduction"] = "Degraded",
                ["Persistence:Sharding:CreateShardingTableOnStarting"] = "true",
                                ["Persistence:Sharding:Governance:Runbook"] = "docs/internal/sharding-governance-runbook",
                ["Persistence:Sharding:HashSharding:ExpansionPlan:CurrentMod"] = "16",
                ["Persistence:Sharding:HashSharding:ExpansionPlan:TargetMod"] = "32",
                ["Persistence:Sharding:Strategy:Mode"] = "Time",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerMonth"
            })
            .Build();
        var service = new DatabaseInitializerHostedService(
            new ServiceCollection().BuildServiceProvider(),
            logger,
            new TestDialect(),
            new AlwaysExistsShardingPhysicalTableProbe(),
            new TestHostEnvironment("Development"),
            configuration,
            new MigrationGovernanceStateStore());

        var exception = await Record.ExceptionAsync(() => service.StartAsync(CancellationToken.None));
        Assert.Null(exception);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("数据库初始化重试中", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证场景：SqlServerDialect_BatchProbeSql_ShouldUseSchemaParameter。
    /// </summary>
    [Fact]
    public void SqlServerDialect_BatchProbeSql_ShouldUseSchemaParameter() {
        var sqlField = typeof(SqlServerDialect).GetField(
            "BatchShardingProbeSql",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(sqlField);
        var sql = sqlField!.GetRawConstantValue() as string;
        Assert.False(string.IsNullOrWhiteSpace(sql));
        Assert.Contains("@p0", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("N'dbo'", sql, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：ResolveCriticalIndexesForProvider_ShouldIncludeChuteAndBagIndexes。
    /// </summary>
    [Fact]
    public void ResolveCriticalIndexesForProvider_ShouldIncludeChuteAndBagIndexes() {
        var mySqlIndexes = DatabaseInitializerHostedService.ResolveCriticalIndexesForProvider("MySQL");
        var sqlServerIndexes = DatabaseInitializerHostedService.ResolveCriticalIndexesForProvider("SQLServer");
        var mySqlAuditOnlyIndexes = DatabaseInitializerHostedService.ResolveAuditOnlyIndexesForProvider("MySQL");

        Assert.Contains(ParcelIndexNames.BagCodeScannedTime, mySqlIndexes);
        Assert.Contains(ParcelIndexNames.ActualChuteIdScannedTime, mySqlIndexes);
        Assert.Contains(ParcelIndexNames.TargetChuteIdScannedTime, mySqlIndexes);
        Assert.Contains(ParcelIndexNames.BagCodeScannedTime, sqlServerIndexes);
        Assert.Contains(ParcelIndexNames.ActualChuteIdScannedTime, sqlServerIndexes);
        Assert.Contains(ParcelIndexNames.TargetChuteIdScannedTime, sqlServerIndexes);
        Assert.DoesNotContain(ParcelIndexNames.BarCodesFullText, mySqlIndexes);
        Assert.DoesNotContain(ParcelIndexNames.BarCodesFullText, sqlServerIndexes);
        Assert.Contains(ParcelIndexNames.BarCodesFullText, mySqlAuditOnlyIndexes);
    }

    /// <summary>
    /// 验证场景：CriticalIndexAudit_DispatchesByLogicalTable_ShouldHitParcelAndWebRequestAuditLogSeparately。
    /// </summary>
    [Fact]
    public void CriticalIndexAudit_DispatchesByLogicalTable_ShouldHitParcelAndWebRequestAuditLogSeparately() {
        var criticalIndexes = DatabaseInitializerHostedService.ResolveCriticalIndexesByLogicalTableForProvider("MySQL");
        var auditOnlyIndexes = DatabaseInitializerHostedService.ResolveAuditOnlyIndexesByLogicalTableForProvider("MySQL");

        Assert.Contains("Parcels", criticalIndexes.Keys);
        Assert.Contains("WebRequestAuditLogs", criticalIndexes.Keys);
        Assert.Contains("WebRequestAuditLogDetails", criticalIndexes.Keys);
        Assert.Contains(ParcelIndexNames.BagCodeScannedTime, criticalIndexes["Parcels"]);
        Assert.Contains(WebRequestAuditLogIndexNames.StartedAt, criticalIndexes["WebRequestAuditLogs"]);
        Assert.Contains("IX_WebRequestAuditLogDetails_StartedAt", criticalIndexes["WebRequestAuditLogDetails"]);
        Assert.Contains("Parcels", auditOnlyIndexes.Keys);
        Assert.Contains(ParcelIndexNames.BarCodesFullText, auditOnlyIndexes["Parcels"]);
    }


    /// <summary>
    /// 验证场景：IsolationPolicy_DryRun_DoesNotExecuteSql。
    /// </summary>
    [Fact]
    public void IsolationPolicy_DryRun_DoesNotExecuteSql() {
        var decision = ActionIsolationPolicy.Evaluate(
            enableGuard: true,
            allowDangerousActionExecution: true,
            enableDryRun: true,
            dangerousAction: false,
            isRollback: false);

        Assert.Equal(ActionIsolationDecision.DryRunOnly, decision);
    }

    /// <summary>
    /// 验证场景：WebRequestAuditLogRetentionDecision_GuardDryRunExecute_ShouldKeepPlannedAndExecutedSemanticsConsistent。
    /// </summary>
    [Fact]
    public void WebRequestAuditLogRetentionDecision_GuardDryRunExecute_ShouldKeepPlannedAndExecutedSemanticsConsistent() {
        var blocked = DatabaseInitializerHostedService.EvaluateWebRequestAuditLogRetentionDecision(
            candidateCount: 6,
            enableGuard: true,
            allowDangerousActionExecution: false,
            enableDryRun: true);
        var dryRun = DatabaseInitializerHostedService.EvaluateWebRequestAuditLogRetentionDecision(
            candidateCount: 6,
            enableGuard: true,
            allowDangerousActionExecution: true,
            enableDryRun: true);
        var executed = DatabaseInitializerHostedService.EvaluateWebRequestAuditLogRetentionDecision(
            candidateCount: 6,
            enableGuard: false,
            allowDangerousActionExecution: false,
            enableDryRun: false);

        Assert.Equal(ActionIsolationDecision.BlockedByGuard, blocked.Decision);
        Assert.Equal(6, blocked.PlannedCount);
        Assert.Equal(0, blocked.ExecutedCount);
        Assert.True(blocked.IsBlockedByGuard);
        Assert.False(blocked.IsDryRun);

        Assert.Equal(ActionIsolationDecision.DryRunOnly, dryRun.Decision);
        Assert.Equal(6, dryRun.PlannedCount);
        Assert.Equal(0, dryRun.ExecutedCount);
        Assert.False(dryRun.IsBlockedByGuard);
        Assert.True(dryRun.IsDryRun);

        Assert.Equal(ActionIsolationDecision.Execute, executed.Decision);
        Assert.Equal(6, executed.PlannedCount);
        Assert.Equal(6, executed.ExecutedCount);
        Assert.False(executed.IsBlockedByGuard);
        Assert.False(executed.IsDryRun);
    }

    /// <summary>
    /// 验证场景：ResolvePerDayGovernanceGroups_ShouldUseShardingRegistrationSameSourceForWebRequestAuditLog。
    /// </summary>
    [Fact]
    public void ResolvePerDayGovernanceGroups_ShouldUseShardingRegistrationSameSourceForWebRequestAuditLog() {
        var options = new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase($"resolve-governance-webrequest-{Guid.NewGuid():N}")
            .Options;
        using var db = new SortingHubDbContext(options);
        var decision = new ParcelShardingStrategyDecision(
            Mode: ParcelShardingStrategyMode.Time,
            TimeGranularity: ParcelTimeShardingGranularity.PerMonth,
            ThresholdAction: ParcelVolumeThresholdAction.AlertOnly,
            VolumeObservation: new ParcelShardingVolumeObservation("test", null, null),
            ThresholdReached: false,
            EffectiveDateMode: ExpandByDateMode.PerMonth,
            FinerGranularityExtensionPlan: new ParcelFinerGranularityExtensionPlan(
                ShouldPlanExtension: false,
                SuggestedMode: ParcelFinerGranularityMode.None,
                Lifecycle: ParcelFinerGranularityPlanLifecycle.PlanOnly,
                RequiresPrebuildGuard: false,
                Reason: "test"),
            Reason: "test",
            ConfigSnapshot: new ParcelShardingStrategyConfigSnapshot(
                Mode: ParcelShardingStrategyMode.Time,
                TimeGranularity: ParcelTimeShardingGranularity.PerMonth,
                ThresholdAction: ParcelVolumeThresholdAction.AlertOnly,
                MaxRowsPerShard: null,
                HotThresholdRatio: null,
                VolumeObservation: new ParcelShardingVolumeObservation("test", null, null),
                FinerGranularity: new ParcelFinerGranularityStrategySnapshot(
                    ModeWhenPerDayStillHot: ParcelFinerGranularityMode.None,
                    Lifecycle: ParcelFinerGranularityPlanLifecycle.PlanOnly,
                    RequirePrebuildGuard: false,
                    BucketCount: null)));

        var groups = DatabaseInitializerHostedService.ResolvePerDayGovernanceGroups(
            dbContext: db,
            parcelShardingDecision: decision,
            enableWebRequestAuditLogPerDayGuard: true);

        var webRequestGroup = Assert.Single(groups, static group => group.GroupName == "WebRequestAuditLog");
        Assert.Contains("WebRequestAuditLogs", webRequestGroup.BaseTableNames);
        Assert.Contains("WebRequestAuditLogDetails", webRequestGroup.BaseTableNames);
        Assert.Equal(2, webRequestGroup.BaseTableNames.Count);
    }

    /// <summary>
    /// 验证场景：EstimateWebRequestAuditLogRetentionCandidates_KeepRecentBoundary_ShouldReturnExpectedCount。
    /// </summary>
    [Fact]
    public void EstimateWebRequestAuditLogRetentionCandidates_KeepRecentBoundary_ShouldReturnExpectedCount() {
        var requiredDates = new[] {
            DateTime.Now.Date.AddDays(-4),
            DateTime.Now.Date.AddDays(-3),
            DateTime.Now.Date.AddDays(-2),
            DateTime.Now.Date.AddDays(-1)
        };
        var governanceGroups = new[] {
            new PerDayGovernanceGroup(
                GroupName: "WebRequestAuditLog",
                BaseTableNames: new[] { "WebRequestAuditLogs", "WebRequestAuditLogDetails" })
        };

        var candidatesWhenKeep2 = DatabaseInitializerHostedService.EstimateWebRequestAuditLogRetentionCandidates(
            requiredDates,
            keepRecentShardCount: 2,
            governanceGroups);
        var candidatesWhenKeep5 = DatabaseInitializerHostedService.EstimateWebRequestAuditLogRetentionCandidates(
            requiredDates,
            keepRecentShardCount: 5,
            governanceGroups);

        Assert.Equal(4, candidatesWhenKeep2);
        Assert.Equal(0, candidatesWhenKeep5);
    }

    /// <summary>
    /// 验证场景：WebRequestAuditLogRetention_MetadataCandidatesAndDryRun_ShouldUseRealPhysicalMetadataAndEmitObservability。
    /// </summary>
    [Fact]
    public async Task WebRequestAuditLogRetention_MetadataCandidatesAndDryRun_ShouldUseRealPhysicalMetadataAndEmitObservability() {
        var configuration = BuildPerDayGovernanceConfiguration(
            createShardingTableOnStarting: false,
            timeGranularity: "PerMonth",
            prebuildWindowHours: 24,
            enableWebRequestAuditLogPerDayGuard: true);
        var overrideValues = new Dictionary<string, string?> {
            ["Persistence:Sharding:Governance:WebRequestAuditLog:Retention:KeepRecentShardCount"] = "2",
            ["Persistence:Sharding:Governance:WebRequestAuditLog:Retention:Isolator:EnableGuard"] = "false",
            ["Persistence:Sharding:Governance:WebRequestAuditLog:Retention:Isolator:DryRun"] = "true"
        };
        var mergedConfiguration = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(overrideValues)
            .Build();
        var observability = new TestObservability();
        var serviceProvider = new ServiceCollection()
            .AddSingleton(CreateTestingDbContext())
            .AddSingleton<IAutoTuningObservability>(observability)
            .BuildServiceProvider();
        var probe = new AlwaysExistsShardingPhysicalTableProbe();
        var service = new DatabaseInitializerHostedService(
            serviceProvider,
            new TestLogger<DatabaseInitializerHostedService>(),
            new TestSqlServerDialect(),
            probe,
            new TestHostEnvironment("Development"),
            mergedConfiguration,
            new MigrationGovernanceStateStore());

        var exception = await Record.ExceptionAsync(() => InvokeShardingGovernanceGuardAsync(service));
        Assert.Null(exception);
        Assert.True(probe.ListPhysicalTablesCallCount > 0);
        Assert.Contains(observability.Metrics, metricName => string.Equals(metricName, "web_request_audit_log.retention.executed_count", StringComparison.Ordinal));
        var executedMetric = Assert.Single(
            observability.MetricEntries,
            static entry => string.Equals(entry.Name, "web_request_audit_log.retention.executed_count", StringComparison.Ordinal));
        Assert.Equal(0d, executedMetric.Value);
        Assert.Contains(
            observability.EventEntries,
            entry => string.Equals(entry.Name, "web_request_audit_log.retention.skipped", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证场景：IsolationPolicy_BlocksDangerousAction_WhenNotAllowed。
    /// </summary>
    [Fact]
    public void IsolationPolicy_BlocksDangerousAction_WhenNotAllowed() {
        var decision = ActionIsolationPolicy.Evaluate(
            enableGuard: true,
            allowDangerousActionExecution: false,
            enableDryRun: false,
            dangerousAction: true,
            isRollback: false);

        Assert.Equal(ActionIsolationDecision.BlockedByGuard, decision);
    }

    /// <summary>
    /// 验证场景：EnsureDatabaseExistsDecision_GuardDryRunExecute_ShouldKeepPlannedAndExecutedSemanticsConsistent。
    /// </summary>
    [Fact]
    public void EnsureDatabaseExistsDecision_GuardDryRunExecute_ShouldKeepPlannedAndExecutedSemanticsConsistent() {
        var blocked = DatabaseInitializerHostedService.EvaluateEnsureDatabaseExistsDecision(
            databaseMissing: true,
            enableGuard: true,
            allowDangerousActionExecution: false,
            enableDryRun: false);
        var dryRun = DatabaseInitializerHostedService.EvaluateEnsureDatabaseExistsDecision(
            databaseMissing: true,
            enableGuard: true,
            allowDangerousActionExecution: true,
            enableDryRun: true);
        var executed = DatabaseInitializerHostedService.EvaluateEnsureDatabaseExistsDecision(
            databaseMissing: true,
            enableGuard: false,
            allowDangerousActionExecution: false,
            enableDryRun: false);

        Assert.Equal(ActionIsolationDecision.BlockedByGuard, blocked.Decision);
        Assert.Equal(1, blocked.PlannedCount);
        Assert.Equal(0, blocked.ExecutedCount);
        Assert.True(blocked.IsBlockedByGuard);
        Assert.False(blocked.IsDryRun);

        Assert.Equal(ActionIsolationDecision.DryRunOnly, dryRun.Decision);
        Assert.Equal(1, dryRun.PlannedCount);
        Assert.Equal(0, dryRun.ExecutedCount);
        Assert.False(dryRun.IsBlockedByGuard);
        Assert.True(dryRun.IsDryRun);

        Assert.Equal(ActionIsolationDecision.Execute, executed.Decision);
        Assert.Equal(1, executed.PlannedCount);
        Assert.Equal(1, executed.ExecutedCount);
        Assert.False(executed.IsBlockedByGuard);
        Assert.False(executed.IsDryRun);
    }

    /// <summary>
    /// 验证场景：EnsureDatabaseExistsDecision_WhenDatabaseAlreadyExists_ShouldSkipCreatePlan。
    /// </summary>
    [Fact]
    public void EnsureDatabaseExistsDecision_WhenDatabaseAlreadyExists_ShouldSkipCreatePlan() {
        var decision = DatabaseInitializerHostedService.EvaluateEnsureDatabaseExistsDecision(
            databaseMissing: false,
            enableGuard: true,
            allowDangerousActionExecution: false,
            enableDryRun: true);

        Assert.Equal(ActionIsolationDecision.Execute, decision.Decision);
        Assert.Equal(0, decision.PlannedCount);
        Assert.Equal(0, decision.ExecutedCount);
        Assert.False(decision.IsBlockedByGuard);
        Assert.False(decision.IsDryRun);
    }

    /// <summary>
    /// 验证场景：ResolveProviderConnectionStringKey_ShouldResolveMySqlAndSqlServerAndUnknown。
    /// </summary>
    [Fact]
    public void ResolveProviderConnectionStringKey_ShouldResolveMySqlAndSqlServerAndUnknown() {
        var mySqlByConfig = DatabaseInitializerHostedService.ResolveProviderConnectionStringKey("MySql", "Test");
        var sqlServerByDialect = DatabaseInitializerHostedService.ResolveProviderConnectionStringKey(null, "SQLServer");
        var unknown = DatabaseInitializerHostedService.ResolveProviderConnectionStringKey("Test", "Test");

        Assert.Equal("MySql", mySqlByConfig);
        Assert.Equal("SqlServer", sqlServerByDialect);
        Assert.Null(unknown);
    }

    /// <summary>
    /// 验证场景：Dialect_ExtractDatabaseNameAndAdminConnection_ShouldFollowProviderSemantics。
    /// </summary>
    [Fact]
    public void Dialect_ExtractDatabaseNameAndAdminConnection_ShouldFollowProviderSemantics() {
        // 仅用于单元测试的本地示例连接字符串，不用于生产环境。
        var mySqlConnectionString = "Server=127.0.0.1;Port=3306;Database=zeye_sorting_hub;User Id=validation_user;Password=placeholder;";
        var sqlServerConnectionString = "Server=127.0.0.1,1433;Database=zeye_sorting_hub;User Id=validation_user;Password=placeholder;TrustServerCertificate=True;Encrypt=False;";

        var mySqlDialect = new MySqlDialect();
        var sqlServerDialect = new SqlServerDialect();

        var mySqlDatabaseName = mySqlDialect.ExtractDatabaseName(mySqlConnectionString);
        var sqlServerDatabaseName = sqlServerDialect.ExtractDatabaseName(sqlServerConnectionString);
        using var mySqlAdminConnection = mySqlDialect.CreateAdministrationConnection(mySqlConnectionString);
        using var sqlServerAdminConnection = sqlServerDialect.CreateAdministrationConnection(sqlServerConnectionString);
        var mySqlAdminBuilder = new MySqlConnectionStringBuilder(mySqlAdminConnection.ConnectionString);
        var sqlServerAdminBuilder = new SqlConnectionStringBuilder(sqlServerAdminConnection.ConnectionString);

        Assert.Equal("zeye_sorting_hub", mySqlDatabaseName);
        Assert.Equal("zeye_sorting_hub", sqlServerDatabaseName);
        Assert.True(string.IsNullOrWhiteSpace(mySqlAdminBuilder.Database));
        Assert.Equal("master", sqlServerAdminBuilder.InitialCatalog);
    }

    /// <summary>
    /// 验证场景：Pipeline_AlertsSupportDebounceAndRecovery。
    /// </summary>
    [Fact]
    public void Pipeline_AlertsSupportDebounceAndRecovery() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:SlowQueryThresholdMilliseconds"] = "10",
                ["Persistence:AutoTuning:AnalysisBatchSize"] = "1",
                ["Persistence:AutoTuning:TriggerCount"] = "1",
                ["Persistence:AutoTuning:AlertDebounceMinCallCount"] = "1",
                ["Persistence:AutoTuning:AlertP99Milliseconds"] = "500",
                ["Persistence:AutoTuning:AlertTimeoutRatePercent"] = "100",
                ["Persistence:AutoTuning:AlertDeadlockCount"] = "99",
                ["Persistence:AutoTuning:AlertDebounceWindowSeconds"] = "3600",
                ["Persistence:AutoTuning:AlertConsecutiveWindows"] = "2",
                ["Persistence:AutoTuning:AlertRecoveryConsecutiveWindows"] = "1"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, new NullAutoTuningObservability());
        var dialect = new TestDialect();
        const string sql = "select * from parcels where parcel_code = @p0";

        pipeline.Collect(sql, TimeSpan.FromMilliseconds(900));
        var first = pipeline.Analyze(dialect);
        Assert.Empty(first.Alerts);

        pipeline.Collect(sql, TimeSpan.FromMilliseconds(900));
        var second = pipeline.Analyze(dialect);
        Assert.Single(second.Alerts);

        pipeline.Collect(sql, TimeSpan.FromMilliseconds(900));
        var third = pipeline.Analyze(dialect);
        Assert.Empty(third.Alerts);

        pipeline.Collect(sql, TimeSpan.FromMilliseconds(100));
        var fourth = pipeline.Analyze(dialect);
        Assert.Single(fourth.RecoveryNotifications);
    }

    /// <summary>
    /// 验证场景：AutoTuningConfigurationReader_BuildKeys_ShouldUseExpectedPrefix。
    /// </summary>
    [Fact]
    public void AutoTuningConfigurationReader_BuildKeys_ShouldUseExpectedPrefix() {
        var autoTuningKey = AutoTuningConfigurationReader.BuildAutoTuningKey("TriggerCount");
        var autonomousKey = AutoTuningConfigurationReader.BuildAutonomousKey("Validation:DelayCycles");

        Assert.Equal("Persistence:AutoTuning:TriggerCount", autoTuningKey);
        Assert.Equal("Persistence:AutoTuning:Autonomous:Validation:DelayCycles", autonomousKey);
    }

    /// <summary>
    /// 验证场景：AutoTuningConfigurationReader_BuildAutoTuningKey_ShouldProduceCorrectPath（参数化）。
    /// 覆盖多个常用配置项后缀，确保拼装规则对未来新增配置项持续有效。
    /// </summary>
    [Theory]
    [InlineData("TriggerCount", "Persistence:AutoTuning:TriggerCount")]
    [InlineData("SlowQueryThresholdMilliseconds", "Persistence:AutoTuning:SlowQueryThresholdMilliseconds")]
    [InlineData("AnalysisBatchSize", "Persistence:AutoTuning:AnalysisBatchSize")]
    [InlineData("AlertP99Milliseconds", "Persistence:AutoTuning:AlertP99Milliseconds")]
    [InlineData("AlertTimeoutRatePercent", "Persistence:AutoTuning:AlertTimeoutRatePercent")]
    [InlineData("AlertDeadlockCount", "Persistence:AutoTuning:AlertDeadlockCount")]
    [InlineData("AlertDebounceWindowSeconds", "Persistence:AutoTuning:AlertDebounceWindowSeconds")]
    [InlineData("AlertConsecutiveWindows", "Persistence:AutoTuning:AlertConsecutiveWindows")]
    [InlineData("AlertRecoveryConsecutiveWindows", "Persistence:AutoTuning:AlertRecoveryConsecutiveWindows")]
    [InlineData("AlertDebounceMinCallCount", "Persistence:AutoTuning:AlertDebounceMinCallCount")]
    public void AutoTuningConfigurationReader_BuildAutoTuningKey_ShouldProduceCorrectPath(string suffix, string expectedKey) {
        var actual = AutoTuningConfigurationReader.BuildAutoTuningKey(suffix);
        Assert.Equal(expectedKey, actual);
    }

    /// <summary>
    /// 验证场景：AutoTuningConfigurationReader_BuildAutonomousKey_ShouldProduceCorrectPath（参数化）。
    /// 覆盖多个 Autonomous 配置子路径，确保前缀拼装规则对嵌套层级一致有效。
    /// </summary>
    [Theory]
    [InlineData("EnableFullAutomation", "Persistence:AutoTuning:Autonomous:EnableFullAutomation")]
    [InlineData("Validation:DelayCycles", "Persistence:AutoTuning:Autonomous:Validation:DelayCycles")]
    [InlineData("CapacityPrediction:EnableCapacityPrediction", "Persistence:AutoTuning:Autonomous:CapacityPrediction:EnableCapacityPrediction")]
    [InlineData("CapacityPrediction:PredictionWindowDays", "Persistence:AutoTuning:Autonomous:CapacityPrediction:PredictionWindowDays")]
    [InlineData("CapacityPrediction:GrowthRateThreshold", "Persistence:AutoTuning:Autonomous:CapacityPrediction:GrowthRateThreshold")]
    [InlineData("SchemaSync:EnableAutoSchemaSync", "Persistence:AutoTuning:Autonomous:SchemaSync:EnableAutoSchemaSync")]
    public void AutoTuningConfigurationReader_BuildAutonomousKey_ShouldProduceCorrectPath(string suffix, string expectedKey) {
        var actual = AutoTuningConfigurationReader.BuildAutonomousKey(suffix);
        Assert.Equal(expectedKey, actual);
    }

    /// <summary>
    /// 验证场景：AutoTuningConfigurationReader_NormalizeToLocalTime_UsesLocalSemantics。
    /// </summary>
    [Fact]
    public void AutoTuningConfigurationReader_NormalizeToLocalTime_UsesLocalSemantics() {
        var unspecified = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Unspecified);
        var normalizedUnspecified = AutoTuningConfigurationReader.NormalizeToLocalTime(unspecified);
        Assert.Equal(DateTimeKind.Local, normalizedUnspecified.Kind);
        Assert.Equal(unspecified, DateTime.SpecifyKind(normalizedUnspecified, DateTimeKind.Unspecified));

        var local = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Local);
        var normalizedLocal = AutoTuningConfigurationReader.NormalizeToLocalTime(local);
        Assert.Equal(local, normalizedLocal);

        var nonLocalKind = Enum.GetValues<DateTimeKind>()
            .First(kind => kind != DateTimeKind.Local && kind != DateTimeKind.Unspecified);
        var nonLocalKindInput = DateTime.SpecifyKind(unspecified, nonLocalKind);
        Assert.Throws<InvalidOperationException>(() => AutoTuningConfigurationReader.NormalizeToLocalTime(nonLocalKindInput));
    }

    /// <summary>
    /// 验证场景：Dialect_IndexNameHash_ShouldStayConsistentAcrossProviders。
    /// </summary>
    [Fact]
    public void Dialect_IndexNameHash_ShouldStayConsistentAcrossProviders() {
        IReadOnlyList<string> whereColumns = new[] { "col_a", "col_b" };
        var mySqlCreateIndexSql = new MySqlDialect().BuildAutomaticTuningSql("demo", "parcel", whereColumns)[0];
        var sqlServerCreateIndexSql = new SqlServerDialect().BuildAutomaticTuningSql("demo", "parcel", whereColumns)[0];

        var mySqlMatch = Regex.Match(mySqlCreateIndexSql, @"CREATE INDEX `(?<name>[^`]+)`", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var sqlServerMatch = Regex.Match(sqlServerCreateIndexSql, @"CREATE INDEX \[(?<name>[^\]]+)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.True(mySqlMatch.Success);
        Assert.True(sqlServerMatch.Success);

        var mySqlName = mySqlMatch.Groups["name"].Value;
        var sqlServerName = sqlServerMatch.Groups["name"].Value;
        var mySqlHash = mySqlName[(mySqlName.LastIndexOf('_') + 1)..];
        var sqlServerHash = sqlServerName[(sqlServerName.LastIndexOf('_') + 1)..];
        Assert.Equal(mySqlHash, sqlServerHash);
    }

    /// <summary>
    /// 验证场景：当 maxLength 小于最小允许值（9）时，BuildIndexName 抛出 ArgumentOutOfRangeException。
    /// </summary>
    [Fact]
    public void BuildIndexName_ShouldThrowArgumentOutOfRangeException_WhenMaxLengthLessThanMinimumRequired() {
        var helperType = typeof(MySqlDialect).Assembly.GetType("Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects.DatabaseProviderOperations");
        Assert.NotNull(helperType);

        var method = helperType!.GetMethod("BuildIndexName", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);

        IReadOnlyList<string> columns = new[] { "col_a" };
        var invocation = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(null, ["dbo", "parcels", columns, 8]));

        var argumentException = Assert.IsType<ArgumentOutOfRangeException>(invocation.InnerException);
        Assert.Equal("maxLength", argumentException.ParamName);
        Assert.Contains("至少为 9", argumentException.Message);
    }

    /// <summary>
    /// 验证场景：UpdateAutonomousSignals_EmitsShardingObservabilityMetrics。
    /// </summary>
    [Fact]
    public void UpdateAutonomousSignals_EmitsShardingObservabilityMetrics() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:Autonomous:EnableFullAutomation"] = "true",
                ["Persistence:AutoTuning:Autonomous:CapacityPrediction:EnableCapacityPrediction"] = "true"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);

        var result = new SlowQueryAnalysisResult(
            DateTime.Now,
            0,
            [
                new SlowQueryMetric(
                    "fp-1",
                    "select * from parcels p join parcel_positions pp on p.id = pp.parcel_id where p.code = @p0",
                    10,
                    1000,
                    0m,
                    0m,
                    0,
                    100d,
                    120d,
                    150d,
                    null),
                new SlowQueryMetric(
                    "fp-2",
                    "select * from parcels where code = @p1",
                    5,
                    300,
                    0m,
                    0m,
                    0,
                    80d,
                    100d,
                    110d,
                    null),
                new SlowQueryMetric(
                    "fp-3",
                    "select * from parcels where id = @p2",
                    20,
                    500,
                    0m,
                    0m,
                    0,
                    90d,
                    110d,
                    130d,
                    null)
            ],
            [
                new SlowQueryTuningCandidate("fp-1", "dbo", "parcels", Array.Empty<string>(), Array.Empty<string>()),
                new SlowQueryTuningCandidate("fp-2", "dbo", "parcel_positions", Array.Empty<string>(), Array.Empty<string>())
            ],
            Array.Empty<string>(),
            Array.Empty<SlowQuerySuggestionInsight>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);
        var updateAutonomousSignals = typeof(DatabaseAutoTuningHostedService).GetMethod("UpdateAutonomousSignals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        updateAutonomousSignals.Invoke(service, [result, DateTime.Now]);

        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hit_rate");
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.cross_table_query_ratio");
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hot_table_skew");
        // 命中调用数 = 5 + 20 = 25，总调用数 = 10 + 5 + 20 = 35。
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hit_rate" && Math.Abs(entry.Value - (25d / 35d)) < DoublePrecisionTolerance);
    }

    /// <summary>
    /// 验证场景：UpdateAutonomousSignals_HitRateSupportsPartialAndNoTableReferenceCases。
    /// </summary>
    [Fact]
    public void UpdateAutonomousSignals_HitRateSupportsPartialAndNoTableReferenceCases() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var fixedNow = DateTime.Now;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:Autonomous:EnableFullAutomation"] = "true",
                ["Persistence:AutoTuning:Autonomous:CapacityPrediction:EnableCapacityPrediction"] = "true"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);
        var updateAutonomousSignals = typeof(DatabaseAutoTuningHostedService).GetMethod("UpdateAutonomousSignals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        var partial = new SlowQueryAnalysisResult(
            fixedNow,
            0,
            [
                new SlowQueryMetric("partial-1", "select * from parcels where code=@p0", 10, 100, 0m, 0m, 0, 10d, 20d, 30d, null),
                new SlowQueryMetric("partial-2", "show status", 10, 0, 0m, 0m, 0, 10d, 20d, 30d, null)
            ],
            Array.Empty<SlowQueryTuningCandidate>(),
            Array.Empty<string>(),
            Array.Empty<SlowQuerySuggestionInsight>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);
        updateAutonomousSignals.Invoke(service, [partial, fixedNow]);
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hit_rate" && Math.Abs(entry.Value - 0.5d) < DoublePrecisionTolerance);

        observability.MetricEntries.Clear();
        var none = new SlowQueryAnalysisResult(
            fixedNow,
            0,
            [
                new SlowQueryMetric("none-1", "show status", 7, 0, 0m, 0m, 0, 10d, 20d, 30d, null)
            ],
            Array.Empty<SlowQueryTuningCandidate>(),
            Array.Empty<string>(),
            Array.Empty<SlowQuerySuggestionInsight>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);
        updateAutonomousSignals.Invoke(service, [none, fixedNow]);
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hit_rate" && Math.Abs(entry.Value) < DoublePrecisionTolerance);
    }

    /// <summary>
    /// 验证场景：UpdateAutonomousSignals_CrossTableRatioDetectsSubQueryWithoutJoinKeyword。
    /// </summary>
    [Fact]
    public void UpdateAutonomousSignals_CrossTableRatioDetectsSubQueryWithoutJoinKeyword() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:Autonomous:EnableFullAutomation"] = "true",
                ["Persistence:AutoTuning:Autonomous:CapacityPrediction:EnableCapacityPrediction"] = "true"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);
        var updateAutonomousSignals = typeof(DatabaseAutoTuningHostedService).GetMethod("UpdateAutonomousSignals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var fixedNow = new DateTime(2026, 3, 17, 10, 0, 0, DateTimeKind.Local);
        var result = new SlowQueryAnalysisResult(
            fixedNow,
            0,
            [
                new SlowQueryMetric(
                    "subquery-cross",
                    "select * from parcels p where exists (select 1 from parcel_positions pp where pp.parcel_id = p.id)",
                    8,
                    80,
                    0m,
                    0m,
                    0,
                    100d,
                    120d,
                    130d,
                    null),
                new SlowQueryMetric(
                    "single-table",
                    "select * from parcels where code = @p0",
                    8,
                    60,
                    0m,
                    0m,
                    0,
                    90d,
                    110d,
                    130d,
                    null)
            ],
            Array.Empty<SlowQueryTuningCandidate>(),
            Array.Empty<string>(),
            Array.Empty<SlowQuerySuggestionInsight>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);

        updateAutonomousSignals.Invoke(service, [result, fixedNow]);
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.cross_table_query_ratio" && Math.Abs(entry.Value - 0.5d) < DoublePrecisionTolerance);
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hit_rate" && Math.Abs(entry.Value - 0.5d) < DoublePrecisionTolerance);
    }

    /// <summary>
    /// 验证场景：UpdateAutonomousSignals_CrossTableRatio_ShouldIgnoreCommasOutsideFromClause。
    /// </summary>
    [Fact]
    public void UpdateAutonomousSignals_CrossTableRatio_ShouldIgnoreCommasOutsideFromClause() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:Autonomous:EnableFullAutomation"] = "true",
                ["Persistence:AutoTuning:Autonomous:CapacityPrediction:EnableCapacityPrediction"] = "true"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);
        var updateAutonomousSignals = typeof(DatabaseAutoTuningHostedService).GetMethod("UpdateAutonomousSignals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var fixedNow = new DateTime(2026, 3, 17, 10, 0, 0, DateTimeKind.Local);
        var result = new SlowQueryAnalysisResult(
            fixedNow,
            0,
            [
                new SlowQueryMetric(
                    "comma-non-from",
                    "select * from parcels where id in (1,2,3) order by code, id",
                    6,
                    60,
                    0m,
                    0m,
                    0,
                    80d,
                    100d,
                    120d,
                    null),
                new SlowQueryMetric(
                    "comma-from",
                    "select * from parcels p, parcel_positions pp where p.id = pp.parcel_id",
                    6,
                    60,
                    0m,
                    0m,
                    0,
                    80d,
                    100d,
                    120d,
                    null)
            ],
            Array.Empty<SlowQueryTuningCandidate>(),
            Array.Empty<string>(),
            Array.Empty<SlowQuerySuggestionInsight>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);

        updateAutonomousSignals.Invoke(service, [result, fixedNow]);
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.cross_table_query_ratio" && Math.Abs(entry.Value - 0.5d) < DoublePrecisionTolerance);
    }

    /// <summary>
    /// 验证场景：UpdateAutonomousSignals_HotTableSkewUsesAllMetricsInsteadOfOnlyCandidates。
    /// </summary>
    [Fact]
    public void UpdateAutonomousSignals_HotTableSkewUsesAllMetricsInsteadOfOnlyCandidates() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:Autonomous:EnableFullAutomation"] = "true",
                ["Persistence:AutoTuning:Autonomous:CapacityPrediction:EnableCapacityPrediction"] = "true"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);
        var updateAutonomousSignals = typeof(DatabaseAutoTuningHostedService).GetMethod("UpdateAutonomousSignals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var fixedNow = new DateTime(2026, 3, 17, 10, 0, 0, DateTimeKind.Local);
        var result = new SlowQueryAnalysisResult(
            fixedNow,
            0,
            [
                new SlowQueryMetric("table-a", "select * from parcels where code=@p0", 30, 300, 0m, 0m, 0, 10d, 20d, 30d, null),
                new SlowQueryMetric("table-b", "select * from parcel_positions where id=@p1", 10, 120, 0m, 0m, 0, 10d, 20d, 30d, null)
            ],
            Array.Empty<SlowQueryTuningCandidate>(),
            Array.Empty<string>(),
            Array.Empty<SlowQuerySuggestionInsight>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);

        updateAutonomousSignals.Invoke(service, [result, fixedNow]);
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hot_table_skew" && Math.Abs(entry.Value - 1.5d) < DoublePrecisionTolerance);
    }

    /// <summary>
    /// 验证场景：ApplyIndexSuggestionGuardsAsync_FiltersCoveredAndLowValueCreateIndexSuggestions。
    /// </summary>
    [Fact]
    public async Task ApplyIndexSuggestionGuardsAsync_FiltersCoveredAndLowValueCreateIndexSuggestions() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:TriggerCount"] = "3",
                ["Persistence:AutoTuning:AlertP99Milliseconds"] = "500"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);
        var modelIndexField = typeof(DatabaseAutoTuningHostedService).GetField("_modelIndexColumnsByTable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var modelIndexes = (Dictionary<string, IReadOnlyList<string[]>>)modelIndexField.GetValue(service)!;
        modelIndexes["parcels"] = [new[] { "code" }];
        var result = new SlowQueryAnalysisResult(
            DateTime.Now,
            0,
            [
                new SlowQueryMetric("fp-covered", "select * from parcels where code=@p0", 3, 10, 0m, 0m, 0, 100d, 200d, 220d, null)
            ],
            [
                new SlowQueryTuningCandidate(
                    "fp-covered",
                    null,
                    "parcels",
                    ["code"],
                    [
                        "CREATE INDEX `idx_auto_parcels_code_x` ON `parcels` (`code`)",
                        "ANALYZE TABLE `parcels`"
                    ])
            ],
            [],
            [
                new SlowQuerySuggestionInsight(
                    "fp-covered",
                    "/*AUTO_TUNING_READ_ONLY*/ CREATE INDEX `idx_auto_parcels_code_x` ON `parcels` (`code`)",
                    "test",
                    "low",
                    0.8m),
                new SlowQuerySuggestionInsight(
                    "fp-covered",
                    "/*AUTO_TUNING_READ_ONLY*/ ANALYZE TABLE `parcels`",
                    "test",
                    "low",
                    0.8m)
            ],
            [],
            [],
            [],
            false,
            false,
            false);
        var method = typeof(DatabaseAutoTuningHostedService).GetMethod("ApplyIndexSuggestionGuardsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var guardedResult = await (Task<SlowQueryAnalysisResult>)method.Invoke(service, [result, CancellationToken.None])!;

        Assert.Single(guardedResult.TuningCandidates);
        Assert.DoesNotContain(guardedResult.TuningCandidates[0].SuggestedActions, action => action.Contains("create index", StringComparison.OrdinalIgnoreCase));
        Assert.Single(guardedResult.SuggestionInsights);
        Assert.Contains(guardedResult.SuggestionInsights, insight => insight.SuggestionSql.Contains("ANALYZE TABLE", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 验证场景：ApplyIndexSuggestionGuardsAsync_KeepsCreateIndexWhenMetricIsHighValue。
    /// </summary>
    [Fact]
    public async Task ApplyIndexSuggestionGuardsAsync_KeepsCreateIndexWhenMetricIsHighValue() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:TriggerCount"] = "3",
                ["Persistence:AutoTuning:AlertP99Milliseconds"] = "500"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);
        var result = new SlowQueryAnalysisResult(
            DateTime.Now,
            0,
            [
                new SlowQueryMetric("fp-keep", "select * from parcels where code=@p0", 7, 100, 0m, 0m, 0, 100d, 800d, 900d, null)
            ],
            [
                new SlowQueryTuningCandidate(
                    "fp-keep",
                    null,
                    "parcels",
                    ["code"],
                    [
                        "CREATE INDEX `idx_auto_parcels_code_x` ON `parcels` (`code`)",
                        "ANALYZE TABLE `parcels`"
                    ])
            ],
            [],
            [
                new SlowQuerySuggestionInsight(
                    "fp-keep",
                    "/*AUTO_TUNING_READ_ONLY*/ CREATE INDEX `idx_auto_parcels_code_x` ON `parcels` (`code`)",
                    "test",
                    "low",
                    0.8m),
                new SlowQuerySuggestionInsight(
                    "fp-keep",
                    "/*AUTO_TUNING_READ_ONLY*/ ANALYZE TABLE `parcels`",
                    "test",
                    "low",
                    0.8m)
            ],
            [],
            [],
            [],
            false,
            false,
            false);
        var method = typeof(DatabaseAutoTuningHostedService).GetMethod("ApplyIndexSuggestionGuardsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var guardedResult = await (Task<SlowQueryAnalysisResult>)method.Invoke(service, [result, CancellationToken.None])!;

        Assert.Single(guardedResult.TuningCandidates);
        Assert.Contains(guardedResult.TuningCandidates[0].SuggestedActions, action => action.Contains("create index", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, guardedResult.SuggestionInsights.Count);
    }

    /// <summary>
    /// 验证场景：ApplyIndexSuggestionGuardsAsync_DoesNotTreatShorterExistingPrefixAsCovered。
    /// </summary>
    [Fact]
    public async Task ApplyIndexSuggestionGuardsAsync_DoesNotTreatShorterExistingPrefixAsCovered() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:TriggerCount"] = "3",
                ["Persistence:AutoTuning:AlertP99Milliseconds"] = "500"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);
        var modelIndexField = typeof(DatabaseAutoTuningHostedService).GetField("_modelIndexColumnsByTable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var modelIndexes = (Dictionary<string, IReadOnlyList<string[]>>)modelIndexField.GetValue(service)!;
        modelIndexes["parcels"] = [new[] { "code" }];
        var result = new SlowQueryAnalysisResult(
            DateTime.Now,
            0,
            [
                new SlowQueryMetric("fp-composite", "select * from parcels where code=@p0 and id=@p1", 8, 200, 0m, 0m, 0, 100d, 900d, 950d, null)
            ],
            [
                new SlowQueryTuningCandidate(
                    "fp-composite",
                    null,
                    "parcels",
                    ["code", "id"],
                    [
                        "CREATE INDEX `idx_auto_parcels_code_id_x` ON `parcels` (`code`, `id`)",
                        "ANALYZE TABLE `parcels`"
                    ])
            ],
            [],
            [
                new SlowQuerySuggestionInsight(
                    "fp-composite",
                    "/*AUTO_TUNING_READ_ONLY*/ CREATE INDEX `idx_auto_parcels_code_id_x` ON `parcels` (`code`, `id`)",
                    "test",
                    "medium",
                    0.8m)
            ],
            [],
            [],
            [],
            false,
            false,
            false);
        var method = typeof(DatabaseAutoTuningHostedService).GetMethod("ApplyIndexSuggestionGuardsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var guardedResult = await (Task<SlowQueryAnalysisResult>)method.Invoke(service, [result, CancellationToken.None])!;

        Assert.Single(guardedResult.TuningCandidates);
        Assert.Contains(guardedResult.TuningCandidates[0].SuggestedActions, action => action.Contains("create index", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 验证场景：ApplyIndexSuggestionGuardsAsync_ShardingGate_BlocksCreateIndexWhenHitRateBelowThreshold。
    /// 分表治理门禁：当分析窗口 hit rate 低于阈值时，命中分表的候选 CreateIndex 动作应被阻断。
    /// </summary>
    [Fact]
    public async Task ApplyIndexSuggestionGuardsAsync_ShardingGate_BlocksCreateIndexWhenHitRateBelowThreshold() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:TriggerCount"] = "3",
                ["Persistence:AutoTuning:AlertP99Milliseconds"] = "500",
                // 分表门禁阈值 0.8，命中率 = 5/(5+20) = 0.2 < 0.8 → 应阻断
                ["Persistence:AutoTuning:ShardingGovernanceHitRateThreshold"] = "0.8"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);
        // fp-sharding 对应单表查询（IsShardingHitQuery=true），调用量 5
        // fp-cross 对应 JOIN 跨表查询（IsCrossTableQuery=true，IsShardingHitQuery=false），调用量 20
        // 总调用量 25，命中 5，hit rate = 0.2 < 0.8，门禁应阻断 fp-sharding 的 CreateIndex
        var result = new SlowQueryAnalysisResult(
            DateTime.Now,
            0,
            [
                new SlowQueryMetric("fp-sharding", "select * from parcels where code=@p0", 5, 50, 0m, 0m, 0, 300d, 600d, 700d, null),
                new SlowQueryMetric("fp-cross", "select * from parcels p join parcel_positions pp on p.id=pp.parcel_id where p.code=@p1", 20, 200, 0m, 0m, 0, 100d, 200d, 250d, null)
            ],
            [
                new SlowQueryTuningCandidate(
                    "fp-sharding",
                    null,
                    "parcels",
                    ["code"],
                    [
                        "CREATE INDEX `idx_auto_parcels_code_x` ON `parcels` (`code`)",
                        "ANALYZE TABLE `parcels`"
                    ])
            ],
            [],
            [],
            [],
            [],
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);
        var method = typeof(DatabaseAutoTuningHostedService).GetMethod("ApplyIndexSuggestionGuardsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var guardedResult = await (Task<SlowQueryAnalysisResult>)method.Invoke(service, [result, CancellationToken.None])!;

        // CreateIndex 动作应被门禁阻断，候选中只剩非 CreateIndex 动作（ANALYZE TABLE）
        Assert.Single(guardedResult.TuningCandidates);
        Assert.DoesNotContain(guardedResult.TuningCandidates[0].SuggestedActions, action => action.Contains("create index", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(observability.EventEntries, e => e.Name == "autotuning.sharding.governance_gate_blocked");
    }

    /// <summary>
    /// 验证场景：ApplyIndexSuggestionGuardsAsync_ShardingGate_KeepsCreateIndexWhenHitRateAboveThreshold。
    /// 分表治理门禁：当分析窗口 hit rate 高于阈值时，分表候选的 CreateIndex 动作不应被阻断。
    /// </summary>
    [Fact]
    public async Task ApplyIndexSuggestionGuardsAsync_ShardingGate_KeepsCreateIndexWhenHitRateAboveThreshold() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:TriggerCount"] = "3",
                ["Persistence:AutoTuning:AlertP99Milliseconds"] = "500",
                // 分表门禁阈值 0.3，命中率 = 20/(20+5) = 0.8 ≥ 0.3 → 不应阻断
                ["Persistence:AutoTuning:ShardingGovernanceHitRateThreshold"] = "0.3"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);
        // fp-sharding 单表高调用量 20，fp-cross 跨表低调用量 5，hit rate = 0.8 ≥ 0.3
        var result = new SlowQueryAnalysisResult(
            DateTime.Now,
            0,
            [
                new SlowQueryMetric("fp-sharding", "select * from parcels where code=@p0", 20, 200, 0m, 0m, 0, 300d, 600d, 700d, null),
                new SlowQueryMetric("fp-cross", "select * from parcels p join parcel_positions pp on p.id=pp.parcel_id where p.code=@p1", 5, 50, 0m, 0m, 0, 100d, 200d, 250d, null)
            ],
            [
                new SlowQueryTuningCandidate(
                    "fp-sharding",
                    null,
                    "parcels",
                    ["code"],
                    [
                        "CREATE INDEX `idx_auto_parcels_code_x` ON `parcels` (`code`)",
                        "ANALYZE TABLE `parcels`"
                    ])
            ],
            [],
            [],
            [],
            [],
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);
        var method = typeof(DatabaseAutoTuningHostedService).GetMethod("ApplyIndexSuggestionGuardsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var guardedResult = await (Task<SlowQueryAnalysisResult>)method.Invoke(service, [result, CancellationToken.None])!;

        // hit rate 满足阈值，CreateIndex 动作不应被门禁阻断
        Assert.Single(guardedResult.TuningCandidates);
        Assert.Contains(guardedResult.TuningCandidates[0].SuggestedActions, action => action.Contains("create index", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(observability.EventEntries, e => e.Name == "autotuning.sharding.governance_gate_blocked");
    }

    /// <summary>
    /// 验证场景：ParcelAggregateShardingCoverageGuard_ShouldCoverAllInfoValueObjects。
    /// </summary>
    [Fact]
    public void ParcelAggregateShardingCoverageGuard_ShouldCoverAllInfoValueObjects() {
        var method = typeof(PersistenceServiceCollectionExtensions).GetMethod(
            "AssertParcelAggregateShardingCoverage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var exception = Record.Exception(() => method.Invoke(null, null));
        if (exception is System.Reflection.TargetInvocationException invocationException) {
            exception = invocationException.InnerException;
        }

        Assert.Null(exception);
    }

    /// <summary>
    /// 验证场景：ResolveMySqlServerVersion_UsesConfiguredVersion_WhenConfigIsValid。
    /// </summary>
    [Fact]
    public void ResolveMySqlServerVersion_UsesConfiguredVersion_WhenConfigIsValid() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:MySql:ServerVersion"] = "8.0.36"
            })
            .Build();
        var warningMessages = new List<string>();

        var resolved = PersistenceServiceCollectionExtensions.ResolveMySqlServerVersion(
            configuration,
            "server=127.0.0.1;port=3306;database=zeye_sorting_hub;uid=validation_user;password=placeholder;SslMode=None;",
            warningMessages.Add);

        Assert.IsType<MySqlServerVersion>(resolved);
        Assert.Equal(new Version(8, 0, 36), resolved.Version);
    }

    /// <summary>
    /// 验证场景：ResolveMySqlServerVersion_FallsBackToDefault_WhenConfiguredVersionIsInvalid。
    /// </summary>
    [Fact]
    public void ResolveMySqlServerVersion_FallsBackToDefault_WhenConfiguredVersionIsInvalid() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:MySql:ServerVersion"] = "invalid-version"
            })
            .Build();
        var warningMessages = new List<string>();

        var resolved = PersistenceServiceCollectionExtensions.ResolveMySqlServerVersion(
            configuration,
            "invalid-connection-string",
            warningMessages.Add);

        Assert.Equal(new Version(8, 0, 0), resolved.Version);
        Assert.Contains(warningMessages, static message => message.Contains("非法或不受支持", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证场景：ResolveMySqlServerVersion_FallsBackToDefault_WhenAutoDetectThrows。
    /// </summary>
    [Fact]
    public void ResolveMySqlServerVersion_FallsBackToDefault_WhenAutoDetectThrows() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var warningMessages = new List<string>();

        var resolved = PersistenceServiceCollectionExtensions.ResolveMySqlServerVersion(
            configuration,
            "invalid-connection-string",
            warningMessages.Add);

        Assert.Equal(new Version(8, 0, 0), resolved.Version);
        Assert.Contains(warningMessages, static message => message.Contains("自动探测失败", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证场景：ResolveMySqlServerVersion_TreatsMajorLowerThanFiveAsInvalidAndFallsBack。
    /// </summary>
    [Fact]
    public void ResolveMySqlServerVersion_TreatsMajorLowerThanFiveAsInvalidAndFallsBack() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:MySql:ServerVersion"] = "4.1.0"
            })
            .Build();
        var warningMessages = new List<string>();

        var resolved = PersistenceServiceCollectionExtensions.ResolveMySqlServerVersion(
            configuration,
            "invalid-connection-string",
            warningMessages.Add);

        Assert.Equal(new Version(8, 0, 0), resolved.Version);
        Assert.Contains(warningMessages, static message => message.Contains("Major>=5", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证场景：ParcelRelatedValueObjects_ShouldExposeClrParcelId_ForShardingFieldRecognition。
    /// </summary>
    [Fact]
    public void ParcelRelatedValueObjects_ShouldExposeClrParcelId_ForShardingFieldRecognition() {
        var parcelIdPropertyByType = new Dictionary<Type, System.Reflection.PropertyInfo?> {
            [typeof(ParcelDeviceInfo)] = typeof(ParcelDeviceInfo).GetProperty("ParcelId"),
            [typeof(ParcelPositionInfo)] = typeof(ParcelPositionInfo).GetProperty("ParcelId"),
            [typeof(StickingParcelInfo)] = typeof(StickingParcelInfo).GetProperty("ParcelId"),
            [typeof(BarCodeInfo)] = typeof(BarCodeInfo).GetProperty("ParcelId"),
            [typeof(ImageInfo)] = typeof(ImageInfo).GetProperty("ParcelId"),
            [typeof(VideoInfo)] = typeof(VideoInfo).GetProperty("ParcelId")
        };

        foreach (var pair in parcelIdPropertyByType) {
            Assert.NotNull(pair.Value);
            Assert.Equal(typeof(long), pair.Value!.PropertyType);
            var setMethod = pair.Value.SetMethod;
            Assert.NotNull(setMethod);
            Assert.True(setMethod!.IsPrivate);
            Assert.Contains(
                setMethod.ReturnParameter.GetRequiredCustomModifiers(),
                modifier => modifier == typeof(System.Runtime.CompilerServices.IsExternalInit));
        }
    }

    /// <summary>
    /// 验证场景：ParcelRelatedValueObjects_ParcelId_ShouldMapToExistingParcelIdColumn。
    /// </summary>
    [Fact]
    public void ParcelRelatedValueObjects_ParcelId_ShouldMapToExistingParcelIdColumn() {
        using var dbContext = CreateTestingDbContext();
        var mappingPlans = new (Type ValueObjectType, string TableName)[] {
            (typeof(ParcelDeviceInfo), "Parcel_DeviceInfos"),
            (typeof(ParcelPositionInfo), "Parcel_PositionInfos"),
            (typeof(StickingParcelInfo), "Parcel_StickingParcelInfos"),
            (typeof(BarCodeInfo), "Parcel_BarCodeInfos"),
            (typeof(ImageInfo), "Parcel_ImageInfos"),
            (typeof(VideoInfo), "Parcel_VideoInfos")
        };

        foreach (var mappingPlan in mappingPlans) {
            var entityType = dbContext.Model.GetEntityTypes()
                .FirstOrDefault(type =>
                    type.IsOwned()
                    && type.ClrType == mappingPlan.ValueObjectType
                    && string.Equals(type.GetTableName(), mappingPlan.TableName, StringComparison.Ordinal));
            Assert.NotNull(entityType);
            var checkedEntityType = entityType!;
            var parcelIdProperty = checkedEntityType.FindProperty("ParcelId");
            Assert.NotNull(parcelIdProperty);
            Assert.False(parcelIdProperty!.IsShadowProperty());
            var store = StoreObjectIdentifier.Table(mappingPlan.TableName, checkedEntityType.GetSchema());
            Assert.Equal("ParcelId", parcelIdProperty.GetColumnName(store));
        }
    }

    /// <summary>
    /// 验证场景：ParcelRelatedValueObjects_EqualityAndHashCode_ShouldIgnoreParcelId。
    /// </summary>
    [Fact]
    public void ParcelRelatedValueObjects_EqualityAndHashCode_ShouldIgnoreParcelId() {
        var fixedReceiveTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local);
        var fixedCapturedTime = new DateTime(2024, 1, 1, 12, 5, 0, DateTimeKind.Local);

        var leftDevice = SetParcelIdForTesting(new ParcelDeviceInfo {
            WorkstationName = "WS-A",
            MachineCode = "M-1",
            CustomName = "Device-1"
        }, 101L);
        var rightDevice = SetParcelIdForTesting(new ParcelDeviceInfo {
            WorkstationName = "WS-A",
            MachineCode = "M-1",
            CustomName = "Device-1"
        }, 202L);
        Assert.Equal(leftDevice, rightDevice);
        Assert.Equal(leftDevice.GetHashCode(), rightDevice.GetHashCode());

        var leftPosition = SetParcelIdForTesting(new ParcelPositionInfo {
            X1 = 1m,
            X2 = 2m,
            Y1 = 3m,
            Y2 = 4m,
            BackgroundX1 = 5m,
            BackgroundX2 = 6m,
            BackgroundY1 = 7m,
            BackgroundY2 = 8m
        }, 101L);
        var rightPosition = SetParcelIdForTesting(new ParcelPositionInfo {
            X1 = 1m,
            X2 = 2m,
            Y1 = 3m,
            Y2 = 4m,
            BackgroundX1 = 5m,
            BackgroundX2 = 6m,
            BackgroundY1 = 7m,
            BackgroundY2 = 8m
        }, 202L);
        Assert.Equal(leftPosition, rightPosition);
        Assert.Equal(leftPosition.GetHashCode(), rightPosition.GetHashCode());

        var leftSticking = SetParcelIdForTesting(new StickingParcelInfo {
            IsSticking = true,
            ReceiveTime = fixedReceiveTime,
            RawData = "raw",
            ElapsedMilliseconds = 10
        }, 101L);
        var rightSticking = SetParcelIdForTesting(new StickingParcelInfo {
            IsSticking = true,
            ReceiveTime = leftSticking.ReceiveTime,
            RawData = "raw",
            ElapsedMilliseconds = 10
        }, 202L);
        Assert.Equal(leftSticking, rightSticking);
        Assert.Equal(leftSticking.GetHashCode(), rightSticking.GetHashCode());

        var leftBarCode = SetParcelIdForTesting(new BarCodeInfo {
            BarCode = "BC-1",
            BarCodeType = BarCodeType.ExpressSheet,
            CapturedTime = fixedCapturedTime
        }, 101L);
        var rightBarCode = SetParcelIdForTesting(new BarCodeInfo {
            BarCode = "BC-1",
            BarCodeType = BarCodeType.ExpressSheet,
            CapturedTime = leftBarCode.CapturedTime
        }, 202L);
        Assert.Equal(leftBarCode, rightBarCode);
        Assert.Equal(leftBarCode.GetHashCode(), rightBarCode.GetHashCode());

        var leftImage = SetParcelIdForTesting(new ImageInfo {
            CameraName = "Cam-1",
            CustomName = "TopCam",
            CameraSerialNumber = "SN-1",
            ImageType = ImageType.Scan,
            RelativePath = "images/a.jpg",
            CaptureType = ImageCaptureType.Camera
        }, 101L);
        var rightImage = SetParcelIdForTesting(new ImageInfo {
            CameraName = "Cam-1",
            CustomName = "TopCam",
            CameraSerialNumber = "SN-1",
            ImageType = ImageType.Scan,
            RelativePath = "images/a.jpg",
            CaptureType = ImageCaptureType.Camera
        }, 202L);
        Assert.Equal(leftImage, rightImage);
        Assert.Equal(leftImage.GetHashCode(), rightImage.GetHashCode());

        var leftVideo = SetParcelIdForTesting(new VideoInfo {
            Channel = 1,
            NvrSerialNumber = "NVR-1",
            NodeType = VideoNodeType.Scan
        }, 101L);
        var rightVideo = SetParcelIdForTesting(new VideoInfo {
            Channel = 1,
            NvrSerialNumber = "NVR-1",
            NodeType = VideoNodeType.Scan
        }, 202L);
        Assert.Equal(leftVideo, rightVideo);
        Assert.Equal(leftVideo.GetHashCode(), rightVideo.GetHashCode());
    }

    /// <summary>
    /// 验证场景：AutoRollbackDecisionEngine_TriggersSevereRollback。
    /// </summary>
    [Fact]
    public void AutoRollbackDecisionEngine_TriggersSevereRollback() {
        var result = AutoRollbackDecisionEngine.Evaluate(
            p99IncreasePercent: 32m,
            timeoutRateIncreasePercent: 0.5m,
            lockWaitStatus: "unavailable",
            severeRollbackP99IncreasePercent: 25m,
            severeRollbackTimeoutIncreasePercent: 2m,
            regressed: true,
            reason: "p99-regressed");

        Assert.True(result.IsRegressed);
        Assert.True(result.IsSevereRegression);
    }

    /// <summary>
    /// 验证场景：AutoRollbackDecisionEngine_TriggersNormalRegressionWithoutSevere。
    /// </summary>
    [Fact]
    public void AutoRollbackDecisionEngine_TriggersNormalRegressionWithoutSevere() {
        var result = AutoRollbackDecisionEngine.Evaluate(
            p99IncreasePercent: 12m,
            timeoutRateIncreasePercent: 0.7m,
            lockWaitStatus: "available",
            severeRollbackP99IncreasePercent: 25m,
            severeRollbackTimeoutIncreasePercent: 2m,
            regressed: true,
            reason: "p99-regressed");

        Assert.True(result.IsRegressed);
        Assert.False(result.IsSevereRegression);
    }

    /// <summary>
    /// 验证场景：VerificationResultBuilder_ExplicitlyMarksUnavailableMetrics。
    /// </summary>
    [Fact]
    public void VerificationResultBuilder_ExplicitlyMarksUnavailableMetrics() {
        var result = AutoTuningVerificationResultBuilder.Build(
            regressed: true,
            severeRegressed: false,
            reason: "threshold-regression-detected",
            p95IncreasePercent: 8m,
            p99IncreasePercent: 10m,
            errorRateIncreasePercent: 0.6m,
            timeoutRateIncreasePercent: 0.8m,
            deadlockIncreaseCount: 1,
            lockWaitBaseline: null,
            lockWaitCurrent: null,
            planRegression: new PlanRegressionSnapshot(false, false, "probe unavailable", "permission-denied"),
            lockWaitUnavailable: true,
            lockWaitUnavailableReason: AutoTuningUnavailableReason.BaselineAndCurrentUnavailable);

        Assert.Equal("regressed", result.Verdict);
        Assert.Contains(result.SnapshotDiff, diff => diff.Name == "lock-wait" && diff.Status == "unavailable");
        Assert.Contains(result.SnapshotDiff, diff => diff.Name == "plan-regression" && diff.Status == "unavailable");
    }

    /// <summary>
    /// 验证场景：PlanRegressionProbe_SupportsUnavailableAndAvailablePaths。
    /// </summary>
    [Fact]
    public void PlanRegressionProbe_SupportsUnavailableAndAvailablePaths() {
        var observability = new TestObservability();
        var probe = new LoggingOnlyExecutionPlanRegressionProbe(observability);

        var unavailable = probe.Evaluate("MySql", "plan-probe-permission-denied");
        Assert.False(unavailable.IsAvailable);
        Assert.Equal("permission-denied", unavailable.UnavailableReason);

        var available = probe.Evaluate("MySql", "plan-probe-available-regressed");
        Assert.True(available.IsAvailable);
        Assert.True(available.IsRegressed);
        Assert.Equal("none", available.UnavailableReason);
    }

    /// <summary>
    /// 验证场景：PlanRegressionProbe_ProviderAwareExtension_ShouldKeepLoggingOnlyCompatibility。
    /// </summary>
    [Fact]
    public void PlanRegressionProbe_ProviderAwareExtension_ShouldKeepLoggingOnlyCompatibility() {
        var observability = new TestObservability();
        var probe = new LoggingOnlyExecutionPlanRegressionProbe(observability);
        var providerAwareProbe = Assert.IsAssignableFrom<IProviderAwareExecutionPlanRegressionProbe>(probe);

        var snapshot = providerAwareProbe.Evaluate(new ExecutionPlanProbeRequest("SqlServer", "custom-fingerprint"));
        Assert.False(snapshot.IsAvailable);
        Assert.Equal("dialect-not-supported", snapshot.UnavailableReason);
        Assert.Contains("provider=SqlServer", snapshot.Summary, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：PerDayShardingBaseTableNames_ShouldResolveFromEfModelWithoutHardcodedTableList。
    /// </summary>
    [Fact]
    public void PerDayShardingBaseTableNames_ShouldResolveFromEfModelWithoutHardcodedTableList() {
        using var dbContext = CreateTestingDbContext();
        var tableNames = DatabaseInitializerHostedService.ResolvePerDayShardingBaseTableNames(dbContext);
        Assert.Equal(8, tableNames.Count);
        Assert.Contains("Parcels", tableNames);
        Assert.Contains("Parcel_WeightInfos", tableNames);
        Assert.Contains("Parcel_CommandInfos", tableNames);
    }

    /// <summary>
    /// 验证场景：ResolveWebRequestAuditLogRetentionKeepRecentShardCount_InvalidValue_ShouldPointToExactConfigKey。
    /// </summary>
    [Fact]
    public void ResolveWebRequestAuditLogRetentionKeepRecentShardCount_InvalidValue_ShouldPointToExactConfigKey() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Governance:WebRequestAuditLog:Retention:KeepRecentShardCount"] = "0"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseInitializerHostedService.ResolveWebRequestAuditLogRetentionKeepRecentShardCount(configuration));
        Assert.Contains("Persistence:Sharding:Governance:WebRequestAuditLog:Retention:KeepRecentShardCount", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：ClosedLoopFlow_TriggersMonitorExecuteVerifyRollback_WithAuditAndRollbackTrigger。
    /// </summary>
    [Fact]
    public async Task ClosedLoopFlow_TriggersMonitorExecuteVerifyRollback_WithAuditAndRollbackTrigger() {
        var memoryTarget = new NLog.Targets.MemoryTarget { Name = "test-memory", Layout = "${message}" };
        var loggingConfig = new NLog.Config.LoggingConfiguration();
        loggingConfig.AddRuleForAllLevels(memoryTarget);
        NLog.Config.LoggingConfiguration? previousConfig;
        lock (NLogConfigLock) {
            previousConfig = NLog.LogManager.Configuration;
            NLog.LogManager.Configuration = loggingConfig;
        }
        try {
        var observability = new TestObservability();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:AnalyzeIntervalSeconds"] = "1",
                ["Persistence:AutoTuning:Autonomous:EnableFullAutomation"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:EnableAutoValidation"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:EnableAutoRollback"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:DelayCycles"] = "1",
                ["Persistence:AutoTuning:Autonomous:Validation:P95IncreasePercent"] = "1",
                ["Persistence:AutoTuning:Autonomous:Validation:P99IncreasePercent"] = "1",
                ["Persistence:AutoTuning:Autonomous:Validation:ErrorRateIncreasePercent"] = "0.1",
                ["Persistence:AutoTuning:Autonomous:Validation:TimeoutRateIncreasePercent"] = "0.1",
                ["Persistence:AutoTuning:Autonomous:Validation:DeadlockIncreaseCount"] = "1",
                ["Persistence:AutoTuning:Autonomous:Validation:PauseActionCyclesOnRegression"] = "2",
                ["Persistence:AutoTuning:Autonomous:Validation:SevereRollback:P99IncreasePercent"] = "25",
                ["Persistence:AutoTuning:Autonomous:Validation:SevereRollback:TimeoutRateIncreasePercent"] = "2",
                ["Persistence:AutoTuning:Autonomous:Execution:Isolator:EnableGuard"] = "true",
                ["Persistence:AutoTuning:Autonomous:Execution:Isolator:AllowDangerousActionExecution"] = "true",
                ["Persistence:AutoTuning:Autonomous:Execution:Isolator:DryRun"] = "true",
                ["Persistence:AutoTuning:Autonomous:Execution:WhitelistedTables:0"] = "parcels"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration,
            Microsoft.Extensions.Options.Options.Create(new Zeye.Sorting.Hub.Host.Options.ResourceThresholdsOptions()));

        var moveToStage = typeof(DatabaseAutoTuningHostedService).GetMethod("MoveToStage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        moveToStage.Invoke(service, [AutoTuningClosedLoopStage.Execute, "test-execute", "action-001", "fingerprint-001"]);

        var executeThroughIsolator = typeof(DatabaseAutoTuningHostedService).GetMethod("ExecuteThroughIsolatorAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var candidate = new SlowQueryTuningCandidate("fingerprint-001", null, "parcels", Array.Empty<string>(), Array.Empty<string>());
        var executeTask = (Task<bool>)executeThroughIsolator.Invoke(service, ["action-001", candidate, "create index `ix_p` on `parcels`(`code`)", "drop index `ix_p` on `parcels`", "test-audit", false, CancellationToken.None])!;
        await executeTask;

        moveToStage.Invoke(service, [AutoTuningClosedLoopStage.Verify, "test-verify", "action-001", "fingerprint-001"]);
        SetField(service, "_analysisCycleCounter", 2);
        SeedPendingRollback(service);

        var validate = typeof(DatabaseAutoTuningHostedService).GetMethod("ValidateAutonomousActionsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var result = new SlowQueryAnalysisResult(
            DateTime.Now,
            0,
            [
                new SlowQueryMetric(
                    "fingerprint-001",
                    "select * from parcels where code = @p0",
                    2,
                    10,
                    2m,
                    3m,
                    2,
                    1200d,
                    1800d,
                    1800d,
                    null)
            ],
            Array.Empty<SlowQueryTuningCandidate>(),
            Array.Empty<string>(),
            Array.Empty<SlowQuerySuggestionInsight>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);
        var metricsByFingerprint = result.Metrics.ToDictionary(static x => x.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
        var validateTask = (Task)validate.Invoke(service, [result, metricsByFingerprint, CancellationToken.None])!;
        await validateTask;

        Assert.Contains(memoryTarget.Logs, message => message.Contains("闭环自治阶段迁移") && message.Contains("CurrentStage=Execute"));
        Assert.Contains(memoryTarget.Logs, message => message.Contains("闭环自治阶段迁移") && message.Contains("CurrentStage=Verify"));
        Assert.Contains(memoryTarget.Logs, message => message.Contains("自动调优变更审计"));
        Assert.Contains(memoryTarget.Logs, message => message.Contains("闭环自治自动验证触发回滚"));
        Assert.Contains(memoryTarget.Logs, message => message.Contains("rollback-triggered"));
        Assert.Contains(observability.EventEntries, entry =>
            entry.Name == "autotuning.closed_loop.stage_transition"
            && entry.Tags.TryGetValue("evidence_id", out var evidenceId)
            && evidenceId.Contains("action-001", StringComparison.Ordinal)
            && entry.Tags.ContainsKey("correlation_id"));
        Assert.Contains(observability.EventEntries, entry =>
            entry.Name == "autotuning.validation.rollback_triggered"
            && entry.Tags.ContainsKey("evidence_id")
            && entry.Tags.ContainsKey("correlation_id"));
        } finally {
            lock (NLogConfigLock) {
                NLog.LogManager.Configuration = previousConfig;
            }
        }
    }
    /// <summary>
    /// 验证场景：Validation_WhenPlanProbeDisabledOrSampleRateZero_MarksUnavailableWithoutInvokingProbe。
    /// </summary>
    [Fact]
    public async Task Validation_WhenPlanProbeDisabledOrSampleRateZero_MarksUnavailableWithoutInvokingProbe() {
        var memoryTarget = new NLog.Targets.MemoryTarget { Name = "test-memory", Layout = "${message}" };
        var loggingConfig = new NLog.Config.LoggingConfiguration();
        loggingConfig.AddRuleForAllLevels(memoryTarget);
        NLog.Config.LoggingConfiguration? previousConfig;
        lock (NLogConfigLock) {
            previousConfig = NLog.LogManager.Configuration;
            NLog.LogManager.Configuration = loggingConfig;
        }
        try {
        var observability = new TestObservability();
        var probe = new CountingPlanProbe();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:Autonomous:EnableFullAutomation"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:EnableAutoValidation"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:EnableAutoRollback"] = "false",
                ["Persistence:AutoTuning:Autonomous:Validation:DelayCycles"] = "1",
                ["Persistence:AutoTuning:Autonomous:Validation:PlanProbe:Enable"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:PlanProbe:SampleRate"] = "0"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            observability,
            probe,
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration,
            Microsoft.Extensions.Options.Options.Create(new Zeye.Sorting.Hub.Host.Options.ResourceThresholdsOptions()));

        SetField(service, "_analysisCycleCounter", 2);
        SeedPendingRollback(service);
        var validate = typeof(DatabaseAutoTuningHostedService).GetMethod("ValidateAutonomousActionsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var result = new SlowQueryAnalysisResult(
            DateTime.Now,
            0,
            [
                new SlowQueryMetric(
                    "fingerprint-001",
                    "select * from parcels where code = @p0",
                    2,
                    10,
                    2m,
                    3m,
                    2,
                    1200d,
                    1800d,
                    1800d,
                    null)
            ],
            Array.Empty<SlowQueryTuningCandidate>(),
            Array.Empty<string>(),
            Array.Empty<SlowQuerySuggestionInsight>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);
        var metricsByFingerprint = result.Metrics.ToDictionary(static x => x.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
        var validateTask = (Task)validate.Invoke(service, [result, metricsByFingerprint, CancellationToken.None])!;
        await validateTask;

        Assert.Equal(0, probe.CallCount);
        Assert.Contains(memoryTarget.Logs, message => message.Contains("plan-probe-sampling-skipped"));
        } finally {
            lock (NLogConfigLock) {
                NLog.LogManager.Configuration = previousConfig;
            }
        }
    }
    /// </summary>
    [Fact]
    public async Task WhenPlanProbeSampleRateInvalid_FallsBackToDefaultAndInvokesProbe() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var probe = new CountingPlanProbe();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:Autonomous:EnableFullAutomation"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:EnableAutoValidation"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:EnableAutoRollback"] = "false",
                ["Persistence:AutoTuning:Autonomous:Validation:DelayCycles"] = "1",
                ["Persistence:AutoTuning:Autonomous:Validation:PlanProbe:Enable"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:PlanProbe:SampleRate"] = "not-a-number"
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            probe,
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);

        SetField(service, "_analysisCycleCounter", 2);
        SeedPendingRollback(service);
        var validate = typeof(DatabaseAutoTuningHostedService).GetMethod("ValidateAutonomousActionsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var result = new SlowQueryAnalysisResult(
            DateTime.Now,
            0,
            [
                new SlowQueryMetric(
                    "fingerprint-001",
                    "select * from parcels where code = @p0",
                    2,
                    10,
                    2m,
                    3m,
                    2,
                    1200d,
                    1800d,
                    1800d,
                    null)
            ],
            Array.Empty<SlowQueryTuningCandidate>(),
            Array.Empty<string>(),
            Array.Empty<SlowQuerySuggestionInsight>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);
        var metricsByFingerprint = result.Metrics.ToDictionary(static x => x.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
        var validateTask = (Task)validate.Invoke(service, [result, metricsByFingerprint, CancellationToken.None])!;
        await validateTask;

        Assert.Equal(1, probe.CallCount);
    }

    /// <summary>
    /// 验证场景：WhenPlanProbeSampleRateOutOfRange_ClampsToLegacyBehavior。
    /// </summary>
    [Theory]
    [InlineData("-0.1", 0)]
    [InlineData("1.8", 1)]
    public async Task WhenPlanProbeSampleRateOutOfRange_ClampsToLegacyBehavior(string sampleRate, int expectedCallCount) {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var probe = new CountingPlanProbe();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:Autonomous:EnableFullAutomation"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:EnableAutoValidation"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:EnableAutoRollback"] = "false",
                ["Persistence:AutoTuning:Autonomous:Validation:DelayCycles"] = "1",
                ["Persistence:AutoTuning:Autonomous:Validation:PlanProbe:Enable"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:PlanProbe:SampleRate"] = sampleRate
            })
            .Build();
        var pipeline = new SlowQueryAutoTuningPipeline(configuration, observability);
        var service = new DatabaseAutoTuningHostedService(
            logger,
            observability,
            probe,
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);

        SetField(service, "_analysisCycleCounter", 2);
        SeedPendingRollback(service);
        var validate = typeof(DatabaseAutoTuningHostedService).GetMethod("ValidateAutonomousActionsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var result = new SlowQueryAnalysisResult(
            DateTime.Now,
            0,
            [
                new SlowQueryMetric(
                    "fingerprint-001",
                    "select * from parcels where code = @p0",
                    2,
                    10,
                    2m,
                    3m,
                    2,
                    1200d,
                    1800d,
                    1800d,
                    null)
            ],
            Array.Empty<SlowQueryTuningCandidate>(),
            Array.Empty<string>(),
            Array.Empty<SlowQuerySuggestionInsight>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<SlowQueryAlertNotification>(),
            false,
            false,
            false);
        var metricsByFingerprint = result.Metrics.ToDictionary(static x => x.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
        var validateTask = (Task)validate.Invoke(service, [result, metricsByFingerprint, CancellationToken.None])!;
        await validateTask;

        Assert.Equal(expectedCallCount, probe.CallCount);
    }

    /// <summary>
    /// 验证场景：WhenShouldSamplePlanProbeInvoked_UsesStableHashBucket。
    /// </summary>
    [Fact]
    public void WhenShouldSamplePlanProbeInvoked_UsesStableHashBucket() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:Autonomous:Validation:PlanProbe:Enable"] = "true",
                ["Persistence:AutoTuning:Autonomous:Validation:PlanProbe:SampleRate"] = "0.1234"
            })
            .Build();
        var service = new DatabaseAutoTuningHostedService(
            new TestLogger<DatabaseAutoTuningHostedService>(),
            new TestObservability(),
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            new SlowQueryAutoTuningPipeline(configuration, new TestObservability()),
            configuration);

        SeedPendingRollback(service);
        var rollback = GetSeededRollbackAction(service);
        var shouldSample = typeof(DatabaseAutoTuningHostedService).GetMethod("ShouldSamplePlanProbe", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var sampled = (bool)shouldSample.Invoke(service, [rollback])!;

        const string seed = "action-001:fingerprint-001";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var bucket = BinaryPrimitives.ReadUInt32LittleEndian(hashBytes) % 10000u;
        var threshold = (uint)Math.Round(0.1234d * 10000d, MidpointRounding.AwayFromZero);
        Assert.Equal(bucket < threshold, sampled);
    }

    /// <summary>
    /// 验证场景：ClosedLoopTracker_RecordsMonitorExecuteVerifyRollbackChain。
    /// </summary>
    [Fact]
    public void ClosedLoopTracker_RecordsMonitorExecuteVerifyRollbackChain() {
        var tracker = new AutoTuningClosedLoopTracker();
        tracker.MoveTo(AutoTuningClosedLoopStage.Diagnose);
        tracker.MoveTo(AutoTuningClosedLoopStage.Execute);
        tracker.MoveTo(AutoTuningClosedLoopStage.Verify);
        tracker.MoveTo(AutoTuningClosedLoopStage.Rollback);

        Assert.Equal(
            [
                AutoTuningClosedLoopStage.Monitor,
                AutoTuningClosedLoopStage.Diagnose,
                AutoTuningClosedLoopStage.Execute,
                AutoTuningClosedLoopStage.Verify,
                AutoTuningClosedLoopStage.Rollback
            ],
            tracker.Stages);
    }

    /// <summary>
    /// 验证场景：ClosedLoopTracker_CapsAt1000AndDropsOldestWhenOverflow。
    /// </summary>
    [Fact]
    public void ClosedLoopTracker_CapsAt1000AndDropsOldestWhenOverflow() {
        var tracker = new AutoTuningClosedLoopTracker();
        // Fill exactly to the cap (1000 entries: 1 initial Monitor + 999 alternating Diagnose/Monitor).
        for (var i = 0; i < 999; i++) {
            tracker.MoveTo(i % 2 == 0 ? AutoTuningClosedLoopStage.Diagnose : AutoTuningClosedLoopStage.Monitor);
        }
        Assert.Equal(1000, tracker.Stages.Count);

        // The initial Monitor entry added by the constructor is still the oldest.
        Assert.Equal(AutoTuningClosedLoopStage.Monitor, tracker.Stages[0]);

        // Push one more entry beyond the cap.
        tracker.MoveTo(AutoTuningClosedLoopStage.Execute);

        // Count must stay at the cap.
        Assert.Equal(1000, tracker.Stages.Count);
        // The newest entry is the one just added.
        Assert.Equal(AutoTuningClosedLoopStage.Execute, tracker.Stages[^1]);
        // The initial Monitor entry has been evicted; the oldest is now index 0 of the previous cycle.
        Assert.NotEqual(AutoTuningClosedLoopStage.Monitor, tracker.Stages[0]);
    }

    /// <summary>
    /// 验证场景：SetField。
    /// </summary>
    private static void SetField(object target, string fieldName, object value) {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(target, value);
    }

    /// <summary>
    /// 验证场景：SeedPendingRollback。
    /// </summary>
    private static void SeedPendingRollback(DatabaseAutoTuningHostedService service) {
        var mapField = typeof(DatabaseAutoTuningHostedService).GetField("_pendingRollbackByFingerprint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var map = mapField.GetValue(service)!;
        var recordType = map.GetType().GetGenericArguments()[1];
        var record = Activator.CreateInstance(
            recordType,
            "action-001",
            "fingerprint-001",
            "drop index `ix_p` on `parcels`",
            "parcels",
            DateTime.Now,
            1,
            200d,
            300d,
            0m,
            0m,
            0,
            null)!;
        var addMethod = map.GetType().GetMethod("Add")!;
        addMethod.Invoke(map, ["fingerprint-001", record]);
    }

    /// <summary>
    /// 验证场景：GetSeededRollbackAction。
    /// </summary>
    private static object GetSeededRollbackAction(DatabaseAutoTuningHostedService service) {
        var mapField = typeof(DatabaseAutoTuningHostedService).GetField("_pendingRollbackByFingerprint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var map = mapField.GetValue(service)!;
        var valuesProperty = map.GetType().GetProperty("Values")!;
        var values = (System.Collections.IEnumerable)valuesProperty.GetValue(map)!;
        var enumerator = values.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        return enumerator.Current!;
    }

    /// <summary>
    /// 构建最小可运行的分表治理配置。
    /// </summary>
    /// <param name="createShardingTableOnStarting">是否启用启动自动建表。</param>
    /// <param name="timeGranularity">时间粒度配置（PerDay/PerMonth）。</param>
    /// <param name="prebuildWindowHours">预建窗口小时数。</param>
    /// <returns>配置对象。</returns>
    private static IConfiguration BuildPerDayGovernanceConfiguration(
        bool createShardingTableOnStarting,
        string timeGranularity,
        int prebuildWindowHours,
        bool criticalIndexAuditEnabled = true,
        bool blockOnCriticalIndexMissing = true,
        bool enableWebRequestAuditLogPerDayGuard = true) {
        var configData = new Dictionary<string, string?> {
            ["Persistence:Migration:FailureStrategy:NonProduction"] = "Degraded",
            ["Persistence:Sharding:CreateShardingTableOnStarting"] = createShardingTableOnStarting.ToString(),
            ["Persistence:Sharding:Governance:Runbook"] = "docs/internal/sharding-governance-runbook",
            ["Persistence:Sharding:Governance:PrebuildWindowHours"] = prebuildWindowHours.ToString(),
            ["Persistence:Sharding:Governance:WebRequestAuditLog:EnablePerDayGuard"] = enableWebRequestAuditLogPerDayGuard.ToString(),
            ["Persistence:Sharding:Governance:CriticalIndexAudit:Enabled"] = criticalIndexAuditEnabled.ToString(),
            ["Persistence:Sharding:Governance:CriticalIndexAudit:BlockOnMissing"] = blockOnCriticalIndexMissing.ToString(),
            ["Persistence:Sharding:HashSharding:ExpansionPlan:CurrentMod"] = "16",
            ["Persistence:Sharding:HashSharding:ExpansionPlan:TargetMod"] = "32",
            ["Persistence:Sharding:Strategy:Mode"] = "Time",
            ["Persistence:Sharding:Strategy:Time:Granularity"] = timeGranularity
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    /// <summary>
    /// 创建数据库初始化托管服务（含最小 DB 上下文桩）。
    /// </summary>
    /// <param name="configuration">配置源。</param>
    /// <param name="physicalTableProbe">物理表探测器。</param>
    /// <param name="logger">日志桩。</param>
    /// <param name="dialect">数据库方言桩。</param>
    /// <returns>初始化托管服务实例。</returns>
    private static DatabaseInitializerHostedService CreateDatabaseInitializerHostedService(
        IConfiguration configuration,
        IShardingPhysicalTableProbe physicalTableProbe,
        TestLogger<DatabaseInitializerHostedService>? logger = null,
        IDatabaseDialect? dialect = null) {
        var services = new ServiceCollection();
        services.AddSingleton(CreateTestingDbContext());
        var provider = services.BuildServiceProvider();
        return new DatabaseInitializerHostedService(
            provider,
            logger ?? new TestLogger<DatabaseInitializerHostedService>(),
            dialect ?? new TestDialect(),
            physicalTableProbe,
            new TestHostEnvironment("Development"),
            configuration,
            new MigrationGovernanceStateStore());
    }

    /// <summary>
    /// 创建用于守卫与模型解析测试的 DbContext（使用 InMemory provider，避免依赖真实数据库）。
    /// </summary>
    /// <returns>测试 DbContext。</returns>
    private static SortingHubDbContext CreateTestingDbContext() {
        var options = new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase($"sharding-guard-test-{Guid.NewGuid():N}")
            .Options;
        return new SortingHubDbContext(options);
    }

    /// <summary>
    /// 仅用于测试：通过反射写入值对象 ParcelId，验证该字段不影响领域相等语义。
    /// </summary>
    /// <typeparam name="TValueObject">值对象类型。</typeparam>
    /// <param name="valueObject">值对象实例。</param>
    /// <param name="parcelId">测试用 ParcelId。</param>
    /// <returns>写入后的值对象实例。</returns>
    private static TValueObject SetParcelIdForTesting<TValueObject>(TValueObject valueObject, long parcelId)
        where TValueObject : class {
        var property = valueObject.GetType().GetProperty("ParcelId", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(property);
        var setMethod = property!.GetSetMethod(nonPublic: true);
        Assert.NotNull(setMethod);
        setMethod!.Invoke(valueObject, [parcelId]);
        return valueObject;
    }

    /// <summary>
    /// 通过反射调用启动期分表治理守卫（仅验证守卫，不触发后续迁移逻辑）。
    /// </summary>
    /// <param name="service">初始化托管服务实例。</param>
    /// <returns>守卫执行任务。</returns>
    private static async Task InvokeShardingGovernanceGuardAsync(DatabaseInitializerHostedService service) {
        var validateMethod = typeof(DatabaseInitializerHostedService)
            .GetMethod("ValidateShardingGovernanceGuardAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(validateMethod);

        Task? guardTask = null;
        var invocationException = Record.Exception(() => {
            guardTask = (Task?)validateMethod!.Invoke(service, [CancellationToken.None]);
        });
        if (invocationException is TargetInvocationException targetInvocationException
            && targetInvocationException.InnerException is not null) {
            ExceptionDispatchInfo.Capture(targetInvocationException.InnerException).Throw();
        }
        if (invocationException is not null) {
            ExceptionDispatchInfo.Capture(invocationException).Throw();
        }

        Assert.NotNull(guardTask);
        await guardTask!;
    }
}
