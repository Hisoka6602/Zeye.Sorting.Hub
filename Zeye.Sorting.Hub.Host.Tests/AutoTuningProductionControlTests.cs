using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.Tests;

public sealed class AutoTuningProductionControlTests {
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

    private sealed class TestDialect : IDatabaseDialect {
        public string ProviderName => "Test";
        public IReadOnlyList<string> GetOptionalBootstrapSql() => Array.Empty<string>();
        public IReadOnlyList<string> BuildAutomaticTuningSql(string? schemaName, string tableName, IReadOnlyList<string> whereColumns) => Array.Empty<string>();
        public bool ShouldIgnoreAutoTuningException(Exception exception) => false;
        public IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) => Array.Empty<string>();
    }

    private sealed class FixedPlanProbe : IExecutionPlanRegressionProbe {
        public PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint) =>
            new(true, false, $"probe available: {providerName}/{sqlFingerprint}", "none");
    }

    private sealed class CountingPlanProbe : IExecutionPlanRegressionProbe {
        public int CallCount { get; private set; }
        public PlanRegressionSnapshot Evaluate(string providerName, string sqlFingerprint) {
            CallCount++;
            return new(true, false, $"probe available: {providerName}/{sqlFingerprint}", "none");
        }
    }

    private static void SetField(object target, string fieldName, object value) {
        var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(target, value);
    }

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

    private sealed class TestObservability : IAutoTuningObservability {
        public readonly List<string> Metrics = new();
        public readonly List<string> Events = new();
        public readonly List<ObservabilityEntry> MetricEntries = new();
        public readonly List<ObservabilityEntry> EventEntries = new();
        public void EmitMetric(string name, double value, IReadOnlyDictionary<string, string>? tags = null) {
            Metrics.Add(name);
            MetricEntries.Add(new ObservabilityEntry(name, CloneTags(tags)));
        }
        public void EmitEvent(string name, LogLevel level, string message, IReadOnlyDictionary<string, string>? tags = null) {
            Events.Add($"{name}:{message}");
            EventEntries.Add(new ObservabilityEntry(name, CloneTags(tags)));
        }
        private static IReadOnlyDictionary<string, string> CloneTags(IReadOnlyDictionary<string, string>? tags) {
            return tags is null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(tags, StringComparer.OrdinalIgnoreCase);
        }
    }

    private sealed record ObservabilityEntry(string Name, IReadOnlyDictionary<string, string> Tags);

    private sealed class TestLogger<T> : ILogger<T> {
        public readonly List<string> Messages = new();
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            Messages.Add(formatter(state, exception));
        }
        private sealed class NullScope : IDisposable {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class EmptyServiceScopeFactory : IServiceScopeFactory {
        public IServiceScope CreateScope() => new EmptyServiceScope();
        private sealed class EmptyServiceScope : IServiceScope {
            public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
            public void Dispose() { }
        }
    }
}
