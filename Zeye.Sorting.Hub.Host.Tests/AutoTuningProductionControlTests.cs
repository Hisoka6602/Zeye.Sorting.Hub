using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
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
    [Fact]
    /// <summary>
    /// 测试方法：ParcelStatus_ShouldOnlyContainThreeValues。
    /// </summary>
    public void ParcelStatus_ShouldOnlyContainThreeValues() {
        var values = Enum.GetValues<ParcelStatus>();
        Assert.Equal(3, values.Length);
        Assert.Contains(ParcelStatus.Pending, values);
        Assert.Contains(ParcelStatus.Completed, values);
        Assert.Contains(ParcelStatus.SortingException, values);
    }

    [Fact]
    /// <summary>
    /// 测试方法：Parcel_CreateAndMarkSortingException_ShouldKeepExceptionTypeConsistent。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：MigrationFailStartupPolicy_DefaultsToFalse_WhenConfigMissing。
    /// </summary>
    public void MigrationFailStartupPolicy_DefaultsToFalse_WhenConfigMissing() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var failStartup = DatabaseInitializerHostedService.ResolveFailStartupOnMigrationError(configuration);
        Assert.False(failStartup);
    }

    [Fact]
    /// <summary>
    /// 测试方法：MigrationFailStartupPolicy_ReturnsTrue_WhenConfigEnabled。
    /// </summary>
    public void MigrationFailStartupPolicy_ReturnsTrue_WhenConfigEnabled() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Migration:FailStartupOnError"] = "true"
            })
            .Build();

        var failStartup = DatabaseInitializerHostedService.ResolveFailStartupOnMigrationError(configuration);
        Assert.True(failStartup);
    }

    [Fact]
    /// <summary>
    /// 测试方法：MigrationFailStartupPolicy_ReturnsFalse_WhenConfigIsInvalid。
    /// </summary>
    public void MigrationFailStartupPolicy_ReturnsFalse_WhenConfigIsInvalid() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Migration:FailStartupOnError"] = "invalid"
            })
            .Build();

        var failStartup = DatabaseInitializerHostedService.ResolveFailStartupOnMigrationError(configuration);
        Assert.False(failStartup);
    }

    [Fact]
    /// <summary>
    /// 测试方法：ShardingGovernanceTextNormalization_UsesPlaceholderForWhitespace。
    /// </summary>
    public void ShardingGovernanceTextNormalization_UsesPlaceholderForWhitespace() {
        var normalized = DatabaseInitializerHostedService.NormalizeOptionalTextOrPlaceholder("   ", "未配置");
        Assert.Equal("未配置", normalized);
        Assert.Equal("runbook-path", DatabaseInitializerHostedService.NormalizeOptionalTextOrPlaceholder("  runbook-path  ", "未配置"));
    }

    [Fact]
    /// <summary>
    /// 测试方法：IsolationPolicy_DryRun_DoesNotExecuteSql。
    /// </summary>
    public void IsolationPolicy_DryRun_DoesNotExecuteSql() {
        var decision = ActionIsolationPolicy.Evaluate(
            enableGuard: true,
            allowDangerousActionExecution: true,
            enableDryRun: true,
            dangerousAction: false,
            isRollback: false);

        Assert.Equal(ActionIsolationDecision.DryRunOnly, decision);
    }

    [Fact]
    /// <summary>
    /// 测试方法：IsolationPolicy_BlocksDangerousAction_WhenNotAllowed。
    /// </summary>
    public void IsolationPolicy_BlocksDangerousAction_WhenNotAllowed() {
        var decision = ActionIsolationPolicy.Evaluate(
            enableGuard: true,
            allowDangerousActionExecution: false,
            enableDryRun: false,
            dangerousAction: true,
            isRollback: false);

        Assert.Equal(ActionIsolationDecision.BlockedByGuard, decision);
    }

    [Fact]
    /// <summary>
    /// 测试方法：Pipeline_AlertsSupportDebounceAndRecovery。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：UpdateAutonomousSignals_EmitsShardingObservabilityMetrics。
    /// </summary>
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
        var metricsByFingerprint = result.Metrics.ToDictionary(static metric => metric.SqlFingerprint, StringComparer.OrdinalIgnoreCase);

        var updateAutonomousSignals = typeof(DatabaseAutoTuningHostedService).GetMethod("UpdateAutonomousSignals", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        updateAutonomousSignals.Invoke(service, [result, DateTime.Now, metricsByFingerprint]);

        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hit_rate");
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.cross_table_query_ratio");
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hot_table_skew");
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hit_rate" && Math.Abs(entry.Value - 1d) < DoublePrecisionTolerance);
    }

    [Fact]
    /// <summary>
    /// 测试方法：UpdateAutonomousSignals_HitRateSupportsPartialAndNoTableReferenceCases。
    /// </summary>
    public void UpdateAutonomousSignals_HitRateSupportsPartialAndNoTableReferenceCases() {
        var logger = new TestLogger<DatabaseAutoTuningHostedService>();
        var observability = new TestObservability();
        var fixedNow = new DateTime(2026, 3, 17, 10, 0, 0, DateTimeKind.Local);
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
        var partialMetrics = partial.Metrics.ToDictionary(static metric => metric.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
        updateAutonomousSignals.Invoke(service, [partial, fixedNow, partialMetrics]);
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
        var noneMetrics = none.Metrics.ToDictionary(static metric => metric.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
        updateAutonomousSignals.Invoke(service, [none, fixedNow, noneMetrics]);
        Assert.Contains(observability.MetricEntries, entry => entry.Name == "autotuning.sharding.hit_rate" && Math.Abs(entry.Value) < DoublePrecisionTolerance);
    }

    [Fact]
    /// <summary>
    /// 测试方法：ParcelAggregateShardingCoverageGuard_ShouldCoverAllInfoValueObjects。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：AutoRollbackDecisionEngine_TriggersSevereRollback。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：AutoRollbackDecisionEngine_TriggersNormalRegressionWithoutSevere。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：VerificationResultBuilder_ExplicitlyMarksUnavailableMetrics。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：PlanRegressionProbe_SupportsUnavailableAndAvailablePaths。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：ClosedLoopFlow_TriggersMonitorExecuteVerifyRollback_WithAuditAndRollbackTrigger。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：Validation_WhenPlanProbeDisabledOrSampleRateZero_MarksUnavailableWithoutInvokingProbe。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：WhenPlanProbeSampleRateInvalid_FallsBackToDefaultAndInvokesProbe。
    /// </summary>
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

    [Theory]
    [InlineData("-0.1", 0)]
    [InlineData("1.8", 1)]
    /// <summary>
    /// 测试方法：WhenPlanProbeSampleRateOutOfRange_ClampsToLegacyBehavior。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：WhenShouldSamplePlanProbeInvoked_UsesStableHashBucket。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：ClosedLoopTracker_RecordsMonitorExecuteVerifyRollbackChain。
    /// </summary>
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

    [Fact]
    /// <summary>
    /// 测试方法：ClosedLoopTracker_CapsAt1000AndDropsOldestWhenOverflow。
    /// </summary>
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
        /// 测试方法：GetOptionalBootstrapSql。
        /// </summary>
        public IReadOnlyList<string> GetOptionalBootstrapSql() => Array.Empty<string>();
        /// <summary>
        /// 测试方法：BuildAutomaticTuningSql。
        /// </summary>
        public IReadOnlyList<string> BuildAutomaticTuningSql(string? schemaName, string tableName, IReadOnlyList<string> whereColumns) => Array.Empty<string>();
        /// <summary>
        /// 测试方法：ShouldIgnoreAutoTuningException。
        /// </summary>
        public bool ShouldIgnoreAutoTuningException(Exception exception) => false;
        /// <summary>
        /// 测试方法：BuildAutonomousMaintenanceSql。
        /// </summary>
        public IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) => Array.Empty<string>();
    }

    private sealed class FixedPlanProbe : IExecutionPlanRegressionProbe {
        /// <summary>
        /// 测试方法：Evaluate。
        /// </summary>
        public PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint) =>
            new(true, false, $"probe available: {providerName}/{sqlFingerprint}", "none");
    }

    private sealed class CountingPlanProbe : IExecutionPlanRegressionProbe {
        public int CallCount { get; private set; }
        /// <summary>
        /// 测试方法：Evaluate。
        /// </summary>
        public PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint) {
            CallCount++;
            return new(true, false, $"probe available: {providerName}/{sqlFingerprint}", "none");
        }
    }

    /// <summary>
    /// 测试方法：SetField。
    /// </summary>
    private static void SetField(object target, string fieldName, object value) {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(target, value);
    }

    /// <summary>
    /// 测试方法：SeedPendingRollback。
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
    /// 测试方法：GetSeededRollbackAction。
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
        /// 测试方法：EmitMetric。
        /// </summary>
        public void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null) {
            Metrics.Add(name);
            MetricEntries.Add(new ObservabilityEntry(name, value, CloneTags(tags)));
        }
        /// <summary>
        /// 测试方法：EmitEvent。
        /// </summary>
        public void EmitEvent(string name, LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
            Events.Add($"{name}:{message}");
            EventEntries.Add(new ObservabilityEntry(name, 0d, CloneTags(tags)));
        }
        /// <summary>
        /// 测试方法：CloneTags。
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
        /// 测试方法：IsEnabled。
        /// </summary>
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            Messages.Add(formatter(state, exception));
        }
        private sealed class NullScope : IDisposable {
            public static readonly NullScope Instance = new();
            /// <summary>
            /// 测试方法：Dispose。
            /// </summary>
            public void Dispose() { }
        }
    }

    private sealed class EmptyServiceScopeFactory : IServiceScopeFactory {
        /// <summary>
        /// 测试方法：CreateScope。
        /// </summary>
        public IServiceScope CreateScope() => new EmptyServiceScope();
        private sealed class EmptyServiceScope : IServiceScope {
            public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
            /// <summary>
            /// 测试方法：Dispose。
            /// </summary>
            public void Dispose() { }
        }
    }
}
