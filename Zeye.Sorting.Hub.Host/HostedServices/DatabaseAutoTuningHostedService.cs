using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>数据库自动调谐后台服务：慢查询分析 + 闭环自治执行/验证/回退 + 审计日志</summary>
    public sealed class DatabaseAutoTuningHostedService : BackgroundService {
        private const string AutoTuningConfigPrefix = "Persistence:AutoTuning";
        private const string AutonomousConfigPrefix = $"{AutoTuningConfigPrefix}:Autonomous";
        private const string PerformanceConfigPrefix = "Persistence:PerformanceTuning";
        private const int MaxTrackedFingerprintCount = 1000;
        private const int MaxTrackedTableCount = 500;
        private const int MaxCapacitySnapshotsPerTable = 64;
        private const string NotAvailableTag = "n/a";
        // 风险项采用加权叠加后再截断到 [0,1]，刻意不要求权重和为 1。
        private const decimal DangerousActionRiskWeight = 0.45m;
        private const decimal PeakWindowRiskWeight = 0.20m;
        private const decimal HighP99RiskWeight = 0.15m;
        private const decimal HighTimeoutRiskWeight = 0.15m;
        private const decimal HighErrorRiskWeight = 0.10m;
        private const decimal DeadlockRiskWeight = 0.20m;
        private const double HighP99ThresholdMilliseconds = 1000d;
        private const decimal HighTimeoutThresholdPercent = 1m;
        private const decimal HighErrorThresholdPercent = 1m;
        private static readonly TimeSpan PendingRollbackRetention = TimeSpan.FromHours(24);
        private static readonly Regex SqlServerCreateIndexRegex = new(
            @"\bcreate\s+(?:unique\s+)?index\s+\[(?<index>[^\]]+)\]\s+on\s+(?<table>\[[^\]]+\](?:\.\[[^\]]+\])?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        private static readonly Regex MySqlCreateIndexRegex = new(
            @"\bcreate\s+(?:unique\s+)?index\s+`(?<index>[^`]+)`\s+on\s+(?<table>(?:`[^`]+`\.)?`[^`]+`)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        private static readonly Regex LeadingCommentRegex = new(
            @"^\s*(?:(--[^\r\n]*[\r\n]+)|(/\*.*?\*/\s*))",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        private static readonly Regex DangerousDdlRegex = new(
            @"\b(create|alter|drop)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex JoinKeywordRegex = new(
            @"\b(?:left|right|inner|full|cross)?\s*join\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly ILogger<DatabaseAutoTuningHostedService> _logger;
        private readonly IAutoTuningObservability _observability;
        private readonly IExecutionPlanRegressionProbe _planRegressionProbe;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDatabaseDialect _dialect;
        private readonly SlowQueryAutoTuningPipeline _pipeline;
        private readonly int _analyzeIntervalSeconds;
        private readonly int _maxExecuteActionsPerCycle;
        private readonly int _actionExecutionTimeoutSeconds;
        private readonly bool _skipExecutionDuringPeak;
        private readonly TimeSpan _peakStartTime;
        private readonly TimeSpan _peakEndTime;
        private readonly bool _enableAutoRollback;
        private readonly bool _enableDangerousActionIsolator;
        private readonly bool _allowDangerousActionExecution;
        private readonly bool _enableActionDryRun;
        private readonly HashSet<string> _whitelistedTables;
        private readonly Dictionary<string, PendingRollbackAction> _pendingRollbackByFingerprint = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _baselineCommandTimeoutSeconds;
        private readonly int _baselineMaxRetryCount;
        private readonly int _baselineMaxRetryDelaySeconds;
        private readonly int _configuredCommandTimeoutSeconds;
        private readonly int _configuredMaxRetryCount;
        private readonly int _configuredMaxRetryDelaySeconds;
        private readonly bool _enableFullAutomation;
        private readonly int _policyMinTableHeatCalls;
        private readonly decimal _policyMaxRiskScore;
        private readonly decimal _policyPeakMaxRiskScore;
        private readonly int _policyPeakMaxTableHeatCalls;
        private readonly bool _enableAutoValidation;
        private readonly int _validationDelayCycles;
        private readonly decimal _validationP95IncreasePercent;
        private readonly decimal _validationP99IncreasePercent;
        private readonly decimal _validationErrorRateIncreasePercent;
        private readonly decimal _validationTimeoutRateIncreasePercent;
        private readonly int _validationDeadlockIncreaseCount;
        private readonly bool _enableCapacityPrediction;
        private readonly int _capacityProjectionDays;
        private readonly int _capacityGrowthAlertRows;
        private readonly int _capacityHotLayeringRows;
        private readonly int _configuredSlowQueryThresholdMilliseconds;
        private readonly int _configuredTriggerCount;
        private readonly int _configuredMaxSuggestionsPerCycle;
        private readonly int _configuredAlertP99Milliseconds;
        private readonly decimal _configuredAlertTimeoutRatePercent;
        private readonly int _configuredAlertDeadlockCount;
        private readonly decimal _severeRollbackP99IncreasePercent;
        private readonly decimal _severeRollbackTimeoutIncreasePercent;
        private readonly int _pauseActionCyclesOnRegression;
        private readonly bool _enablePlanProbe;
        private readonly decimal _planProbeSampleRate;
        private readonly Dictionary<string, int> _tableHeatByTable = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Queue<TableCapacitySnapshot>> _capacitySnapshotsByTable = new(StringComparer.OrdinalIgnoreCase);
        private int _analysisCycleCounter;
        private int _pauseExecutionUntilCycle;
        private AutoTuningClosedLoopStage _currentStage = AutoTuningClosedLoopStage.Monitor;
        private readonly AutoTuningClosedLoopTracker _closedLoopTracker = new();

        /// <summary>初始化数据库自动调谐后台服务及其运行策略参数。</summary>
        public DatabaseAutoTuningHostedService(
            ILogger<DatabaseAutoTuningHostedService> logger,
            IAutoTuningObservability observability,
            IExecutionPlanRegressionProbe planRegressionProbe,
            IServiceScopeFactory scopeFactory,
            IDatabaseDialect dialect,
            SlowQueryAutoTuningPipeline pipeline,
            IConfiguration configuration) {
            _logger = logger;
            _observability = observability;
            _planRegressionProbe = planRegressionProbe;
            _scopeFactory = scopeFactory;
            _dialect = dialect;
            _pipeline = pipeline;
            _analyzeIntervalSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningKey("AnalyzeIntervalSeconds"), 30);
            _maxExecuteActionsPerCycle = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutonomousKey("Execution:MaxExecuteActionsPerCycle"), 2);
            _actionExecutionTimeoutSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutonomousKey("Execution:ActionExecutionTimeoutSeconds"), 60);
            _skipExecutionDuringPeak = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutonomousKey("Execution:SkipExecutionDuringPeak"), true);
            _peakStartTime = AutoTuningConfigurationHelper.GetTimeOfDayOrDefault(configuration, AutonomousKey("Execution:PeakStartLocalTime"), new TimeSpan(8, 0, 0));
            _peakEndTime = AutoTuningConfigurationHelper.GetTimeOfDayOrDefault(configuration, AutonomousKey("Execution:PeakEndLocalTime"), new TimeSpan(21, 0, 0));
            _enableDangerousActionIsolator = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutonomousKey("Execution:Isolator:EnableGuard"), true);
            _allowDangerousActionExecution = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutonomousKey("Execution:Isolator:AllowDangerousActionExecution"), false);
            _enableActionDryRun = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutonomousKey("Execution:Isolator:DryRun"), false);
            _enableAutoRollback = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutonomousKey("Validation:EnableAutoRollback"), true);
            _whitelistedTables = LoadWhitelistedTables(configuration.GetSection(AutonomousKey("Execution:WhitelistedTables")));
            if (_whitelistedTables.Count == 0) {
                _logger.LogWarning("自动调优执行白名单为空：当前将阻断所有候选表自动动作。");
            }
            _baselineCommandTimeoutSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningKey("BaselineCommandTimeoutSeconds"), 30);
            _baselineMaxRetryCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningKey("BaselineMaxRetryCount"), 5);
            _baselineMaxRetryDelaySeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningKey("BaselineMaxRetryDelaySeconds"), 10);
            _configuredCommandTimeoutSeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, $"{PerformanceConfigPrefix}:CommandTimeoutSeconds", 30);
            _configuredMaxRetryCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, $"{PerformanceConfigPrefix}:MaxRetryCount", 5);
            _configuredMaxRetryDelaySeconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, $"{PerformanceConfigPrefix}:MaxRetryDelaySeconds", 10);
            _configuredSlowQueryThresholdMilliseconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningKey("SlowQueryThresholdMilliseconds"), 500);
            _configuredTriggerCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningKey("TriggerCount"), 3);
            _configuredMaxSuggestionsPerCycle = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningKey("MaxActionsPerCycle"), 3);
            _configuredAlertP99Milliseconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningKey("AlertP99Milliseconds"), 500);
            _configuredAlertTimeoutRatePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutoTuningKey("AlertTimeoutRatePercent"), 1m);
            _configuredAlertDeadlockCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningKey("AlertDeadlockCount"), 1);
            _enableFullAutomation = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutonomousKey("EnableFullAutomation"), true);
            _policyMinTableHeatCalls = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutonomousKey("Policy:MinTableHeatCalls"), 10);
            _policyMaxRiskScore = AutoTuningConfigurationHelper.GetDecimalInRangeOrDefault(configuration, AutonomousKey("Policy:MaxRiskScore"), 0.85m, 0m, 1m);
            _policyPeakMaxRiskScore = AutoTuningConfigurationHelper.GetDecimalInRangeOrDefault(configuration, AutonomousKey("Policy:PeakMaxRiskScore"), 0.45m, 0m, 1m);
            _policyPeakMaxTableHeatCalls = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutonomousKey("Policy:PeakMaxTableHeatCalls"), 50);
            _enableAutoValidation = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutonomousKey("Validation:EnableAutoValidation"), true);
            _validationDelayCycles = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutonomousKey("Validation:DelayCycles"), 1);
            _validationP95IncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutonomousKey("Validation:P95IncreasePercent"), 5m);
            _validationP99IncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutonomousKey("Validation:P99IncreasePercent"), 10m);
            _validationErrorRateIncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutonomousKey("Validation:ErrorRateIncreasePercent"), 0.5m);
            _validationTimeoutRateIncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutonomousKey("Validation:TimeoutRateIncreasePercent"), 0.5m);
            _validationDeadlockIncreaseCount = AutoTuningConfigurationHelper.GetNonNegativeIntOrDefault(configuration, AutonomousKey("Validation:DeadlockIncreaseCount"), 1);
            _enableCapacityPrediction = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutonomousKey("CapacityPrediction:EnableCapacityPrediction"), true);
            _capacityProjectionDays = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutonomousKey("CapacityPrediction:ProjectionDays"), 7);
            _capacityGrowthAlertRows = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutonomousKey("CapacityPrediction:GrowthAlertRows"), 50000);
            _capacityHotLayeringRows = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutonomousKey("CapacityPrediction:HotLayeringRows"), 200000);
            _severeRollbackP99IncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutonomousKey("Validation:SevereRollback:P99IncreasePercent"), 25m);
            _severeRollbackTimeoutIncreasePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutonomousKey("Validation:SevereRollback:TimeoutRateIncreasePercent"), 2m);
            _pauseActionCyclesOnRegression = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutonomousKey("Validation:PauseActionCyclesOnRegression"), 2);
            _enablePlanProbe = AutoTuningConfigurationHelper.GetBoolOrDefault(configuration, AutonomousKey("Validation:PlanProbe:Enable"), true);
            _planProbeSampleRate = AutoTuningConfigurationHelper.GetDecimalClampedOrDefault(configuration, AutonomousKey("Validation:PlanProbe:SampleRate"), 1m, 0m, 1m);
        }

        /// <summary>后台循环：按固定周期分析慢 SQL，并执行自治策略/验证/清理。</summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            AuditBaseline();

            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(_analyzeIntervalSeconds), stoppingToken);
                MoveToStage(AutoTuningClosedLoopStage.Monitor, "analysis-cycle-start");

                var result = _pipeline.Analyze(_dialect);
                if (result.Metrics.Count == 0) {
                    if (result.ShouldEmitDailyReport) {
                        EmitDailyReport(result);
                    }
                    continue;
                }
                _analysisCycleCounter++;
                MoveToStage(AutoTuningClosedLoopStage.Diagnose, "metrics-aggregated");

                _logger.LogInformation(
                    "慢 SQL 分析窗口完成，Provider={Provider}, GeneratedTime={GeneratedTime}, Groups={Groups}, DroppedSamples={DroppedSamples}",
                    _dialect.ProviderName,
                    result.GeneratedTime,
                    result.Metrics.Count,
                    result.DroppedSamples);
                _observability.EmitMetric("autotuning.analysis.group_count", result.Metrics.Count);

                foreach (var metric in result.Metrics) {
                    _logger.LogInformation(
                        "慢 SQL 聚合：Provider={Provider}, Fingerprint={Fingerprint}, Calls={Calls}, Rows={Rows}, P95Ms={P95Ms}, P99Ms={P99Ms}, MaxMs={MaxMs}, ErrorRatePercent={ErrorRatePercent}, TimeoutRatePercent={TimeoutRatePercent}, DeadlockCount={DeadlockCount}",
                        _dialect.ProviderName,
                        metric.SqlFingerprint,
                        metric.CallCount,
                        metric.TotalAffectedRows,
                        metric.P95Milliseconds,
                        metric.P99Milliseconds,
                        metric.MaxMilliseconds,
                        metric.ErrorRatePercent,
                        metric.TimeoutRatePercent,
                        metric.DeadlockCount);
                }

                foreach (var alert in result.Alerts) {
                    _logger.LogWarning("慢 SQL 阈值告警：Provider={Provider}, Alert={Alert}", _dialect.ProviderName, alert);
                }
                foreach (var recovery in result.RecoveryNotifications) {
                    _logger.LogInformation("慢 SQL 告警恢复：Provider={Provider}, Recovery={Recovery}", _dialect.ProviderName, recovery);
                }

                var metricsByFingerprint = result.Metrics.ToDictionary(static metric => metric.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
                UpdateAutonomousSignals(result, result.GeneratedTime, metricsByFingerprint);
                MoveToStage(AutoTuningClosedLoopStage.Execute, "execute-candidates");
                await ExecuteAutoTuningActionsAsync(result, metricsByFingerprint, stoppingToken);
                MoveToStage(AutoTuningClosedLoopStage.Verify, "validate-actions");
                await ValidateAutonomousActionsAsync(result, metricsByFingerprint, stoppingToken);
                PruneTrackingState();

                if (result.ShouldEmitDailyReport) {
                    EmitDailyReport(result);
                }
            }
        }

        /// <summary>输出每日慢 SQL 汇总报告与只读建议。</summary>
        private void EmitDailyReport(SlowQueryAnalysisResult result) {
            _logger.LogInformation(
                "每日慢 SQL 报告：Provider={Provider}, GeneratedTime={GeneratedTime}, TopCount={TopCount}, DroppedSamples={DroppedSamples}",
                _dialect.ProviderName,
                result.GeneratedTime,
                result.Metrics.Count,
                result.DroppedSamples);

            if (result.Metrics.Count == 0) {
                _logger.LogInformation("每日慢 SQL 报告：Provider={Provider}, 当前报告周期无慢 SQL 样本。", _dialect.ProviderName);
            }

            foreach (var metric in result.Metrics) {
                _logger.LogInformation(
                    "每日慢 SQL Top（按 P99/P95 排序）：Fingerprint={Fingerprint}, Calls={Calls}, Rows={Rows}, P95Ms={P95Ms}, P99Ms={P99Ms}, ErrorRatePercent={ErrorRatePercent}, TimeoutRatePercent={TimeoutRatePercent}, DeadlockCount={DeadlockCount}",
                    metric.SqlFingerprint,
                    metric.CallCount,
                    metric.TotalAffectedRows,
                    metric.P95Milliseconds,
                    metric.P99Milliseconds,
                    metric.ErrorRatePercent,
                    metric.TimeoutRatePercent,
                    metric.DeadlockCount);
            }

            foreach (var suggestion in result.SuggestionInsights) {
                _logger.LogInformation(
                    "每日慢 SQL 索引建议（只读，不自动执行，需人工确认）：Provider={Provider}, Fingerprint={Fingerprint}, Suggestion={Suggestion}, Reason={Reason}, RiskLevel={RiskLevel}, Confidence={Confidence:F2}",
                    _dialect.ProviderName,
                    suggestion.SqlFingerprint,
                    suggestion.SuggestionSql,
                    suggestion.Reason,
                    suggestion.RiskLevel,
                    suggestion.Confidence);
            }
        }

        /// <summary>执行自动调优动作及其后置维护动作。</summary>
        private async Task ExecuteAutoTuningActionsAsync(
            SlowQueryAnalysisResult result,
            IReadOnlyDictionary<string, SlowQueryMetric> metricsByFingerprint,
            CancellationToken cancellationToken) {
            // 步骤 1：先判断总开关与执行窗口。
            if (!_enableFullAutomation) {
                return;
            }
            if (_analysisCycleCounter < _pauseExecutionUntilCycle) {
                _logger.LogWarning(
                    "闭环自治执行暂停中：Provider={Provider}, CurrentCycle={CurrentCycle}, ResumeCycle={ResumeCycle}",
                    _dialect.ProviderName,
                    _analysisCycleCounter,
                    _pauseExecutionUntilCycle);
                return;
            }

            var now = DateTime.Now;
            var inPeakWindow = IsInPeakWindow(now.TimeOfDay);
            if (_skipExecutionDuringPeak && inPeakWindow) {
                _logger.LogInformation("自动调优执行已跳过：当前处于高峰时段，仅采集不变更。Provider={Provider}", _dialect.ProviderName);
                return;
            }

            // 步骤 2：逐候选执行（含白名单、风险评分、动作上限）。
            var executedCount = 0;
            foreach (var candidate in result.TuningCandidates) {
                if (executedCount >= _maxExecuteActionsPerCycle) {
                    break;
                }

                var tableKey = BuildTableKey(candidate.SchemaName, candidate.TableName);
                if (!IsWhitelisted(candidate.SchemaName, candidate.TableName)) {
                    _logger.LogInformation(
                        "自动调优白名单拦截：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}",
                        _dialect.ProviderName,
                        candidate.SqlFingerprint,
                        tableKey);
                    continue;
                }

                metricsByFingerprint.TryGetValue(candidate.SqlFingerprint, out var currentMetric);
                var tableHeat = _tableHeatByTable.TryGetValue(tableKey, out var heat) ? heat : 0;
                foreach (var actionSql in candidate.SuggestedActions) {
                    if (executedCount >= _maxExecuteActionsPerCycle) {
                        break;
                    }

                    var policyDecision = EvaluateExecutionPolicy(actionSql, currentMetric, tableHeat, inPeakWindow);
                    if (!policyDecision.ShouldExecute) {
                        _logger.LogInformation(
                            "闭环自治策略引擎拦截自动动作：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}, RiskScore={RiskScore:F2}, Reason={Reason}",
                            _dialect.ProviderName,
                            candidate.SqlFingerprint,
                            tableKey,
                            policyDecision.RiskScore,
                            policyDecision.Reason);
                        continue;
                    }

                    var rollbackSql = BuildRollbackSql(actionSql);
                    var actionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                    if (!await ExecuteThroughIsolatorAsync(
                            actionId,
                            candidate,
                            actionSql,
                            rollbackSql,
                            reason: $"execute-action, risk={policyDecision.RiskScore:F2}",
                            isRollback: false,
                            cancellationToken)) {
                        continue;
                    }
                    executedCount++;

                    // 步骤 3：登记可回滚动作，并执行自治维护 SQL。
                    if (!string.IsNullOrWhiteSpace(rollbackSql)) {
                        _pendingRollbackByFingerprint[candidate.SqlFingerprint] = new PendingRollbackAction(
                            ActionId: actionId,
                            Fingerprint: candidate.SqlFingerprint,
                            RollbackSql: rollbackSql,
                            TableKey: tableKey,
                            CreatedTime: now,
                            CreatedCycle: _analysisCycleCounter,
                            BaselineP95Milliseconds: currentMetric?.P95Milliseconds ?? 0d,
                            BaselineP99Milliseconds: currentMetric?.P99Milliseconds ?? 0d,
                            BaselineErrorRatePercent: currentMetric?.ErrorRatePercent ?? 0m,
                            BaselineTimeoutRatePercent: currentMetric?.TimeoutRatePercent ?? 0m,
                            BaselineDeadlockCount: currentMetric?.DeadlockCount ?? 0,
                            BaselineLockWaitCount: currentMetric?.LockWaitCount);
                    }

                    var maintenanceSql = _dialect.BuildAutonomousMaintenanceSql(
                        candidate.SchemaName,
                        candidate.TableName,
                        inPeakWindow,
                        policyDecision.RiskScore > _policyMaxRiskScore);
                    foreach (var maintenance in maintenanceSql) {
                        if (!await ExecuteThroughIsolatorAsync(
                                $"{actionId}-maintenance",
                                candidate,
                                maintenance,
                                rollbackSql: null,
                                reason: "autonomous-maintenance",
                                isRollback: false,
                                cancellationToken)) {
                            continue;
                        }
                    }
                }
            }
        }

        /// <summary>对已执行动作执行延迟验证，必要时触发自动回滚。</summary>
        private async Task ValidateAutonomousActionsAsync(
            SlowQueryAnalysisResult result,
            IReadOnlyDictionary<string, SlowQueryMetric> metricsByFingerprint,
            CancellationToken cancellationToken) {
            // 步骤 1：检查验证开关与待验证池。
            if (!_enableFullAutomation || !_enableAutoValidation || _pendingRollbackByFingerprint.Count == 0) {
                return;
            }

            // 步骤 2：计算回归指标并判定是否退化。
            foreach (var rollback in _pendingRollbackByFingerprint.Values.ToArray()) {
                if (_analysisCycleCounter - rollback.CreatedCycle < _validationDelayCycles) {
                    continue;
                }

                if (!metricsByFingerprint.TryGetValue(rollback.Fingerprint, out var currentMetric)) {
                    var unavailableReason = AutoTuningUnavailableReason.MetricWindowMiss;
                    var evidenceForUnavailable = BuildEvidenceContext(rollback.ActionId, rollback.Fingerprint);
                    _observability.EmitMetric(
                        "autotuning.validation.metric_unavailable",
                        1d,
                        new Dictionary<string, string> {
                            ["provider"] = _dialect.ProviderName,
                            ["stage"] = AutoTuningClosedLoopStage.Verify.ToString(),
                            ["action_id"] = rollback.ActionId,
                            ["fingerprint"] = rollback.Fingerprint,
                            ["reason"] = unavailableReason.ToTagValue(),
                            ["evidence_id"] = evidenceForUnavailable.EvidenceId,
                            ["correlation_id"] = evidenceForUnavailable.CorrelationId
                        });
                    _observability.EmitEvent(
                        "autotuning.validation.metric_unavailable",
                        LogLevel.Warning,
                        $"validation metric unavailable: {rollback.Fingerprint}",
                        new Dictionary<string, string> {
                            ["provider"] = _dialect.ProviderName,
                            ["stage"] = AutoTuningClosedLoopStage.Verify.ToString(),
                            ["action_id"] = rollback.ActionId,
                            ["fingerprint"] = rollback.Fingerprint,
                            ["reason"] = unavailableReason.ToTagValue(),
                            ["evidence_id"] = evidenceForUnavailable.EvidenceId,
                            ["correlation_id"] = evidenceForUnavailable.CorrelationId
                        });
                    _logger.LogWarning(
                        "闭环自治自动验证跳过：Provider={Provider}, Stage={Stage}, ActionId={ActionId}, Fingerprint={Fingerprint}, Reason={Reason}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                        _dialect.ProviderName,
                        AutoTuningClosedLoopStage.Verify,
                        rollback.ActionId,
                        rollback.Fingerprint,
                        unavailableReason.ToTagValue(),
                        evidenceForUnavailable.EvidenceId,
                        evidenceForUnavailable.CorrelationId);
                    continue;
                }

                var p95IncreasePercent = CalculateIncreasePercent(rollback.BaselineP95Milliseconds, currentMetric.P95Milliseconds);
                var p99IncreasePercent = CalculateIncreasePercent(rollback.BaselineP99Milliseconds, currentMetric.P99Milliseconds);
                var errorRateIncrease = currentMetric.ErrorRatePercent - rollback.BaselineErrorRatePercent;
                var timeoutRateIncrease = currentMetric.TimeoutRatePercent - rollback.BaselineTimeoutRatePercent;
                var deadlockIncrease = currentMetric.DeadlockCount - rollback.BaselineDeadlockCount;
                var lockWaitUnavailable = !rollback.BaselineLockWaitCount.HasValue || !currentMetric.LockWaitCount.HasValue;
                var lockWaitUnavailableReason = DetermineLockWaitUnavailableReason(rollback.BaselineLockWaitCount, currentMetric.LockWaitCount);
                var lockWaitStatus = lockWaitUnavailable ? "unavailable" : "available";
                var planRegression = EvaluatePlanRegression(rollback);
                var evidence = BuildEvidenceContext(rollback.ActionId, rollback.Fingerprint);

                var regressed = (_validationP95IncreasePercent > 0m && p95IncreasePercent >= _validationP95IncreasePercent)
                    || (_validationP99IncreasePercent > 0m && p99IncreasePercent >= _validationP99IncreasePercent)
                    || (_validationErrorRateIncreasePercent > 0m && errorRateIncrease >= _validationErrorRateIncreasePercent)
                    || (_validationTimeoutRateIncreasePercent > 0m && timeoutRateIncrease >= _validationTimeoutRateIncreasePercent)
                    || (_validationDeadlockIncreaseCount > 0 && deadlockIncrease >= _validationDeadlockIncreaseCount)
                    || (planRegression.IsAvailable && planRegression.IsRegressed);
                var regressionDecision = regressed
                    ? AutoRollbackDecisionEngine.Evaluate(
                        p99IncreasePercent,
                        timeoutRateIncrease,
                        lockWaitStatus,
                        _severeRollbackP99IncreasePercent,
                        _severeRollbackTimeoutIncreasePercent,
                        regressed,
                        "threshold-regression-detected")
                    : new RegressionEvaluationResult(
                        IsRegressed: false,
                        IsSevereRegression: false,
                        Reason: "validation-pass",
                        LockWaitStatus: lockWaitStatus);
                var verificationResult = AutoTuningVerificationResultBuilder.Build(
                    regressed: regressed,
                    severeRegressed: regressionDecision.IsSevereRegression,
                    reason: regressed ? regressionDecision.Reason : "validation-pass",
                    p95IncreasePercent: p95IncreasePercent,
                    p99IncreasePercent: p99IncreasePercent,
                    errorRateIncreasePercent: errorRateIncrease,
                    timeoutRateIncreasePercent: timeoutRateIncrease,
                    deadlockIncreaseCount: deadlockIncrease,
                    lockWaitBaseline: rollback.BaselineLockWaitCount,
                    lockWaitCurrent: currentMetric.LockWaitCount,
                    planRegression: planRegression,
                    lockWaitUnavailable: lockWaitUnavailable,
                    lockWaitUnavailableReason: lockWaitUnavailableReason);
                EmitValidationResult(rollback, verificationResult);

                if (!regressed) {
                    _pendingRollbackByFingerprint.Remove(rollback.Fingerprint);
                    _logger.LogInformation(
                        "闭环自治自动验证通过：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}, P95Increase={P95Increase:F2}, P99Increase={P99Increase:F2}, ErrorRateIncrease={ErrorRateIncrease:F2}, TimeoutRateIncrease={TimeoutRateIncrease:F2}, DeadlockIncrease={DeadlockIncrease}, LockWaitStatus={LockWaitStatus}, PlanRegressionSummary={PlanRegressionSummary}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                        _dialect.ProviderName,
                        rollback.Fingerprint,
                        rollback.TableKey,
                        p95IncreasePercent,
                        p99IncreasePercent,
                        errorRateIncrease,
                        timeoutRateIncrease,
                        deadlockIncrease,
                        lockWaitStatus,
                        planRegression.Summary,
                        evidence.EvidenceId,
                        evidence.CorrelationId);
                    continue;
                }

                var severeRegression = regressionDecision.IsSevereRegression;
                _logger.LogWarning(
                    "闭环自治自动验证失败：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}, P95Increase={P95Increase:F2}, P99Increase={P99Increase:F2}, ErrorRateIncrease={ErrorRateIncrease:F2}, TimeoutRateIncrease={TimeoutRateIncrease:F2}, DeadlockIncrease={DeadlockIncrease}, LockWaitStatus={LockWaitStatus}, PlanRegressionSummary={PlanRegressionSummary}, SevereRegression={SevereRegression}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                    _dialect.ProviderName,
                    rollback.Fingerprint,
                    rollback.TableKey,
                    p95IncreasePercent,
                    p99IncreasePercent,
                    errorRateIncrease,
                    timeoutRateIncrease,
                    deadlockIncrease,
                    regressionDecision.LockWaitStatus,
                    planRegression.Summary,
                    severeRegression,
                    evidence.EvidenceId,
                    evidence.CorrelationId);

                // 步骤 3：分级动作（暂停后续动作 / 立即回滚）。
                if (!severeRegression) {
                    _pauseExecutionUntilCycle = Math.Max(_pauseExecutionUntilCycle, _analysisCycleCounter + _pauseActionCyclesOnRegression);
                    _logger.LogWarning(
                        "闭环自治触发保护暂停：Provider={Provider}, Fingerprint={Fingerprint}, PauseUntilCycle={PauseUntilCycle}",
                        _dialect.ProviderName,
                        rollback.Fingerprint,
                        _pauseExecutionUntilCycle);
                }

                // 步骤 4：在允许条件下执行回滚并移除追踪记录。
                if (_enableAutoRollback) {
                    var triggerReason = severeRegression ? "validation-severe-rollback-triggered" : "validation-regression-rollback-triggered";
                    _observability.EmitMetric(
                        "autotuning.validation.rollback_triggered",
                        1d,
                        new Dictionary<string, string> {
                            ["provider"] = _dialect.ProviderName,
                            ["stage"] = AutoTuningClosedLoopStage.Verify.ToString(),
                            ["action_id"] = rollback.ActionId,
                            ["fingerprint"] = rollback.Fingerprint,
                            ["reason"] = triggerReason,
                            ["evidence_id"] = evidence.EvidenceId,
                            ["correlation_id"] = evidence.CorrelationId
                        });
                    _observability.EmitEvent(
                        "autotuning.validation.rollback_triggered",
                        LogLevel.Warning,
                        $"rollback triggered: {rollback.Fingerprint}",
                        new Dictionary<string, string> {
                            ["provider"] = _dialect.ProviderName,
                            ["stage"] = AutoTuningClosedLoopStage.Verify.ToString(),
                            ["action_id"] = rollback.ActionId,
                            ["fingerprint"] = rollback.Fingerprint,
                            ["reason"] = triggerReason,
                            ["evidence_id"] = evidence.EvidenceId,
                            ["correlation_id"] = evidence.CorrelationId
                        });
                    _logger.LogWarning(
                        "闭环自治自动验证触发回滚：Provider={Provider}, Stage={Stage}, ActionId={ActionId}, Fingerprint={Fingerprint}, Reason={Reason}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                        _dialect.ProviderName,
                        AutoTuningClosedLoopStage.Verify,
                        rollback.ActionId,
                        rollback.Fingerprint,
                        triggerReason,
                        evidence.EvidenceId,
                        evidence.CorrelationId);
                }

                if (_enableAutoRollback && await ExecuteThroughIsolatorAsync(
                        rollback.ActionId,
                        new SlowQueryTuningCandidate(rollback.Fingerprint, null, rollback.TableKey, Array.Empty<string>(), Array.Empty<string>()),
                        rollback.RollbackSql,
                        rollbackSql: null,
                        reason: severeRegression ? "severe-regression-immediate-rollback" : "regression-rollback",
                        isRollback: true,
                        cancellationToken)) {
                    MoveToStage(AutoTuningClosedLoopStage.Rollback, "validation-rollback-executed", rollback.ActionId, rollback.Fingerprint);
                    _logger.LogWarning(
                        "闭环自治自动验证回滚执行完成：Provider={Provider}, Fingerprint={Fingerprint}, ActionId={ActionId}, RollbackSql={RollbackSql}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                        _dialect.ProviderName,
                        rollback.Fingerprint,
                        rollback.ActionId,
                        rollback.RollbackSql,
                        evidence.EvidenceId,
                        evidence.CorrelationId);
                }

                _pendingRollbackByFingerprint.Remove(rollback.Fingerprint);
            }
        }

        /// <summary>统一危险动作隔离入口（开关、dry-run、审计、执行）。</summary>
        private async Task<bool> ExecuteThroughIsolatorAsync(
            string actionId,
            SlowQueryTuningCandidate candidate,
            string actionSql,
            string? rollbackSql,
            string reason,
            bool isRollback,
            CancellationToken cancellationToken) {
            var dangerous = IsDangerousAction(actionSql);
            var isolationDecision = ActionIsolationPolicy.Evaluate(
                _enableDangerousActionIsolator,
                _allowDangerousActionExecution,
                _enableActionDryRun,
                dangerous,
                isRollback);
            if (isolationDecision == ActionIsolationDecision.BlockedByGuard) {
                EmitAuditLog(actionId, candidate, actionSql, rollbackSql, executed: false, dryRun: false, reason: $"{reason}, blocked-by-guard");
                _logger.LogWarning(
                    "危险动作隔离器拦截动作：Provider={Provider}, ActionId={ActionId}, Fingerprint={Fingerprint}, Sql={Sql}",
                    _dialect.ProviderName,
                    actionId,
                    candidate.SqlFingerprint,
                    actionSql);
                return false;
            }

            if (isolationDecision == ActionIsolationDecision.DryRunOnly) {
                EmitAuditLog(actionId, candidate, actionSql, rollbackSql, executed: false, dryRun: true, reason: $"{reason}, dry-run");
                _observability.EmitEvent(
                    "autotuning.isolator.dry_run",
                    LogLevel.Information,
                    $"Dry-run blocked SQL execution: {candidate.SqlFingerprint}",
                    new Dictionary<string, string> {
                        ["provider"] = _dialect.ProviderName,
                        ["action_id"] = actionId
                    });
                return false;
            }

            var executed = await TryExecuteSqlAsync(actionSql, candidate.SqlFingerprint, isRollback, cancellationToken);
            EmitAuditLog(actionId, candidate, actionSql, rollbackSql, executed: executed, dryRun: false, reason: reason);
            if (executed) {
                _observability.EmitMetric("autotuning.action.executed", 1d, new Dictionary<string, string> {
                    ["provider"] = _dialect.ProviderName,
                    ["is_rollback"] = isRollback ? "true" : "false"
                });
            }

            return executed;
        }

        /// <summary>在独立作用域中执行单条 SQL。</summary>
        private async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken) {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();
            dbContext.Database.SetCommandTimeout(_actionExecutionTimeoutSeconds);
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        /// <summary>包装 SQL 执行并处理可忽略异常。</summary>
        private async Task<bool> TryExecuteSqlAsync(string sql, string fingerprint, bool isRollback, CancellationToken cancellationToken) {
            try {
                await ExecuteSqlAsync(sql, cancellationToken);
                return true;
            } catch (Exception ex) when (_dialect.ShouldIgnoreAutoTuningException(ex)) {
                _logger.LogWarning(
                    ex,
                    "自动调优 SQL 已忽略异常：Provider={Provider}, Fingerprint={Fingerprint}, IsRollback={IsRollback}, Sql={Sql}",
                    _dialect.ProviderName,
                    fingerprint,
                    isRollback,
                    sql);
                return false;
            } catch (Exception ex) {
                _logger.LogError(
                    ex,
                    "自动调优 SQL 执行失败：Provider={Provider}, Fingerprint={Fingerprint}, IsRollback={IsRollback}, Sql={Sql}",
                    _dialect.ProviderName,
                    fingerprint,
                    isRollback,
                    sql);
                return false;
            }
        }

        /// <summary>计算百分比增幅（仅统计正向增长）。</summary>
        private static decimal CalculateIncreasePercent(double previousValue, double currentValue) {
            if (previousValue <= 0d) {
                return currentValue > 0d ? 100m : 0m;
            }

            var increase = currentValue - previousValue;
            if (increase <= 0d) {
                return 0m;
            }

            return (decimal)(increase / previousValue * 100d);
        }

        private static AutoTuningUnavailableReason DetermineLockWaitUnavailableReason(int? baselineLockWaitCount, int? currentLockWaitCount) {
            if (!baselineLockWaitCount.HasValue && !currentLockWaitCount.HasValue) {
                return AutoTuningUnavailableReason.BaselineAndCurrentUnavailable;
            }

            if (!baselineLockWaitCount.HasValue) {
                return AutoTuningUnavailableReason.BaselineUnavailable;
            }

            if (!currentLockWaitCount.HasValue) {
                return AutoTuningUnavailableReason.CurrentUnavailable;
            }

            return AutoTuningUnavailableReason.Sampled;
        }

        /// <summary>输出自动调优动作审计日志。</summary>
        private void EmitAuditLog(
            string actionId,
            SlowQueryTuningCandidate candidate,
            string actionSql,
            string? rollbackSql,
            bool executed,
            bool dryRun,
            string reason) {
            _logger.LogInformation(
                "自动调优变更审计：Provider={Provider}, ActionId={ActionId}, Fingerprint={Fingerprint}, Table={Table}, Executed={Executed}, DryRun={DryRun}, Reason={Reason}, ActionSql={ActionSql}, RollbackSql={RollbackSql}",
                _dialect.ProviderName,
                actionId,
                candidate.SqlFingerprint,
                BuildTableKey(candidate.SchemaName, candidate.TableName),
                executed,
                dryRun,
                reason,
                actionSql,
                rollbackSql ?? string.Empty);
        }

        private void MoveToStage(AutoTuningClosedLoopStage stage, string reason, string? actionId = null, string? fingerprint = null) {
            if (_currentStage == stage) {
                return;
            }

            var previousStage = _currentStage;
            _currentStage = stage;
            _closedLoopTracker.MoveTo(stage);
            var normalizedActionId = NormalizeTagValue(actionId);
            var normalizedFingerprint = NormalizeTagValue(fingerprint);
            var evidence = BuildEvidenceContext(normalizedActionId, normalizedFingerprint, normalized: true);
            _logger.LogInformation(
                "闭环自治阶段迁移：Provider={Provider}, PreviousStage={PreviousStage}, CurrentStage={CurrentStage}, ActionId={ActionId}, Fingerprint={Fingerprint}, Reason={Reason}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}",
                _dialect.ProviderName,
                previousStage,
                _currentStage,
                normalizedActionId,
                normalizedFingerprint,
                reason,
                evidence.EvidenceId,
                evidence.CorrelationId);
            _observability.EmitMetric(
                "autotuning.closed_loop.stage_transition",
                1d,
                new Dictionary<string, string> {
                    ["provider"] = _dialect.ProviderName,
                    ["stage"] = _currentStage.ToString(),
                    ["action_id"] = normalizedActionId,
                    ["fingerprint"] = normalizedFingerprint,
                    ["reason"] = reason,
                    ["evidence_id"] = evidence.EvidenceId,
                    ["correlation_id"] = evidence.CorrelationId
                });
            _observability.EmitEvent(
                "autotuning.closed_loop.stage_transition",
                LogLevel.Information,
                $"stage moved: {previousStage} -> {_currentStage}",
                new Dictionary<string, string> {
                    ["provider"] = _dialect.ProviderName,
                    ["stage"] = _currentStage.ToString(),
                    ["action_id"] = normalizedActionId,
                    ["fingerprint"] = normalizedFingerprint,
                    ["reason"] = reason,
                    ["evidence_id"] = evidence.EvidenceId,
                    ["correlation_id"] = evidence.CorrelationId
                });
        }

        private void EmitValidationResult(PendingRollbackAction rollback, AutoTuningVerificationResult result) {
            var level = string.Equals(result.Verdict, "pass", StringComparison.OrdinalIgnoreCase)
                ? LogLevel.Information
                : LogLevel.Warning;
            var tags = new Dictionary<string, string> {
                ["provider"] = _dialect.ProviderName,
                ["stage"] = AutoTuningClosedLoopStage.Verify.ToString(),
                ["action_id"] = rollback.ActionId,
                ["fingerprint"] = rollback.Fingerprint,
                ["reason"] = result.Reason,
                ["verdict"] = result.Verdict
            };
            var evidence = BuildEvidenceContext(rollback.ActionId, rollback.Fingerprint);
            tags["evidence_id"] = evidence.EvidenceId;
            tags["correlation_id"] = evidence.CorrelationId;
            _observability.EmitMetric("autotuning.validation.result", 1d, tags);
            _observability.EmitEvent(
                "autotuning.validation.result",
                level,
                $"validation verdict: {result.Verdict}",
                tags);
            _logger.LogInformation(
                "闭环自治自动验证结果：Provider={Provider}, Stage={Stage}, ActionId={ActionId}, Fingerprint={Fingerprint}, Verdict={Verdict}, Reason={Reason}, EvidenceId={EvidenceId}, CorrelationId={CorrelationId}, SnapshotDiff={SnapshotDiff}",
                _dialect.ProviderName,
                AutoTuningClosedLoopStage.Verify,
                rollback.ActionId,
                rollback.Fingerprint,
                result.Verdict,
                result.Reason,
                evidence.EvidenceId,
                evidence.CorrelationId,
                string.Join(" | ", result.SnapshotDiff.Select(static diff => $"{diff.Name}:{diff.Status}:{diff.Delta}:{diff.Reason}")));
        }

        private static string NormalizeTagValue(string? value) => string.IsNullOrWhiteSpace(value) ? NotAvailableTag : value.Trim();

        private PlanRegressionSnapshot EvaluatePlanRegression(PendingRollbackAction rollback) {
            var normalizedFingerprint = NormalizeTagValue(rollback.Fingerprint);
            if (!_enablePlanProbe) {
                return BuildUnavailablePlanRegressionSnapshot(normalizedFingerprint, AutoTuningUnavailableReason.PlanProbeDisabled);
            }

            if (!ShouldSamplePlanProbe(rollback)) {
                return BuildUnavailablePlanRegressionSnapshot(normalizedFingerprint, AutoTuningUnavailableReason.PlanProbeSamplingSkipped);
            }

            return _planRegressionProbe.Evaluate(_dialect.ProviderName, rollback.Fingerprint);
        }

        private bool ShouldSamplePlanProbe(PendingRollbackAction rollback) {
            if (_planProbeSampleRate <= 0m) {
                return false;
            }

            if (_planProbeSampleRate >= 1m) {
                return true;
            }

            var seed = $"{rollback.ActionId}:{rollback.Fingerprint}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
            var bucket = BinaryPrimitives.ReadUInt32LittleEndian(hashBytes) % 10000u;
            var threshold = (int)Math.Round((double)(_planProbeSampleRate * 10000m), MidpointRounding.AwayFromZero);
            return bucket < (uint)threshold;
        }

        private PlanRegressionSnapshot BuildUnavailablePlanRegressionSnapshot(string fingerprint, AutoTuningUnavailableReason reason) {
            return new PlanRegressionSnapshot(
                IsAvailable: false,
                IsRegressed: false,
                Summary: $"fingerprint={fingerprint}, provider={_dialect.ProviderName}, plan regression unavailable({reason.ToTagValue()})",
                UnavailableReason: reason.ToTagValue());
        }

        private static EvidenceContext BuildEvidenceContext(string? actionId, string? fingerprint, bool normalized = false) {
            var normalizedActionId = normalized ? actionId ?? NotAvailableTag : NormalizeTagValue(actionId);
            var normalizedFingerprint = normalized ? fingerprint ?? NotAvailableTag : NormalizeTagValue(fingerprint);
            var evidenceId = $"{normalizedActionId}:{normalizedFingerprint}";
            var correlationId = normalizedActionId != NotAvailableTag ? normalizedActionId : normalizedFingerprint;
            return new EvidenceContext(evidenceId, correlationId);
        }

        /// <summary>判断候选表是否命中执行白名单。</summary>
        private bool IsWhitelisted(string? schemaName, string tableName) {
            if (_whitelistedTables.Count == 0) {
                return false;
            }

            return _whitelistedTables.Contains(BuildTableKey(schemaName, tableName));
        }

        /// <summary>根据风险评分、热度与时段判定动作是否可执行。</summary>
        private PolicyDecision EvaluateExecutionPolicy(string actionSql, SlowQueryMetric? metric, int tableHeat, bool inPeakWindow) {
            var riskScore = CalculateRiskScore(actionSql, metric, inPeakWindow);
            if (tableHeat < _policyMinTableHeatCalls) {
                return PolicyDecision.Skip(riskScore, $"table heat too low: {tableHeat} < {_policyMinTableHeatCalls}");
            }

            if (inPeakWindow && tableHeat > _policyPeakMaxTableHeatCalls) {
                return PolicyDecision.Skip(riskScore, $"table heat too high in peak: {tableHeat} > {_policyPeakMaxTableHeatCalls}");
            }

            var threshold = inPeakWindow ? _policyPeakMaxRiskScore : _policyMaxRiskScore;
            if (riskScore > threshold) {
                return PolicyDecision.Skip(riskScore, $"risk score {riskScore:F2} > threshold {threshold:F2}");
            }

            return PolicyDecision.Execute(riskScore, "autonomous-policy-approved");
        }

        /// <summary>计算动作风险分（0~1）。</summary>
        private decimal CalculateRiskScore(string actionSql, SlowQueryMetric? metric, bool inPeakWindow) {
            decimal riskScore = 0m;
            if (IsDangerousAction(actionSql)) {
                riskScore += DangerousActionRiskWeight;
            }

            if (inPeakWindow) {
                riskScore += PeakWindowRiskWeight;
            }

            if (metric is not null) {
                if (metric.P99Milliseconds >= HighP99ThresholdMilliseconds) {
                    riskScore += HighP99RiskWeight;
                }

                if (metric.TimeoutRatePercent >= HighTimeoutThresholdPercent) {
                    riskScore += HighTimeoutRiskWeight;
                }

                if (metric.ErrorRatePercent >= HighErrorThresholdPercent) {
                    riskScore += HighErrorRiskWeight;
                }

                if (metric.DeadlockCount > 0) {
                    riskScore += DeadlockRiskWeight;
                }
            }

            return decimal.Clamp(riskScore, 0m, 1m);
        }

        /// <summary>更新表热度与容量观测快照。</summary>
        private void UpdateAutonomousSignals(
            SlowQueryAnalysisResult result,
            DateTime cycleTime,
            IReadOnlyDictionary<string, SlowQueryMetric> metricsByFingerprint) {
            // 容量预测与执行热度是相互独立的功能，分别检查各自的开关。
            if (!_enableFullAutomation && !_enableCapacityPrediction) {
                return;
            }

            // 步骤 1：对历史热度做衰减（仅全自动模式下维护热度，避免旧热点长期占用容量）。
            if (_enableFullAutomation) {
                foreach (var table in _tableHeatByTable.Keys.ToArray()) {
                    var decayed = (int)Math.Floor(_tableHeatByTable[table] * 0.9d);
                    if (decayed <= 0) {
                        _tableHeatByTable.Remove(table);
                        continue;
                    }

                    _tableHeatByTable[table] = decayed;
                }
            }

            // 步骤 2：按本轮候选聚合表级调用量与影响行数。
            var tableSamples = new Dictionary<string, (long Rows, int Calls)>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in result.TuningCandidates) {
                if (!metricsByFingerprint.TryGetValue(candidate.SqlFingerprint, out var metric)) {
                    continue;
                }

                var tableKey = BuildTableKey(candidate.SchemaName, candidate.TableName);
                if (!tableSamples.TryGetValue(tableKey, out var sample)) {
                    sample = (Rows: 0L, Calls: 0);
                }

                sample.Rows += metric.TotalAffectedRows;
                sample.Calls += metric.CallCount;
                tableSamples[tableKey] = sample;
            }

            // 步骤 3：刷新热度与容量样本，并触发趋势评估。
            foreach (var pair in tableSamples) {
                // 表热度仅在全自动模式下维护（用于执行策略评估）。
                if (_enableFullAutomation) {
                    if (_tableHeatByTable.TryGetValue(pair.Key, out var currentHeat)) {
                        _tableHeatByTable[pair.Key] = currentHeat + pair.Value.Calls;
                    }
                    else {
                        _tableHeatByTable[pair.Key] = pair.Value.Calls;
                    }
                }

                // 容量预测独立于全自动开关，单独由 _enableCapacityPrediction 控制。
                if (_enableCapacityPrediction) {
                    if (!_capacitySnapshotsByTable.TryGetValue(pair.Key, out var queue)) {
                        queue = new Queue<TableCapacitySnapshot>(MaxCapacitySnapshotsPerTable);
                        _capacitySnapshotsByTable[pair.Key] = queue;
                    }

                    if (queue.Count >= MaxCapacitySnapshotsPerTable) {
                        queue.Dequeue();
                    }

                    queue.Enqueue(new TableCapacitySnapshot(cycleTime, pair.Value.Rows, pair.Value.Calls));
                    EmitCapacityForecast(pair.Key, queue);
                }
            }

            EmitShardingObservabilityMetrics(result.Metrics, tableSamples);
        }

        /// <summary>输出分表命中率、跨分表查询占比与热点倾斜三类观测指标。</summary>
        private void EmitShardingObservabilityMetrics(
            IReadOnlyList<SlowQueryMetric> metrics,
            IReadOnlyDictionary<string, (long Rows, int Calls)> tableSamples) {
            if (metrics.Count == 0) {
                return;
            }

            var totalCalls = metrics.Sum(static metric => metric.CallCount);
            if (totalCalls <= 0) {
                return;
            }

            var shardingHitCalls = tableSamples.Values.Sum(static sample => sample.Calls);
            var hitRate = Math.Clamp((double)shardingHitCalls / totalCalls, 0d, 1d);
            var crossTableCalls = metrics.Sum(static metric => IsCrossTableQuery(metric.SampleSql) ? metric.CallCount : 0);
            var crossTableRatio = Math.Clamp((double)crossTableCalls / totalCalls, 0d, 1d);

            var hotTableSkew = 0d;
            if (tableSamples.Count > 0) {
                var maxCalls = tableSamples.Values.Max(static sample => sample.Calls);
                var averageCalls = tableSamples.Values.Average(static sample => sample.Calls);
                hotTableSkew = averageCalls > 0d ? maxCalls / averageCalls : 0d;
            }

            var tags = new Dictionary<string, string> {
                ["provider"] = _dialect.ProviderName
            };
            _observability.EmitMetric("autotuning.sharding.hit_rate", hitRate, tags);
            _observability.EmitMetric("autotuning.sharding.cross_table_query_ratio", crossTableRatio, tags);
            _observability.EmitMetric("autotuning.sharding.hot_table_skew", hotTableSkew, tags);
        }

        /// <summary>根据表级样本序列估算查询体量增长趋势并告警。</summary>
        private void EmitCapacityForecast(string tableKey, Queue<TableCapacitySnapshot> snapshots) {
            // 步骤 1：确保观察窗口足够，避免短样本导致误报。
            if (snapshots.Count < 2) {
                return;
            }

            var ordered = snapshots.ToArray();
            var first = ordered[0];
            var last = ordered[^1];
            var observationWindowSeconds = (last.CapturedLocalTime - first.CapturedLocalTime).TotalSeconds;
            if (observationWindowSeconds <= 0d) {
                _logger.LogWarning(
                    "闭环自治查询体量趋势预测跳过：Provider={Provider}, Table={Table}, ElapsedSeconds={ElapsedSeconds:F0}, Reason={Reason}",
                    _dialect.ProviderName,
                    tableKey,
                    observationWindowSeconds,
                    "non-positive elapsed time");
                return;
            }

            var minimumElapsedSeconds = _analyzeIntervalSeconds * 3d;
            if (observationWindowSeconds < minimumElapsedSeconds) {
                _logger.LogInformation(
                    "闭环自治查询体量趋势预测跳过：Provider={Provider}, Table={Table}, ElapsedSeconds={ElapsedSeconds:F0}, MinimumElapsedSeconds={MinimumElapsedSeconds:F0}, Reason={Reason}",
                    _dialect.ProviderName,
                    tableKey,
                    observationWindowSeconds,
                    minimumElapsedSeconds,
                    "insufficient observation window");
                return;
            }

            // 步骤 2：按窗口样本均值估算投影体量（AffectedRows 为每轮窗口值，不能直接做首尾差分增长）。
            var averageRowsPerCycle = ordered.Average(static snapshot => (double)snapshot.AffectedRows);
            var cyclesPerDay = TimeSpan.FromDays(1).TotalSeconds / _analyzeIntervalSeconds;
            var projectedWindowRows = averageRowsPerCycle * cyclesPerDay * _capacityProjectionDays;
            if (projectedWindowRows < _capacityGrowthAlertRows) {
                return;
            }

            // 步骤 3：达到投影阈值后输出容量告警并给出治理建议。
            _logger.LogWarning(
                "闭环自治查询体量趋势告警：Provider={Provider}, Table={Table}, ProjectionDays={ProjectionDays}, ProjectedWindowQueryVolume={ProjectedWindowQueryVolume:F0}, AverageQueryVolumePerCycle={AverageRowsPerCycle:F0}, ActionHint={Hint}",
                _dialect.ProviderName,
                tableKey,
                _capacityProjectionDays,
                projectedWindowRows,
                averageRowsPerCycle,
                projectedWindowRows >= _capacityHotLayeringRows ? "建议冷热分层 + 历史归档 + 索引重评估" : "建议优先检查索引与统计信息维护策略");
        }

        /// <summary>生成统一的小写表标识（schema.table）。</summary>
        private static string BuildTableKey(string? schemaName, string tableName) {
            return string.IsNullOrWhiteSpace(schemaName)
                ? tableName.Trim().ToLowerInvariant()
                : $"{schemaName.Trim().ToLowerInvariant()}.{tableName.Trim().ToLowerInvariant()}";
        }

        /// <summary>基于 JOIN 关键字判断是否为跨分表/跨表查询 (当前仅覆盖 JOIN 场景)。</summary>
        private static bool IsCrossTableQuery(string sql) {
            if (string.IsNullOrWhiteSpace(sql)) {
                return false;
            }

            var normalizedSql = TrimLeadingComments(sql);
            return JoinKeywordRegex.IsMatch(normalizedSql);
        }

        /// <summary>判断当前时刻是否位于高峰执行窗口。</summary>
        private bool IsInPeakWindow(TimeSpan nowTime) {
            if (_peakStartTime == _peakEndTime) {
                return false;
            }

            if (_peakStartTime < _peakEndTime) {
                return nowTime >= _peakStartTime && nowTime < _peakEndTime;
            }

            return nowTime >= _peakStartTime || nowTime < _peakEndTime;
        }

        /// <summary>识别是否为高风险 DDL 动作。</summary>
        private bool IsDangerousAction(string actionSql) {
            if (string.IsNullOrWhiteSpace(actionSql)) {
                return false;
            }

            var normalized = TrimLeadingComments(actionSql);
            return DangerousDdlRegex.IsMatch(normalized);
        }

        /// <summary>尝试为自动创建索引动作生成回滚 SQL。</summary>
        private string? BuildRollbackSql(string actionSql) {
            if (string.IsNullOrWhiteSpace(actionSql)) {
                return null;
            }

            var normalized = TrimLeadingComments(actionSql);
            var sqlServerMatch = SqlServerCreateIndexRegex.Match(normalized);
            if (sqlServerMatch.Success) {
                var indexName = sqlServerMatch.Groups["index"].Value;
                var tableName = sqlServerMatch.Groups["table"].Value;
                return $"DROP INDEX [{indexName}] ON {tableName}";
            }

            var mySqlMatch = MySqlCreateIndexRegex.Match(normalized);
            if (mySqlMatch.Success) {
                var indexName = mySqlMatch.Groups["index"].Value;
                var tableName = mySqlMatch.Groups["table"].Value;
                return $"DROP INDEX `{indexName}` ON {tableName}";
            }

            return null;
        }

        /// <summary>审计当前运行参数是否符合推荐基线。</summary>
        private void AuditBaseline() {
            AuditBaselineItem("CommandTimeoutSeconds", _configuredCommandTimeoutSeconds, _baselineCommandTimeoutSeconds);
            AuditBaselineItem("MaxRetryCount", _configuredMaxRetryCount, _baselineMaxRetryCount);
            AuditBaselineItem("MaxRetryDelaySeconds", _configuredMaxRetryDelaySeconds, _baselineMaxRetryDelaySeconds);
            AuditBaselineItem("SlowQueryThresholdMilliseconds", _configuredSlowQueryThresholdMilliseconds, 500);
            AuditBaselineItem("TriggerCount", _configuredTriggerCount, 3);
            AuditBaselineItem("MaxActionsPerCycle", _configuredMaxSuggestionsPerCycle, 3);
            AuditBaselineItem("AlertP99Milliseconds", _configuredAlertP99Milliseconds, 500);
            AuditBaselineItem("AlertTimeoutRatePercent", (double)_configuredAlertTimeoutRatePercent, 1d);
            AuditBaselineItem("AlertDeadlockCount", _configuredAlertDeadlockCount, 1);
        }

        /// <summary>输出单个参数的基线审计结果。</summary>
        private void AuditBaselineItem(string key, int configured, int baseline) {
            AuditBaselineItem(key, (double)configured, baseline);
        }

        private void AuditBaselineItem(string key, double configured, double baseline) {
            if (Math.Abs(configured - baseline) < 0.0001d) {
                _logger.LogInformation("运行参数基线审计通过：Key={Key}, Current={Current}, Baseline={Baseline}", key, configured, baseline);
                return;
            }

            var ratio = baseline <= 0d ? 1d : Math.Abs(configured - baseline) / baseline;
            var level = ratio >= 0.5d ? "high" : ratio >= 0.2d ? "medium" : "low";
            _logger.LogWarning(
                "运行参数基线审计告警：Key={Key}, Current={Current}, Baseline={Baseline}, DeviationLevel={DeviationLevel}, Recommended={Recommended}",
                key,
                configured,
                baseline,
                level,
                baseline);
            _observability.EmitEvent(
                "autotuning.baseline.deviation",
                LogLevel.Warning,
                $"baseline deviation: {key}",
                new Dictionary<string, string> {
                    ["key"] = key,
                    ["level"] = level
                });
        }

        /// <summary>加载执行白名单并标准化为小写。</summary>
        private static HashSet<string> LoadWhitelistedTables(IConfigurationSection section) {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in section.GetChildren()) {
                var value = child.Value?.Trim();
                if (string.IsNullOrWhiteSpace(value)) {
                    continue;
                }

                tables.Add(value.ToLowerInvariant());
            }

            return tables;
        }

        /// <summary>移除 SQL 前导注释，便于后续规则匹配。</summary>
        private static string TrimLeadingComments(string sql) {
            var normalized = sql;
            while (true) {
                var match = LeadingCommentRegex.Match(normalized);
                if (!match.Success) {
                    break;
                }

                normalized = normalized[match.Length..];
            }

            return normalized.TrimStart();
        }

        /// <summary>裁剪追踪状态，限制内存占用并剔除过期记录。</summary>
        private void PruneTrackingState() {
            var rollbackOverflow = _pendingRollbackByFingerprint.Count - MaxTrackedFingerprintCount;
            if (rollbackOverflow > 0) {
                var removeRollbacks = _pendingRollbackByFingerprint
                    .OrderBy(static pair => pair.Value.CreatedTime)
                    .Take(rollbackOverflow)
                    .Select(static pair => pair.Key)
                    .ToArray();
                foreach (var key in removeRollbacks) {
                    _pendingRollbackByFingerprint.Remove(key);
                }
            }

            var now = DateTime.Now;
            var expiredFingerprints = _pendingRollbackByFingerprint
                .Where(pair => now - pair.Value.CreatedTime > PendingRollbackRetention)
                .Select(static pair => pair.Key)
                .ToArray();
            foreach (var fingerprint in expiredFingerprints) {
                _pendingRollbackByFingerprint.Remove(fingerprint);
            }

            var tableOverflow = _tableHeatByTable.Count - MaxTrackedTableCount;
            if (tableOverflow > 0) {
                var removeTables = _tableHeatByTable
                    .OrderBy(static pair => pair.Value)
                    .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(static pair => pair.Key)
                    .Take(tableOverflow)
                    .ToArray();
                foreach (var table in removeTables) {
                    _tableHeatByTable.Remove(table);
                    _capacitySnapshotsByTable.Remove(table);
                }
            }
        }

        /// <summary>生成 AutoTuning 配置全路径键名。</summary>
        private static string AutoTuningKey(string suffix) => $"{AutoTuningConfigPrefix}:{suffix}";

        /// <summary>生成 Autonomous 配置全路径键名。</summary>
        private static string AutonomousKey(string suffix) => $"{AutonomousConfigPrefix}:{suffix}";

        private sealed record PendingRollbackAction(
            string ActionId,
            string Fingerprint,
            string RollbackSql,
            string TableKey,
            DateTime CreatedTime,
            int CreatedCycle,
            double BaselineP95Milliseconds,
            double BaselineP99Milliseconds,
            decimal BaselineErrorRatePercent,
            decimal BaselineTimeoutRatePercent,
            int BaselineDeadlockCount,
            int? BaselineLockWaitCount);

        /// <summary>表级容量快照（CapturedLocalTime 必须使用本地时间语义）。</summary>
        private sealed record TableCapacitySnapshot(
            DateTime CapturedLocalTime,
            long AffectedRows,
            int CallCount);

        private readonly record struct EvidenceContext(string EvidenceId, string CorrelationId);

        private sealed record PolicyDecision(
            bool ShouldExecute,
            decimal RiskScore,
            string Reason) {
            public static PolicyDecision Execute(decimal riskScore, string reason) => new(true, riskScore, reason);
            public static PolicyDecision Skip(decimal riskScore, string reason) => new(false, riskScore, reason);
        }
    }
}
