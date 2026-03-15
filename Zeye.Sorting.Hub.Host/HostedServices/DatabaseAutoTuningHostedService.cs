using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// 数据库自动调谐后台服务：慢查询分析 + L3 闭环自治执行/验证/回退 + 审计日志
    /// </summary>
    public sealed class DatabaseAutoTuningHostedService : BackgroundService {
        private const int MaxTrackedFingerprintCount = 1000;
        private const int MaxTrackedTableCount = 500;
        private const int MaxCapacitySnapshotsPerTable = 64;
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
        private readonly ILogger<DatabaseAutoTuningHostedService> _logger;
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
        private readonly Dictionary<string, int> _tableHeatByTable = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Queue<TableCapacitySnapshot>> _capacitySnapshotsByTable = new(StringComparer.OrdinalIgnoreCase);
        private int _analysisCycleCounter;

        public DatabaseAutoTuningHostedService(
            ILogger<DatabaseAutoTuningHostedService> logger,
            IServiceScopeFactory scopeFactory,
            IDatabaseDialect dialect,
            SlowQueryAutoTuningPipeline pipeline,
            IConfiguration configuration) {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _dialect = dialect;
            _pipeline = pipeline;
            _analyzeIntervalSeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:AnalyzeIntervalSeconds", 30);
            _maxExecuteActionsPerCycle = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:L3:Execution:MaxExecuteActionsPerCycle", 2);
            _actionExecutionTimeoutSeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:L3:Execution:ActionExecutionTimeoutSeconds", 60);
            _skipExecutionDuringPeak = GetBoolOrDefault(configuration, "Persistence:AutoTuning:L3:Execution:SkipExecutionDuringPeak", true);
            _peakStartTime = GetTimeOfDayOrDefault(configuration, "Persistence:AutoTuning:L3:Execution:PeakStartLocalTime", new TimeSpan(8, 0, 0));
            _peakEndTime = GetTimeOfDayOrDefault(configuration, "Persistence:AutoTuning:L3:Execution:PeakEndLocalTime", new TimeSpan(21, 0, 0));
            _enableAutoRollback = GetBoolOrDefault(configuration, "Persistence:AutoTuning:L3:Validation:EnableAutoRollback", true);
            _whitelistedTables = LoadWhitelistedTables(configuration.GetSection("Persistence:AutoTuning:L3:Execution:WhitelistedTables"));
            _baselineCommandTimeoutSeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:BaselineCommandTimeoutSeconds", 30);
            _baselineMaxRetryCount = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:BaselineMaxRetryCount", 5);
            _baselineMaxRetryDelaySeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:BaselineMaxRetryDelaySeconds", 10);
            _configuredCommandTimeoutSeconds = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
            _configuredMaxRetryCount = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:MaxRetryCount", 5);
            _configuredMaxRetryDelaySeconds = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:MaxRetryDelaySeconds", 10);
            _enableFullAutomation = GetBoolOrDefault(configuration, "Persistence:AutoTuning:L3:EnableFullAutomation", true);
            _policyMinTableHeatCalls = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:L3:Policy:MinTableHeatCalls", 10);
            _policyMaxRiskScore = GetDecimalInRangeOrDefault(configuration, "Persistence:AutoTuning:L3:Policy:MaxRiskScore", 0.85m, 0m, 1m);
            _policyPeakMaxRiskScore = GetDecimalInRangeOrDefault(configuration, "Persistence:AutoTuning:L3:Policy:PeakMaxRiskScore", 0.45m, 0m, 1m);
            _policyPeakMaxTableHeatCalls = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:L3:Policy:PeakMaxTableHeatCalls", 50);
            _enableAutoValidation = GetBoolOrDefault(configuration, "Persistence:AutoTuning:L3:Validation:EnableAutoValidation", true);
            _validationDelayCycles = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:L3:Validation:DelayCycles", 1);
            _validationP95IncreasePercent = GetNonNegativeDecimalOrDefault(configuration, "Persistence:AutoTuning:L3:Validation:P95IncreasePercent", 5m);
            _validationP99IncreasePercent = GetNonNegativeDecimalOrDefault(configuration, "Persistence:AutoTuning:L3:Validation:P99IncreasePercent", 10m);
            _validationErrorRateIncreasePercent = GetNonNegativeDecimalOrDefault(configuration, "Persistence:AutoTuning:L3:Validation:ErrorRateIncreasePercent", 0.5m);
            _validationTimeoutRateIncreasePercent = GetNonNegativeDecimalOrDefault(configuration, "Persistence:AutoTuning:L3:Validation:TimeoutRateIncreasePercent", 0.5m);
            _validationDeadlockIncreaseCount = GetNonNegativeIntOrDefault(configuration, "Persistence:AutoTuning:L3:Validation:DeadlockIncreaseCount", 1);
            _enableCapacityPrediction = GetBoolOrDefault(configuration, "Persistence:AutoTuning:L3:CapacityPrediction:EnableCapacityPrediction", true);
            _capacityProjectionDays = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:L3:CapacityPrediction:ProjectionDays", 7);
            _capacityGrowthAlertRows = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:L3:CapacityPrediction:GrowthAlertRows", 50000);
            _capacityHotLayeringRows = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:L3:CapacityPrediction:HotLayeringRows", 200000);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            AuditBaseline();

            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(_analyzeIntervalSeconds), stoppingToken);

                var result = _pipeline.Analyze(_dialect);
                if (result.Metrics.Count == 0) {
                    if (result.ShouldEmitDailyReport) {
                        EmitDailyReport(result);
                    }
                    continue;
                }
                _analysisCycleCounter++;

                _logger.LogInformation(
                    "慢 SQL 分析窗口完成，Provider={Provider}, GeneratedTime={GeneratedTime}, Groups={Groups}, DroppedSamples={DroppedSamples}",
                    _dialect.ProviderName,
                    result.GeneratedTime,
                    result.Metrics.Count,
                    result.DroppedSamples);

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

                UpdateAutonomousSignals(result, result.GeneratedTime);
                await ExecuteAutoTuningActionsAsync(result, stoppingToken);
                await ValidateAutonomousActionsAsync(result, stoppingToken);
                PruneTrackingState();

                if (result.ShouldEmitDailyReport) {
                    EmitDailyReport(result);
                }
            }
        }

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
                    "每日慢 SQL Top：Fingerprint={Fingerprint}, Calls={Calls}, Rows={Rows}, P95Ms={P95Ms}, P99Ms={P99Ms}, ErrorRatePercent={ErrorRatePercent}, TimeoutRatePercent={TimeoutRatePercent}, DeadlockCount={DeadlockCount}",
                    metric.SqlFingerprint,
                    metric.CallCount,
                    metric.TotalAffectedRows,
                    metric.P95Milliseconds,
                    metric.P99Milliseconds,
                    metric.ErrorRatePercent,
                    metric.TimeoutRatePercent,
                    metric.DeadlockCount);
            }

            foreach (var suggestion in result.ReadOnlySuggestions) {
                _logger.LogInformation(
                    "每日慢 SQL 索引建议（只读，不自动执行，需人工确认）：Provider={Provider}, Suggestion={Suggestion}",
                    _dialect.ProviderName,
                    suggestion);
            }
        }

        private async Task ExecuteAutoTuningActionsAsync(SlowQueryAnalysisResult result, CancellationToken cancellationToken) {
            if (!_enableFullAutomation) {
                return;
            }

            var now = DateTime.Now;
            var inPeakWindow = IsInPeakWindow(now.TimeOfDay);
            if (_skipExecutionDuringPeak && inPeakWindow) {
                _logger.LogInformation("自动调优执行已跳过：当前处于高峰时段，仅采集不变更。Provider={Provider}", _dialect.ProviderName);
                return;
            }

            var executedCount = 0;
            var metricsByFingerprint = result.Metrics.ToDictionary(static metric => metric.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
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
                            "L3 策略引擎拦截自动动作：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}, RiskScore={RiskScore:F2}, Reason={Reason}",
                            _dialect.ProviderName,
                            candidate.SqlFingerprint,
                            tableKey,
                            policyDecision.RiskScore,
                            policyDecision.Reason);
                        continue;
                    }

                    var rollbackSql = BuildRollbackSql(actionSql);
                    var actionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                    if (!await TryExecuteSqlAsync(actionSql, candidate.SqlFingerprint, isRollback: false, cancellationToken)) {
                        continue;
                    }

                    EmitAuditLog(actionId, candidate, actionSql, rollbackSql, executed: true, dryRun: false, reason: $"executed, risk={policyDecision.RiskScore:F2}");
                    executedCount++;

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
                            BaselineDeadlockCount: currentMetric?.DeadlockCount ?? 0);
                    }

                    var maintenanceSql = _dialect.BuildAutonomousMaintenanceSql(
                        candidate.SchemaName,
                        candidate.TableName,
                        inPeakWindow,
                        policyDecision.RiskScore > _policyMaxRiskScore);
                    foreach (var maintenance in maintenanceSql) {
                        if (!await TryExecuteSqlAsync(maintenance, candidate.SqlFingerprint, isRollback: false, cancellationToken)) {
                            continue;
                        }

                        EmitAuditLog($"{actionId}-maintenance", candidate, maintenance, rollbackSql: null, executed: true, dryRun: false, reason: "l3-maintenance");
                    }
                }
            }
        }

        private async Task ValidateAutonomousActionsAsync(SlowQueryAnalysisResult result, CancellationToken cancellationToken) {
            if (!_enableFullAutomation || !_enableAutoValidation || _pendingRollbackByFingerprint.Count == 0) {
                return;
            }

            var metricsByFingerprint = result.Metrics.ToDictionary(static metric => metric.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
            foreach (var rollback in _pendingRollbackByFingerprint.Values.ToArray()) {
                if (_analysisCycleCounter - rollback.CreatedCycle < _validationDelayCycles) {
                    continue;
                }

                if (!metricsByFingerprint.TryGetValue(rollback.Fingerprint, out var currentMetric)) {
                    continue;
                }

                var p95IncreasePercent = CalculateIncreasePercent(rollback.BaselineP95Milliseconds, currentMetric.P95Milliseconds);
                var p99IncreasePercent = CalculateIncreasePercent(rollback.BaselineP99Milliseconds, currentMetric.P99Milliseconds);
                var errorRateIncrease = currentMetric.ErrorRatePercent - rollback.BaselineErrorRatePercent;
                var timeoutRateIncrease = currentMetric.TimeoutRatePercent - rollback.BaselineTimeoutRatePercent;
                var deadlockIncrease = currentMetric.DeadlockCount - rollback.BaselineDeadlockCount;

                var regressed = (_validationP95IncreasePercent > 0m && p95IncreasePercent >= _validationP95IncreasePercent)
                    || (_validationP99IncreasePercent > 0m && p99IncreasePercent >= _validationP99IncreasePercent)
                    || (_validationErrorRateIncreasePercent > 0m && errorRateIncrease >= _validationErrorRateIncreasePercent)
                    || (_validationTimeoutRateIncreasePercent > 0m && timeoutRateIncrease >= _validationTimeoutRateIncreasePercent)
                    || (_validationDeadlockIncreaseCount > 0 && deadlockIncrease >= _validationDeadlockIncreaseCount);

                if (!regressed) {
                    _pendingRollbackByFingerprint.Remove(rollback.Fingerprint);
                    _logger.LogInformation(
                        "L3 自动验证通过：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}, P95Increase={P95Increase:F2}, P99Increase={P99Increase:F2}, ErrorRateIncrease={ErrorRateIncrease:F2}, TimeoutRateIncrease={TimeoutRateIncrease:F2}, DeadlockIncrease={DeadlockIncrease}",
                        _dialect.ProviderName,
                        rollback.Fingerprint,
                        rollback.TableKey,
                        p95IncreasePercent,
                        p99IncreasePercent,
                        errorRateIncrease,
                        timeoutRateIncrease,
                        deadlockIncrease);
                    continue;
                }

                _logger.LogWarning(
                    "L3 自动验证失败：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}, P95Increase={P95Increase:F2}, P99Increase={P99Increase:F2}, ErrorRateIncrease={ErrorRateIncrease:F2}, TimeoutRateIncrease={TimeoutRateIncrease:F2}, DeadlockIncrease={DeadlockIncrease}",
                    _dialect.ProviderName,
                    rollback.Fingerprint,
                    rollback.TableKey,
                    p95IncreasePercent,
                    p99IncreasePercent,
                    errorRateIncrease,
                    timeoutRateIncrease,
                    deadlockIncrease);

                if (_enableAutoRollback && await TryExecuteSqlAsync(rollback.RollbackSql, rollback.Fingerprint, isRollback: true, cancellationToken)) {
                    _logger.LogWarning(
                        "L3 自动验证回滚执行完成：Provider={Provider}, Fingerprint={Fingerprint}, ActionId={ActionId}, RollbackSql={RollbackSql}",
                        _dialect.ProviderName,
                        rollback.Fingerprint,
                        rollback.ActionId,
                        rollback.RollbackSql);
                }

                _pendingRollbackByFingerprint.Remove(rollback.Fingerprint);
            }
        }

        private async Task ExecuteSqlAsync(string sql, CancellationToken cancellationToken) {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();
            dbContext.Database.SetCommandTimeout(_actionExecutionTimeoutSeconds);
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

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

        private bool IsWhitelisted(string? schemaName, string tableName) {
            if (_whitelistedTables.Count == 0) {
                return true;
            }

            return _whitelistedTables.Contains(BuildTableKey(schemaName, tableName));
        }

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

            return PolicyDecision.Execute(riskScore, "l3-policy-approved");
        }

        private decimal CalculateRiskScore(string actionSql, SlowQueryMetric? metric, bool inPeakWindow) {
            decimal riskScore = 0m;
            if (IsDangerousAction(actionSql)) {
                riskScore += 0.45m;
            }

            if (inPeakWindow) {
                riskScore += 0.20m;
            }

            if (metric is not null) {
                if (metric.P99Milliseconds >= 1000d) {
                    riskScore += 0.15m;
                }

                if (metric.TimeoutRatePercent >= 1m) {
                    riskScore += 0.15m;
                }

                if (metric.ErrorRatePercent >= 1m) {
                    riskScore += 0.10m;
                }

                if (metric.DeadlockCount > 0) {
                    riskScore += 0.20m;
                }
            }

            return decimal.Clamp(riskScore, 0m, 1m);
        }

        private void UpdateAutonomousSignals(SlowQueryAnalysisResult result, DateTime cycleTime) {
            if (!_enableFullAutomation) {
                return;
            }

            foreach (var table in _tableHeatByTable.Keys.ToArray()) {
                var decayed = (int)Math.Floor(_tableHeatByTable[table] * 0.9d);
                if (decayed <= 0) {
                    _tableHeatByTable.Remove(table);
                    continue;
                }

                _tableHeatByTable[table] = decayed;
            }

            var metricsByFingerprint = result.Metrics.ToDictionary(static metric => metric.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
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

            foreach (var pair in tableSamples) {
                if (_tableHeatByTable.TryGetValue(pair.Key, out var currentHeat)) {
                    _tableHeatByTable[pair.Key] = currentHeat + pair.Value.Calls;
                }
                else {
                    _tableHeatByTable[pair.Key] = pair.Value.Calls;
                }

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
        }

        private void EmitCapacityForecast(string tableKey, Queue<TableCapacitySnapshot> snapshots) {
            if (snapshots.Count < 2) {
                return;
            }

            var ordered = snapshots.ToArray();
            var first = ordered[0];
            var last = ordered[^1];
            var elapsedSeconds = (last.CapturedLocalTime - first.CapturedLocalTime).TotalSeconds;
            if (elapsedSeconds <= 0d) {
                _logger.LogWarning(
                    "L3 查询体量趋势预测跳过：Provider={Provider}, Table={Table}, ElapsedSeconds={ElapsedSeconds:F0}, Reason={Reason}",
                    _dialect.ProviderName,
                    tableKey,
                    elapsedSeconds,
                    "non-positive elapsed time");
                return;
            }

            var minimumElapsedSeconds = _analyzeIntervalSeconds * 3d;
            if (elapsedSeconds < minimumElapsedSeconds) {
                _logger.LogInformation(
                    "L3 查询体量趋势预测跳过：Provider={Provider}, Table={Table}, ElapsedSeconds={ElapsedSeconds:F0}, MinimumElapsedSeconds={MinimumElapsedSeconds:F0}, Reason={Reason}",
                    _dialect.ProviderName,
                    tableKey,
                    elapsedSeconds,
                    minimumElapsedSeconds,
                    "insufficient observation window");
                return;
            }

            var rowGrowthPerDay = (last.AffectedRows - first.AffectedRows) / elapsedSeconds * TimeSpan.FromDays(1).TotalSeconds;
            if (rowGrowthPerDay <= 0d) {
                _logger.LogInformation(
                    "L3 查询体量趋势预测跳过：Provider={Provider}, Table={Table}, GrowthQueryVolumeRowsPerDay={GrowthRowsPerDay:F0}, Reason={Reason}",
                    _dialect.ProviderName,
                    tableKey,
                    rowGrowthPerDay,
                    "non-positive growth");
                return;
            }

            var projectedRows = last.AffectedRows + rowGrowthPerDay * _capacityProjectionDays;
            if (projectedRows < _capacityGrowthAlertRows) {
                return;
            }

            _logger.LogWarning(
                "L3 查询体量趋势告警：Provider={Provider}, Table={Table}, ProjectionDays={ProjectionDays}, ProjectedQueryVolume={ProjectedQueryVolume:F0}, GrowthQueryVolumePerDay={GrowthQueryVolumePerDay:F0}, ActionHint={Hint}",
                _dialect.ProviderName,
                tableKey,
                _capacityProjectionDays,
                projectedRows,
                rowGrowthPerDay,
                projectedRows >= _capacityHotLayeringRows ? "建议冷热分层 + 历史归档 + 索引重评估" : "建议优先检查索引与统计信息维护策略");
        }

        private static string BuildTableKey(string? schemaName, string tableName) {
            return string.IsNullOrWhiteSpace(schemaName)
                ? tableName.Trim().ToLowerInvariant()
                : $"{schemaName.Trim().ToLowerInvariant()}.{tableName.Trim().ToLowerInvariant()}";
        }

        private bool IsInPeakWindow(TimeSpan nowTime) {
            if (_peakStartTime == _peakEndTime) {
                return false;
            }

            if (_peakStartTime < _peakEndTime) {
                return nowTime >= _peakStartTime && nowTime < _peakEndTime;
            }

            return nowTime >= _peakStartTime || nowTime < _peakEndTime;
        }

        private bool IsDangerousAction(string actionSql) {
            if (string.IsNullOrWhiteSpace(actionSql)) {
                return false;
            }

            var normalized = TrimLeadingComments(actionSql);
            return DangerousDdlRegex.IsMatch(normalized);
        }

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

        private void AuditBaseline() {
            AuditBaselineItem("CommandTimeoutSeconds", _configuredCommandTimeoutSeconds, _baselineCommandTimeoutSeconds);
            AuditBaselineItem("MaxRetryCount", _configuredMaxRetryCount, _baselineMaxRetryCount);
            AuditBaselineItem("MaxRetryDelaySeconds", _configuredMaxRetryDelaySeconds, _baselineMaxRetryDelaySeconds);
        }

        private void AuditBaselineItem(string key, int configured, int baseline) {
            if (configured == baseline) {
                _logger.LogInformation("运行参数基线审计通过：Key={Key}, Current={Current}, Baseline={Baseline}", key, configured, baseline);
                return;
            }

            _logger.LogWarning("运行参数基线审计告警：Key={Key}, Current={Current}, Baseline={Baseline}", key, configured, baseline);
        }

        private static int GetPositiveIntOrDefault(IConfiguration configuration, string key, int fallback) {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        }

        private static int GetNonNegativeIntOrDefault(IConfiguration configuration, string key, int fallback) {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed >= 0 ? parsed : fallback;
        }

        private static decimal GetNonNegativeDecimalOrDefault(IConfiguration configuration, string key, decimal fallback) {
            var value = configuration[key];
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0m ? parsed : fallback;
        }

        private static decimal GetDecimalInRangeOrDefault(IConfiguration configuration, string key, decimal fallback, decimal min, decimal max) {
            var value = configuration[key];
            if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) {
                return fallback;
            }

            if (parsed < min || parsed > max) {
                return fallback;
            }

            return parsed;
        }

        private static bool GetBoolOrDefault(IConfiguration configuration, string key, bool fallback) {
            var value = configuration[key];
            return bool.TryParse(value, out var parsed) ? parsed : fallback;
        }

        private static TimeSpan GetTimeOfDayOrDefault(IConfiguration configuration, string key, TimeSpan fallback) {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value)) {
                return fallback;
            }

            if (!TimeSpan.TryParseExact(
                    value,
                    ["HH\\:mm\\:ss", "HH\\:mm"],
                    CultureInfo.InvariantCulture,
                    TimeSpanStyles.None,
                    out var parsed)) {
                throw new InvalidOperationException($"{key} 配置格式无效，仅支持 HH:mm:ss 或 HH:mm（本地时间语义）。");
            }

            if (parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1)) {
                throw new InvalidOperationException($"{key} 必须位于 [00:00:00, 24:00:00) 区间。");
            }

            return parsed;
        }

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
            int BaselineDeadlockCount);

        private sealed record TableCapacitySnapshot(
            DateTime CapturedLocalTime,
            long AffectedRows,
            int CallCount);

        private sealed record PolicyDecision(
            bool ShouldExecute,
            decimal RiskScore,
            string Reason) {
            public static PolicyDecision Execute(decimal riskScore, string reason) => new(true, riskScore, reason);
            public static PolicyDecision Skip(decimal riskScore, string reason) => new(false, riskScore, reason);
        }
    }
}
