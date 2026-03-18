using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

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
        Assert.Equal(DatabaseInitializerHostedService.MigrationFailureMode.FailFast, mode);
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
        Assert.Equal(DatabaseInitializerHostedService.MigrationFailureMode.Degraded, mode);
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
        Assert.Equal(DatabaseInitializerHostedService.MigrationFailureMode.Degraded, productionMode);
        Assert.Equal(DatabaseInitializerHostedService.MigrationFailureMode.FailFast, nonProductionMode);
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
        Assert.Equal(DatabaseInitializerHostedService.MigrationFailureMode.FailFast, mode);
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

        Assert.Throws<InvalidOperationException>(() => AutoTuningConfigurationHelper.NormalizeToLocalTime(new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc)));
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
}
