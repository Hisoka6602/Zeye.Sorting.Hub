using System.Buffers.Binary;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EFCore.Sharding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Host.Enums;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.Enums;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;

namespace Zeye.Sorting.Hub.Host.Tests;

public sealed class AutoTuningProductionControlTests {
    private const double DoublePrecisionTolerance = 0.0001d;
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
                ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Port=3306;Database=test;Uid=root;Pwd=Admin@1234;",
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
    /// 验证场景：ResolvePrebuiltPerDayShardDates_RejectsInvalidLocalDateFormat。
    /// </summary>
    [Fact]
    public void ResolvePrebuiltPerDayShardDates_RejectsInvalidLocalDateFormat() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Governance:PrebuiltPerDayDates:0"] = "2026-03-18",
                ["Persistence:Sharding:Governance:PrebuiltPerDayDates:1"] = "2026-03-18T00:00:00Z"
            })
            .Build();

        var resolution = DatabaseInitializerHostedService.ResolvePrebuiltPerDayShardDates(configuration);

        Assert.Single(resolution.PrebuiltDates);
        Assert.Single(resolution.ValidationErrors);
        Assert.Contains("yyyy-MM-dd", resolution.ValidationErrors[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：ShardingGovernanceGuard_ShouldFail_WhenPerDayMissingPrebuiltDates。
    /// </summary>
    [Fact]
    public async Task ShardingGovernanceGuard_ShouldFail_WhenPerDayMissingPrebuiltDates() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Migration:FailureStrategy:NonProduction"] = "Degraded",
                ["Persistence:Sharding:CreateShardingTableOnStarting"] = "false",
                ["Persistence:Sharding:Governance:EnableManualPrebuildGuard"] = "true",
                ["Persistence:Sharding:Governance:Runbook"] = "docs/internal/sharding-governance-runbook",
                ["Persistence:Sharding:Governance:PrebuildWindowHours"] = "48",
                ["Persistence:Sharding:Governance:PrebuiltPerDayDates:0"] = "2026-03-18",
                ["Persistence:Sharding:HashSharding:ExpansionPlan:CurrentMod"] = "16",
                ["Persistence:Sharding:HashSharding:ExpansionPlan:TargetMod"] = "32",
                ["Persistence:Sharding:Strategy:Mode"] = "Time",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerDay"
            })
            .Build();

        var service = new DatabaseInitializerHostedService(
            new ServiceCollection().BuildServiceProvider(),
            new TestLogger<DatabaseInitializerHostedService>(),
            new TestDialect(),
            new AlwaysExistsShardingPhysicalTableProbe(),
            new TestHostEnvironment("Development"),
            configuration);

        // HostedService 内部使用私有嵌套异常类型（ShardingGovernanceGuardException）承载守卫阻断语义，
        // 测试侧不可直接引用该私有类型，因此使用 ThrowsAnyAsync 断言 InvalidOperationException 继承体系。
        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));
        Assert.Contains("PrebuiltPerDayDates", exception.Message, StringComparison.Ordinal);
        Assert.Contains("PerDay", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：ShardingGovernanceGuard_ConfigValidationFailure_ShouldNotEnterRetryPolicy。
    /// </summary>
    [Fact]
    public async Task ShardingGovernanceGuard_ConfigValidationFailure_ShouldNotEnterRetryPolicy() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Migration:FailureStrategy:NonProduction"] = "FailFast",
                ["Persistence:Sharding:CreateShardingTableOnStarting"] = "false",
                ["Persistence:Sharding:Governance:EnableManualPrebuildGuard"] = "true",
                ["Persistence:Sharding:Governance:Runbook"] = "docs/internal/sharding-governance-runbook",
                ["Persistence:Sharding:Governance:PrebuildWindowHours"] = "24",
                ["Persistence:Sharding:Governance:PrebuiltPerDayDates:0"] = "2026-03-18T00:00:00Z",
                ["Persistence:Sharding:HashSharding:ExpansionPlan:CurrentMod"] = "16",
                ["Persistence:Sharding:HashSharding:ExpansionPlan:TargetMod"] = "32",
                ["Persistence:Sharding:Strategy:Mode"] = "Time",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerDay"
            })
            .Build();
        var logger = new TestLogger<DatabaseInitializerHostedService>();
        var service = new DatabaseInitializerHostedService(
            new ServiceCollection().BuildServiceProvider(),
            logger,
            new TestDialect(),
            new AlwaysExistsShardingPhysicalTableProbe(),
            new TestHostEnvironment("Development"),
            configuration);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));
        Assert.DoesNotContain(logger.Messages, message => message.Contains("数据库初始化重试中", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证场景：ShardingGovernanceGuard_PerDayWithConfiguredDatesAndExistingPhysicalTables_DoesNotBlockByPhysicalProbe。
    /// </summary>
    [Fact]
    public async Task ShardingGovernanceGuard_PerDayWithConfiguredDatesAndExistingPhysicalTables_DoesNotBlockByPhysicalProbe() {
        var requiredDates = DatabaseInitializerHostedService
            .BuildRequiredPerDayShardDates(DateTime.Now, 48)
            .Select(static date => date.ToString("yyyy-MM-dd"))
            .ToArray();
        var configuration = BuildPerDayGovernanceConfiguration(
            createShardingTableOnStarting: false,
            enableManualPrebuildGuard: true,
            timeGranularity: "PerDay",
            prebuildWindowHours: 48,
            prebuiltDates: requiredDates);
        var probe = new AlwaysExistsShardingPhysicalTableProbe();
        var service = CreateDatabaseInitializerHostedService(configuration, probe);

        var exception = await Record.ExceptionAsync(() => InvokeShardingGovernanceGuardAsync(service));
        Assert.Null(exception);
        Assert.True(probe.CallCount > 0);
    }

    /// <summary>
    /// 验证场景：ShardingGovernanceGuard_PerDayWithConfiguredDatesButMissingPhysicalTable_ShouldFail。
    /// </summary>
    [Fact]
    public async Task ShardingGovernanceGuard_PerDayWithConfiguredDatesButMissingPhysicalTable_ShouldFail() {
        var date = DateTime.Now;
        var requiredDates = DatabaseInitializerHostedService
            .BuildRequiredPerDayShardDates(date, 48)
            .Select(static requiredDate => requiredDate.ToString("yyyy-MM-dd"))
            .ToArray();
        var configuration = BuildPerDayGovernanceConfiguration(
            createShardingTableOnStarting: false,
            enableManualPrebuildGuard: true,
            timeGranularity: "PerDay",
            prebuildWindowHours: 48,
            prebuiltDates: requiredDates);
        var missingTable = DatabaseInitializerHostedService.BuildPerDayPhysicalTableName("Parcels", date);
        var probe = new SelectiveMissingShardingPhysicalTableProbe([missingTable]);
        var service = CreateDatabaseInitializerHostedService(configuration, probe);

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => InvokeShardingGovernanceGuardAsync(service));
        Assert.Contains("缺失物理表", exception.Message, StringComparison.Ordinal);
        Assert.Contains(missingTable, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：ShardingGovernanceGuard_PerDayPhysicalMissing_ShouldNotRetry。
    /// </summary>
    [Fact]
    public async Task ShardingGovernanceGuard_PerDayPhysicalMissing_ShouldNotRetry() {
        var date = DateTime.Now;
        var requiredDates = DatabaseInitializerHostedService
            .BuildRequiredPerDayShardDates(date, 24)
            .Select(static requiredDate => requiredDate.ToString("yyyy-MM-dd"))
            .ToArray();
        var configuration = BuildPerDayGovernanceConfiguration(
            createShardingTableOnStarting: false,
            enableManualPrebuildGuard: true,
            timeGranularity: "PerDay",
            prebuildWindowHours: 24,
            prebuiltDates: requiredDates);
        var missingTable = DatabaseInitializerHostedService.BuildPerDayPhysicalTableName("Parcels", date);
        var probe = new BatchSelectiveMissingShardingPhysicalTableProbe([missingTable]);
        var logger = new TestLogger<DatabaseInitializerHostedService>();
        var service = CreateDatabaseInitializerHostedService(configuration, probe, logger);

        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None));
        Assert.Equal(1, probe.FindMissingCallCount);
        Assert.DoesNotContain(logger.Messages, message => message.Contains("数据库初始化重试中", StringComparison.Ordinal));
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
                ["Persistence:Sharding:Governance:EnableManualPrebuildGuard"] = "true",
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
            configuration);

        var exception = await Record.ExceptionAsync(() => service.StartAsync(CancellationToken.None));
        Assert.Null(exception);
        Assert.Equal(6, logger.Messages.Count(message => message.Contains("数据库初始化重试中", StringComparison.Ordinal)));
    }

    /// <summary>
    /// 验证场景：ShardingGovernanceGuard_BatchProbe_ShouldPassSchemaFromHostedService。
    /// </summary>
    [Fact]
    public async Task ShardingGovernanceGuard_BatchProbe_ShouldPassSchemaFromHostedService() {
        var requiredDates = DatabaseInitializerHostedService
            .BuildRequiredPerDayShardDates(DateTime.Now, 24)
            .Select(static date => date.ToString("yyyy-MM-dd"))
            .ToArray();
        var configuration = BuildPerDayGovernanceConfiguration(
            createShardingTableOnStarting: false,
            enableManualPrebuildGuard: true,
            timeGranularity: "PerDay",
            prebuildWindowHours: 24,
            prebuiltDates: requiredDates);
        var probe = new BatchSelectiveMissingShardingPhysicalTableProbe([]);
        var service = CreateDatabaseInitializerHostedService(configuration, probe, dialect: new TestSqlServerDialect());

        var exception = await Record.ExceptionAsync(() => InvokeShardingGovernanceGuardAsync(service));
        Assert.Null(exception);
        Assert.Equal(1, probe.FindMissingCallCount);
        Assert.Equal("dbo", probe.LastSchemaName);
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
    /// 验证场景：ShardingGovernanceGuard_PerDayWithMissingConfiguredDates_ShouldFailBeforePhysicalProbe。
    /// </summary>
    [Fact]
    public async Task ShardingGovernanceGuard_PerDayWithMissingConfiguredDates_ShouldFailBeforePhysicalProbe() {
        var configuration = BuildPerDayGovernanceConfiguration(
            createShardingTableOnStarting: false,
            enableManualPrebuildGuard: true,
            timeGranularity: "PerDay",
            prebuildWindowHours: 48,
            prebuiltDates: [DateTime.Now.ToString("yyyy-MM-dd")]);
        var probe = new AlwaysExistsShardingPhysicalTableProbe();
        var service = CreateDatabaseInitializerHostedService(configuration, probe);

        var exception = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => InvokeShardingGovernanceGuardAsync(service));
        Assert.Contains("缺失日期", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, probe.CallCount);
    }

    /// <summary>
    /// 验证场景：ShardingGovernanceGuard_NonPerDay_ShouldSkipPhysicalProbe。
    /// </summary>
    [Fact]
    public async Task ShardingGovernanceGuard_NonPerDay_ShouldSkipPhysicalProbe() {
        var configuration = BuildPerDayGovernanceConfiguration(
            createShardingTableOnStarting: false,
            enableManualPrebuildGuard: true,
            timeGranularity: "PerMonth",
            prebuildWindowHours: 0,
            prebuiltDates: []);
        var probe = new AlwaysExistsShardingPhysicalTableProbe();
        var service = CreateDatabaseInitializerHostedService(configuration, probe);

        var exception = await Record.ExceptionAsync(() => InvokeShardingGovernanceGuardAsync(service));
        Assert.Null(exception);
        Assert.Equal(0, probe.CallCount);
    }

    /// <summary>
    /// 验证场景：ShardingGovernanceGuard_WhenCreateTableEnabledOrManualGuardDisabled_ShouldSkipPhysicalProbe。
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ShardingGovernanceGuard_WhenCreateTableEnabledOrManualGuardDisabled_ShouldSkipPhysicalProbe(
        bool createShardingTableOnStarting,
        bool enableManualPrebuildGuard) {
        var configuration = BuildPerDayGovernanceConfiguration(
            createShardingTableOnStarting: createShardingTableOnStarting,
            enableManualPrebuildGuard: enableManualPrebuildGuard,
            timeGranularity: "PerDay",
            prebuildWindowHours: 0,
            prebuiltDates: [DateTime.Now.ToString("yyyy-MM-dd")]);
        var probe = new AlwaysExistsShardingPhysicalTableProbe();
        var service = CreateDatabaseInitializerHostedService(configuration, probe);

        var exception = await Record.ExceptionAsync(() => InvokeShardingGovernanceGuardAsync(service));
        Assert.Null(exception);
        Assert.Equal(0, probe.CallCount);
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
    /// 验证场景：AutoTuningConfigurationHelper_BuildKeys_ShouldUseExpectedPrefix。
    /// </summary>
    [Fact]
    public void AutoTuningConfigurationHelper_BuildKeys_ShouldUseExpectedPrefix() {
        var autoTuningKey = AutoTuningConfigurationHelper.BuildAutoTuningKey("TriggerCount");
        var autonomousKey = AutoTuningConfigurationHelper.BuildAutonomousKey("Validation:DelayCycles");

        Assert.Equal("Persistence:AutoTuning:TriggerCount", autoTuningKey);
        Assert.Equal("Persistence:AutoTuning:Autonomous:Validation:DelayCycles", autonomousKey);
    }

    /// <summary>
    /// 验证场景：AutoTuningConfigurationHelper_BuildAutoTuningKey_ShouldProduceCorrectPath（参数化）。
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
    public void AutoTuningConfigurationHelper_BuildAutoTuningKey_ShouldProduceCorrectPath(string suffix, string expectedKey) {
        var actual = AutoTuningConfigurationHelper.BuildAutoTuningKey(suffix);
        Assert.Equal(expectedKey, actual);
    }

    /// <summary>
    /// 验证场景：AutoTuningConfigurationHelper_BuildAutonomousKey_ShouldProduceCorrectPath（参数化）。
    /// 覆盖多个 Autonomous 配置子路径，确保前缀拼装规则对嵌套层级一致有效。
    /// </summary>
    [Theory]
    [InlineData("EnableFullAutomation", "Persistence:AutoTuning:Autonomous:EnableFullAutomation")]
    [InlineData("Validation:DelayCycles", "Persistence:AutoTuning:Autonomous:Validation:DelayCycles")]
    [InlineData("CapacityPrediction:EnableCapacityPrediction", "Persistence:AutoTuning:Autonomous:CapacityPrediction:EnableCapacityPrediction")]
    [InlineData("CapacityPrediction:PredictionWindowDays", "Persistence:AutoTuning:Autonomous:CapacityPrediction:PredictionWindowDays")]
    [InlineData("CapacityPrediction:GrowthRateThreshold", "Persistence:AutoTuning:Autonomous:CapacityPrediction:GrowthRateThreshold")]
    [InlineData("SchemaSync:EnableAutoSchemaSync", "Persistence:AutoTuning:Autonomous:SchemaSync:EnableAutoSchemaSync")]
    public void AutoTuningConfigurationHelper_BuildAutonomousKey_ShouldProduceCorrectPath(string suffix, string expectedKey) {
        var actual = AutoTuningConfigurationHelper.BuildAutonomousKey(suffix);
        Assert.Equal(expectedKey, actual);
    }

    /// <summary>
    /// 验证场景：AutoTuningConfigurationHelper_NormalizeToLocalTime_UsesLocalSemantics。
    /// </summary>
    [Fact]
    public void AutoTuningConfigurationHelper_NormalizeToLocalTime_UsesLocalSemantics() {
        var unspecified = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Unspecified);
        var normalizedUnspecified = AutoTuningConfigurationHelper.NormalizeToLocalTime(unspecified);
        Assert.Equal(DateTimeKind.Local, normalizedUnspecified.Kind);
        Assert.Equal(unspecified, DateTime.SpecifyKind(normalizedUnspecified, DateTimeKind.Unspecified));

        var local = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Local);
        var normalizedLocal = AutoTuningConfigurationHelper.NormalizeToLocalTime(local);
        Assert.Equal(local, normalizedLocal);

        var nonLocalKind = Enum.GetValues<DateTimeKind>()
            .First(kind => kind != DateTimeKind.Local && kind != DateTimeKind.Unspecified);
        var nonLocalKindInput = DateTime.SpecifyKind(unspecified, nonLocalKind);
        Assert.Throws<InvalidOperationException>(() => AutoTuningConfigurationHelper.NormalizeToLocalTime(nonLocalKindInput));
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
        var helperType = typeof(MySqlDialect).Assembly.GetType("Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects.DatabaseProviderExceptionHelper");
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
            false);
        var method = typeof(DatabaseAutoTuningHostedService).GetMethod("ApplyIndexSuggestionGuardsAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var guardedResult = await (Task<SlowQueryAnalysisResult>)method.Invoke(service, [result, CancellationToken.None])!;

        Assert.Single(guardedResult.TuningCandidates);
        Assert.Contains(guardedResult.TuningCandidates[0].SuggestedActions, action => action.Contains("create index", StringComparison.OrdinalIgnoreCase));
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
        var logger = new TestLogger<LoggingOnlyExecutionPlanRegressionProbe>();
        var probe = new LoggingOnlyExecutionPlanRegressionProbe(logger, observability);

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
        var logger = new TestLogger<LoggingOnlyExecutionPlanRegressionProbe>();
        var probe = new LoggingOnlyExecutionPlanRegressionProbe(logger, observability);
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
    /// 验证场景：ClosedLoopFlow_TriggersMonitorExecuteVerifyRollback_WithAuditAndRollbackTrigger。
    /// </summary>
    [Fact]
    public async Task ClosedLoopFlow_TriggersMonitorExecuteVerifyRollback_WithAuditAndRollbackTrigger() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
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
            logger,
            observability,
            new FixedPlanProbe(),
            new EmptyServiceScopeFactory(),
            new TestDialect(),
            pipeline,
            configuration);

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
            false);
        var metricsByFingerprint = result.Metrics.ToDictionary(static x => x.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
        var validateTask = (Task)validate.Invoke(service, [result, metricsByFingerprint, CancellationToken.None])!;
        await validateTask;

        Assert.Contains(logger.Messages, message => message.Contains("闭环自治阶段迁移") && message.Contains("CurrentStage=Execute"));
        Assert.Contains(logger.Messages, message => message.Contains("闭环自治阶段迁移") && message.Contains("CurrentStage=Verify"));
        Assert.Contains(logger.Messages, message => message.Contains("自动调优变更审计"));
        Assert.Contains(logger.Messages, message => message.Contains("闭环自治自动验证触发回滚"));
        Assert.Contains(logger.Messages, message => message.Contains("rollback-triggered"));
        Assert.Contains(observability.EventEntries, entry =>
            entry.Name == "autotuning.closed_loop.stage_transition"
            && entry.Tags.TryGetValue("evidence_id", out var evidenceId)
            && evidenceId.Contains("action-001", StringComparison.Ordinal)
            && entry.Tags.ContainsKey("correlation_id"));
        Assert.Contains(observability.EventEntries, entry =>
            entry.Name == "autotuning.validation.rollback_triggered"
            && entry.Tags.ContainsKey("evidence_id")
            && entry.Tags.ContainsKey("correlation_id"));
    }

    /// <summary>
    /// 验证场景：Validation_WhenPlanProbeDisabledOrSampleRateZero_MarksUnavailableWithoutInvokingProbe。
    /// </summary>
    [Fact]
    public async Task Validation_WhenPlanProbeDisabledOrSampleRateZero_MarksUnavailableWithoutInvokingProbe() {
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
                ["Persistence:AutoTuning:Autonomous:Validation:PlanProbe:SampleRate"] = "0"
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
            false);
        var metricsByFingerprint = result.Metrics.ToDictionary(static x => x.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
        var validateTask = (Task)validate.Invoke(service, [result, metricsByFingerprint, CancellationToken.None])!;
        await validateTask;

        Assert.Equal(0, probe.CallCount);
        Assert.Contains(logger.Messages, message => message.Contains("plan-probe-sampling-skipped"));
    }

    /// <summary>
    /// 验证场景：WhenPlanProbeSampleRateInvalid_FallsBackToDefaultAndInvokesProbe。
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


    private sealed class TestDialect : IDatabaseDialect {
        public string ProviderName => "Test";
        /// <summary>
        /// 验证场景：GetOptionalBootstrapSql。
        /// </summary>
        public IReadOnlyList<string> GetOptionalBootstrapSql() => Array.Empty<string>();
        /// <summary>
        /// 验证场景：BuildAutomaticTuningSql。
        /// </summary>
        public IReadOnlyList<string> BuildAutomaticTuningSql(string? schemaName, string tableName, IReadOnlyList<string> whereColumns) => Array.Empty<string>();
        /// <summary>
        /// 验证场景：ShouldIgnoreAutoTuningException。
        /// </summary>
        public bool ShouldIgnoreAutoTuningException(Exception exception) => false;
        /// <summary>
        /// 验证场景：BuildAutonomousMaintenanceSql。
        /// </summary>
        public IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) => Array.Empty<string>();
    }

    /// <summary>
    /// 模拟 SQL Server ProviderName 的方言测试桩，用于验证 batch probe 的 schema 透传语义。
    /// </summary>
    private sealed class TestSqlServerDialect : IDatabaseDialect {
        public string ProviderName => "SQLServer";
        /// <summary>
        /// 验证场景：GetOptionalBootstrapSql。
        /// </summary>
        public IReadOnlyList<string> GetOptionalBootstrapSql() => Array.Empty<string>();
        /// <summary>
        /// 验证场景：BuildAutomaticTuningSql。
        /// </summary>
        public IReadOnlyList<string> BuildAutomaticTuningSql(string? schemaName, string tableName, IReadOnlyList<string> whereColumns) => Array.Empty<string>();
        /// <summary>
        /// 验证场景：ShouldIgnoreAutoTuningException。
        /// </summary>
        public bool ShouldIgnoreAutoTuningException(Exception exception) => false;
        /// <summary>
        /// 验证场景：BuildAutonomousMaintenanceSql。
        /// </summary>
        public IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) => Array.Empty<string>();
    }

    private sealed class AlwaysExistsShardingPhysicalTableProbe : IShardingPhysicalTableProbe {
        /// <summary>探测调用次数。</summary>
        public int CallCount { get; private set; }

        /// <summary>
        /// 验证场景：ExistsAsync。
        /// </summary>
        public Task<bool> ExistsAsync(DbContext dbContext, string? schemaName, string physicalTableName, CancellationToken cancellationToken) {
            CallCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class SelectiveMissingShardingPhysicalTableProbe : IShardingPhysicalTableProbe {
        /// <summary>缺失表集合。</summary>
        private readonly IReadOnlySet<string> _missingPhysicalTables;
        /// <summary>探测调用次数。</summary>
        public int CallCount { get; private set; }

        /// <summary>
        /// 初始化选择性缺失探测器。
        /// </summary>
        /// <param name="missingPhysicalTables">缺失表名集合。</param>
        public SelectiveMissingShardingPhysicalTableProbe(IEnumerable<string> missingPhysicalTables) {
            _missingPhysicalTables = new HashSet<string>(missingPhysicalTables, StringComparer.Ordinal);
        }

        /// <summary>
        /// 验证场景：ExistsAsync。
        /// </summary>
        public Task<bool> ExistsAsync(DbContext dbContext, string? schemaName, string physicalTableName, CancellationToken cancellationToken) {
            CallCount++;
            return Task.FromResult(!_missingPhysicalTables.Contains(physicalTableName));
        }
    }

    private sealed class BatchSelectiveMissingShardingPhysicalTableProbe : IBatchShardingPhysicalTableProbe {
        /// <summary>缺失表集合。</summary>
        private readonly IReadOnlySet<string> _missingPhysicalTables;
        /// <summary>批量探测调用次数。</summary>
        public int FindMissingCallCount { get; private set; }
        /// <summary>最近一次传入的 schema 名称。</summary>
        public string? LastSchemaName { get; private set; }

        /// <summary>
        /// 初始化批量探测桩。
        /// </summary>
        /// <param name="missingPhysicalTables">缺失表名集合。</param>
        public BatchSelectiveMissingShardingPhysicalTableProbe(IEnumerable<string> missingPhysicalTables) {
            _missingPhysicalTables = new HashSet<string>(missingPhysicalTables, StringComparer.Ordinal);
        }

        /// <summary>
        /// 验证场景：ExistsAsync。
        /// </summary>
        public Task<bool> ExistsAsync(DbContext dbContext, string? schemaName, string physicalTableName, CancellationToken cancellationToken) {
            return Task.FromResult(!_missingPhysicalTables.Contains(physicalTableName));
        }

        /// <summary>
        /// 验证场景：FindMissingTablesAsync。
        /// </summary>
        public Task<IReadOnlyList<string>> FindMissingTablesAsync(
            DbContext dbContext,
            string? schemaName,
            IReadOnlyList<string> physicalTableNames,
            CancellationToken cancellationToken) {
            FindMissingCallCount++;
            LastSchemaName = schemaName;
            var missing = physicalTableNames
                .Where(tableName => _missingPhysicalTables.Contains(tableName))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult<IReadOnlyList<string>>(missing);
        }
    }

    private sealed class FixedPlanProbe : IExecutionPlanRegressionProbe {
        /// <summary>
        /// 验证场景：Evaluate。
        /// </summary>
        public PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint) =>
            new(true, false, $"probe available: {providerName}/{sqlFingerprint}", "none");
    }

    private sealed class CountingPlanProbe : IExecutionPlanRegressionProbe {
        public int CallCount { get; private set; }
        /// <summary>
        /// 验证场景：Evaluate。
        /// </summary>
        public PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint) {
            CallCount++;
            return new(true, false, $"probe available: {providerName}/{sqlFingerprint}", "none");
        }
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

    private sealed class TestHostEnvironment : IHostEnvironment {
        /// <summary>
        /// 测试环境配置桩，用于隔离 IHostEnvironment 依赖并注入环境名称。
        /// </summary>
        /// <param name="environmentName">环境名称。</param>
        public TestHostEnvironment(string environmentName) {
            EnvironmentName = environmentName;
            ApplicationName = "Zeye.Sorting.Hub.Host.Tests";
            ContentRootPath = AppContext.BaseDirectory;
            ContentRootFileProvider = new Microsoft.Extensions.FileProviders.NullFileProvider();
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
    }

    private sealed class TestObservability : IAutoTuningObservability {
        public readonly List<string> Metrics = new();
        public readonly List<string> Events = new();
        public readonly List<ObservabilityEntry> MetricEntries = new();
        public readonly List<ObservabilityEntry> EventEntries = new();
        /// <summary>
        /// 验证场景：EmitMetric。
        /// </summary>
        public void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null) {
            Metrics.Add(name);
            MetricEntries.Add(new ObservabilityEntry(name, value, CloneTags(tags)));
        }
        /// <summary>
        /// 验证场景：EmitEvent。
        /// </summary>
        public void EmitEvent(string name, LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
            Events.Add($"{name}:{message}");
            EventEntries.Add(new ObservabilityEntry(name, 0d, CloneTags(tags)));
        }
        /// <summary>
        /// 验证场景：CloneTags。
        /// </summary>
        private static IReadOnlyDictionary<string, string> CloneTags(IReadOnlyDictionary<string, string>? tags) {
            return tags is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(tags, StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed record ObservabilityEntry(string Name, double Value, IReadOnlyDictionary<string, string> Tags);

    private sealed class TestLogger<T> : ILogger<T> {
        public readonly List<string> Messages = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        /// <summary>
        /// 验证场景：IsEnabled。
        /// </summary>
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            Messages.Add(formatter(state, exception));
        }
        private sealed class NullScope : IDisposable {
            public static readonly NullScope Instance = new();
            /// <summary>
            /// 验证场景：Dispose。
            /// </summary>
            public void Dispose() { }
        }
    }

    private sealed class EmptyServiceScopeFactory : IServiceScopeFactory {
        /// <summary>
        /// 验证场景：CreateScope。
        /// </summary>
        public IServiceScope CreateScope() => new EmptyServiceScope();
        private sealed class EmptyServiceScope : IServiceScope {
            public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
            /// <summary>
            /// 验证场景：Dispose。
            /// </summary>
            public void Dispose() { }
        }
    }

    /// <summary>
    /// 构建最小可运行的分表治理配置。
    /// </summary>
    /// <param name="createShardingTableOnStarting">是否启用启动自动建表。</param>
    /// <param name="enableManualPrebuildGuard">是否启用手工预建守卫。</param>
    /// <param name="timeGranularity">时间粒度配置（PerDay/PerMonth）。</param>
    /// <param name="prebuildWindowHours">预建窗口小时数。</param>
    /// <param name="prebuiltDates">预建日期清单（yyyy-MM-dd）。</param>
    /// <returns>配置对象。</returns>
    private static IConfiguration BuildPerDayGovernanceConfiguration(
        bool createShardingTableOnStarting,
        bool enableManualPrebuildGuard,
        string timeGranularity,
        int prebuildWindowHours,
        IReadOnlyList<string> prebuiltDates) {
        var configData = new Dictionary<string, string?> {
            ["Persistence:Migration:FailureStrategy:NonProduction"] = "Degraded",
            ["Persistence:Sharding:CreateShardingTableOnStarting"] = createShardingTableOnStarting.ToString(),
            ["Persistence:Sharding:Governance:EnableManualPrebuildGuard"] = enableManualPrebuildGuard.ToString(),
            ["Persistence:Sharding:Governance:Runbook"] = "docs/internal/sharding-governance-runbook",
            ["Persistence:Sharding:Governance:PrebuildWindowHours"] = prebuildWindowHours.ToString(),
            ["Persistence:Sharding:HashSharding:ExpansionPlan:CurrentMod"] = "16",
            ["Persistence:Sharding:HashSharding:ExpansionPlan:TargetMod"] = "32",
            ["Persistence:Sharding:Strategy:Mode"] = "Time",
            ["Persistence:Sharding:Strategy:Time:Granularity"] = timeGranularity
        };
        for (var i = 0; i < prebuiltDates.Count; i++) {
            configData[$"Persistence:Sharding:Governance:PrebuiltPerDayDates:{i}"] = prebuiltDates[i];
        }

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
            configuration);
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
    /// 通过反射调用启动期分表治理守卫（仅验证守卫，不触发后续迁移逻辑）。
    /// </summary>
    /// <param name="service">初始化托管服务实例。</param>
    /// <returns>守卫执行任务。</returns>
    private static async Task InvokeShardingGovernanceGuardAsync(DatabaseInitializerHostedService service) {
        var validateMethod = typeof(DatabaseInitializerHostedService)
            .GetMethod("ValidateShardingGovernanceGuardAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(validateMethod);

        try {
            var task = (Task?)validateMethod!.Invoke(service, [CancellationToken.None]);
            Assert.NotNull(task);
            await task!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null) {
            throw ex.InnerException;
        }
    }
}
