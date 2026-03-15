using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Host.HostedServices {

    /// <summary>
    /// 数据库自动调谐后台服务：慢查询分析 + 可控执行 + 审计日志
    /// </summary>
    public sealed class DatabaseAutoTuningHostedService : BackgroundService {
        private const int MaxTrackedFingerprintCount = 1000;
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
        private readonly bool _enableActionExecution;
        private readonly bool _enableDangerousActionIsolator;
        private readonly bool _enableDryRun;
        private readonly int _maxExecuteActionsPerCycle;
        private readonly int _actionExecutionTimeoutSeconds;
        private readonly bool _skipExecutionDuringPeak;
        private readonly TimeSpan _peakStartTime;
        private readonly TimeSpan _peakEndTime;
        private readonly decimal _regressionP99IncreasePercent;
        private readonly decimal _regressionTimeoutRateIncreasePercent;
        private readonly bool _enableAutoRollback;
        private readonly HashSet<string> _whitelistedTables;
        private readonly Dictionary<string, SlowQueryMetric> _lastMetricByFingerprint = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PendingRollbackAction> _pendingRollbackByFingerprint = new(StringComparer.OrdinalIgnoreCase);
        private readonly int _baselineCommandTimeoutSeconds;
        private readonly int _baselineMaxRetryCount;
        private readonly int _baselineMaxRetryDelaySeconds;
        private readonly int _configuredCommandTimeoutSeconds;
        private readonly int _configuredMaxRetryCount;
        private readonly int _configuredMaxRetryDelaySeconds;

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
            _enableActionExecution = GetBoolOrDefault(configuration, "Persistence:AutoTuning:L2:EnableActionExecution", false);
            _enableDangerousActionIsolator = GetBoolOrDefault(configuration, "Persistence:AutoTuning:L2:EnableDangerousActionIsolator", true);
            _enableDryRun = GetBoolOrDefault(configuration, "Persistence:AutoTuning:L2:EnableDryRun", true);
            _maxExecuteActionsPerCycle = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:L2:MaxExecuteActionsPerCycle", 2);
            _actionExecutionTimeoutSeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:L2:ActionExecutionTimeoutSeconds", 60);
            _skipExecutionDuringPeak = GetBoolOrDefault(configuration, "Persistence:AutoTuning:L2:SkipExecutionDuringPeak", true);
            _peakStartTime = GetTimeOfDayOrDefault(configuration, "Persistence:AutoTuning:L2:PeakStartLocalTime", new TimeSpan(8, 0, 0));
            _peakEndTime = GetTimeOfDayOrDefault(configuration, "Persistence:AutoTuning:L2:PeakEndLocalTime", new TimeSpan(21, 0, 0));
            _regressionP99IncreasePercent = GetNonNegativeDecimalOrDefault(configuration, "Persistence:AutoTuning:L2:RegressionP99IncreasePercent", 30m);
            _regressionTimeoutRateIncreasePercent = GetNonNegativeDecimalOrDefault(configuration, "Persistence:AutoTuning:L2:RegressionTimeoutRateIncreasePercent", 1m);
            _enableAutoRollback = GetBoolOrDefault(configuration, "Persistence:AutoTuning:L2:EnableAutoRollback", true);
            _whitelistedTables = LoadWhitelistedTables(configuration.GetSection("Persistence:AutoTuning:L2:WhitelistedTables"));
            if (_enableActionExecution && _whitelistedTables.Count == 0) {
                _logger.LogWarning("L2 自动调优执行已启用但白名单为空：当前将阻止所有表执行自动动作。");
            }
            _baselineCommandTimeoutSeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:BaselineCommandTimeoutSeconds", 30);
            _baselineMaxRetryCount = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:BaselineMaxRetryCount", 5);
            _baselineMaxRetryDelaySeconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:BaselineMaxRetryDelaySeconds", 10);
            _configuredCommandTimeoutSeconds = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:CommandTimeoutSeconds", 30);
            _configuredMaxRetryCount = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:MaxRetryCount", 5);
            _configuredMaxRetryDelaySeconds = GetPositiveIntOrDefault(configuration, "Persistence:PerformanceTuning:MaxRetryDelaySeconds", 10);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            AuditBaseline();

            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(_analyzeIntervalSeconds), stoppingToken);

                var result = _pipeline.Analyze(_dialect);
                if (result.Metrics.Count == 0) {
                    continue;
                }

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

                await ExecuteAutoTuningActionsAsync(result, stoppingToken);
                await DetectRegressionAndAutoRollbackAsync(result, stoppingToken);
                SaveLastMetrics(result.Metrics);

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

            foreach (var metric in result.Metrics) {
                _logger.LogInformation(
                    "每日慢 SQL Top：Fingerprint={Fingerprint}, Calls={Calls}, P95Ms={P95Ms}, P99Ms={P99Ms}, TimeoutRatePercent={TimeoutRatePercent}, DeadlockCount={DeadlockCount}",
                    metric.SqlFingerprint,
                    metric.CallCount,
                    metric.P95Milliseconds,
                    metric.P99Milliseconds,
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
            if (!_enableActionExecution) {
                return;
            }

            if (_skipExecutionDuringPeak && IsInPeakWindow(DateTime.Now.TimeOfDay)) {
                _logger.LogInformation("自动调优执行已跳过：当前处于高峰时段，仅采集不变更。Provider={Provider}", _dialect.ProviderName);
                return;
            }

            var executedCount = 0;
            foreach (var candidate in result.TuningCandidates) {
                if (executedCount >= _maxExecuteActionsPerCycle) {
                    break;
                }

                if (!IsWhitelisted(candidate.SchemaName, candidate.TableName)) {
                    _logger.LogInformation(
                        "自动调优白名单拦截：Provider={Provider}, Fingerprint={Fingerprint}, Table={Table}",
                        _dialect.ProviderName,
                        candidate.SqlFingerprint,
                        BuildTableKey(candidate.SchemaName, candidate.TableName));
                    continue;
                }

                foreach (var actionSql in candidate.SuggestedActions) {
                    if (executedCount >= _maxExecuteActionsPerCycle) {
                        break;
                    }

                    var rollbackSql = BuildRollbackSql(actionSql);
                    var actionId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                    var dangerous = IsDangerousAction(actionSql);
                    var shouldDryRun = _enableDryRun || (_enableDangerousActionIsolator && dangerous);
                    if (shouldDryRun) {
                        EmitAuditLog(actionId, candidate, actionSql, rollbackSql, executed: false, dryRun: true, reason: "dry-run");
                        continue;
                    }

                    if (!await TryExecuteSqlAsync(actionSql, candidate.SqlFingerprint, isRollback: false, cancellationToken)) {
                        continue;
                    }

                    EmitAuditLog(actionId, candidate, actionSql, rollbackSql, executed: true, dryRun: false, reason: "executed");
                    executedCount++;

                    if (!string.IsNullOrWhiteSpace(rollbackSql)) {
                        _pendingRollbackByFingerprint[candidate.SqlFingerprint] = new PendingRollbackAction(
                            ActionId: actionId,
                            Fingerprint: candidate.SqlFingerprint,
                            RollbackSql: rollbackSql,
                            CreatedTime: DateTime.Now);
                    }
                }
            }
        }

        private async Task DetectRegressionAndAutoRollbackAsync(SlowQueryAnalysisResult result, CancellationToken cancellationToken) {
            foreach (var metric in result.Metrics) {
                if (!_lastMetricByFingerprint.TryGetValue(metric.SqlFingerprint, out var previous)) {
                    continue;
                }

                var p99IncreasePercent = CalculateIncreasePercent(previous.P99Milliseconds, metric.P99Milliseconds);
                var timeoutRateIncrease = metric.TimeoutRatePercent - previous.TimeoutRatePercent;
                var hasP99Regression = _regressionP99IncreasePercent > 0m && p99IncreasePercent >= _regressionP99IncreasePercent;
                var hasTimeoutRegression = _regressionTimeoutRateIncreasePercent > 0m && timeoutRateIncrease >= _regressionTimeoutRateIncreasePercent;
                if (!hasP99Regression && !hasTimeoutRegression) {
                    continue;
                }

                _logger.LogWarning(
                    "执行计划/统计信息疑似回退：Provider={Provider}, Fingerprint={Fingerprint}, PrevP99Ms={PrevP99Ms}, CurP99Ms={CurP99Ms}, P99IncreasePercent={P99IncreasePercent}, PrevTimeoutRate={PrevTimeoutRate}, CurTimeoutRate={CurTimeoutRate}",
                    _dialect.ProviderName,
                    metric.SqlFingerprint,
                    previous.P99Milliseconds,
                    metric.P99Milliseconds,
                    p99IncreasePercent,
                    previous.TimeoutRatePercent,
                    metric.TimeoutRatePercent);

                if (!_enableAutoRollback || !_pendingRollbackByFingerprint.TryGetValue(metric.SqlFingerprint, out var rollback)) {
                    continue;
                }

                if (!await TryExecuteSqlAsync(rollback.RollbackSql, metric.SqlFingerprint, isRollback: true, cancellationToken)) {
                    continue;
                }

                _pendingRollbackByFingerprint.Remove(metric.SqlFingerprint);
                _logger.LogWarning(
                    "自动调优回滚已执行：Provider={Provider}, Fingerprint={Fingerprint}, ActionId={ActionId}, RollbackSql={RollbackSql}",
                    _dialect.ProviderName,
                    metric.SqlFingerprint,
                    rollback.ActionId,
                    rollback.RollbackSql);
            }
        }

        private void SaveLastMetrics(IReadOnlyList<SlowQueryMetric> metrics) {
            foreach (var metric in metrics) {
                _lastMetricByFingerprint[metric.SqlFingerprint] = metric;
            }

            PruneTrackingState();
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
                return false;
            }

            return _whitelistedTables.Contains(BuildTableKey(schemaName, tableName));
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

        private static decimal GetNonNegativeDecimalOrDefault(IConfiguration configuration, string key, decimal fallback) {
            var value = configuration[key];
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0m ? parsed : fallback;
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
            var overflowCount = _lastMetricByFingerprint.Count - MaxTrackedFingerprintCount;
            if (overflowCount > 0) {
                var removeKeys = _pendingRollbackByFingerprint
                    .Where(pair => _lastMetricByFingerprint.ContainsKey(pair.Key))
                    .OrderBy(static pair => pair.Value.CreatedTime)
                    .Select(static pair => pair.Key)
                    .Take(overflowCount)
                    .ToList();
                var removeKeySet = new HashSet<string>(removeKeys, StringComparer.OrdinalIgnoreCase);

                if (removeKeys.Count < overflowCount) {
                    var remaining = overflowCount - removeKeys.Count;
                    var fallbackKeys = _lastMetricByFingerprint.Keys
                        .OrderBy(static key => key, StringComparer.Ordinal)
                        .Where(key => !removeKeySet.Contains(key))
                        .Take(remaining);
                    removeKeys.AddRange(fallbackKeys);
                }

                foreach (var key in removeKeys) {
                    _lastMetricByFingerprint.Remove(key);
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
        }

        private sealed record PendingRollbackAction(
            string ActionId,
            string Fingerprint,
            string RollbackSql,
            DateTime CreatedTime);
    }
}
