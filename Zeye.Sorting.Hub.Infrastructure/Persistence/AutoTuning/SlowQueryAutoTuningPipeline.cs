using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>慢查询采集、分析与自动动作编排管道</summary>
    public sealed class SlowQueryAutoTuningPipeline {
        private const string AutoTuningConfigPrefix = "Persistence:AutoTuning";
        private const string AutoTuningMarker = "AUTO_TUNING";
        private const int MaxWhereColumns = 3;
        private const int MaxAlertTrackingStates = 2048;
        private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ParameterRegex = new(@"@[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex StringLiteralRegex = new(@"'[^']*'", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex FromRegex = new(@"\bfrom\s+(?:[`""\[]?([A-Za-z_][A-Za-z0-9_]*)[`""\]]?\s*\.\s*)?[`""\[]?([A-Za-z_][A-Za-z0-9_]*)[`""\]]?", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex UpdateRegex = new(@"\bupdate\s+(?:[`""\[]?([A-Za-z_][A-Za-z0-9_]*)[`""\]]?\s*\.\s*)?[`""\[]?([A-Za-z_][A-Za-z0-9_]*)[`""\]]?", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex WhereRegex = new(@"\bwhere\b(?<where>.+?)(\border\s+by\b|\bgroup\s+by\b|\blimit\b|;|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        private static readonly Regex WhereColumnRegex = new(@"(?:[A-Za-z_][A-Za-z0-9_]*\.)?[`""\[]?([A-Za-z_][A-Za-z0-9_]*)[`""\]]?\s*(=|>|<|>=|<=|like\b|in\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex SafeIdentifierRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly ConcurrentQueue<SlowQuerySample> _slowQueries = new();
        private readonly int _slowQueryThresholdMilliseconds;
        private readonly int _analysisBatchSize;
        private readonly int _triggerCount;
        private readonly int _maxSuggestionsPerCycle;
        private readonly int _maxQueueSize;
        private readonly int _aggregationTopN;
        private readonly int _alertDebounceMinCallCount;
        private readonly int _alertP99Milliseconds;
        private readonly decimal _alertTimeoutRatePercent;
        private readonly int _alertDeadlockCount;
        private readonly TimeSpan _alertDebounceWindow;
        private readonly int _alertConsecutiveWindows;
        private readonly int _alertRecoveryConsecutiveWindows;
        private readonly TimeSpan _dailyReportTime;
        private readonly IAutoTuningObservability _observability;
        private readonly object _queueSync = new();
        private readonly Dictionary<string, AlertTrackingState> _alertStates = new(StringComparer.OrdinalIgnoreCase);
        private int _droppedCount;
        private DateTime _nextDailyReportTime;

        /// <summary>初始化慢查询采集、分析和告警阈值配置。</summary>
        public SlowQueryAutoTuningPipeline(IConfiguration configuration, IAutoTuningObservability observability) {
            _observability = observability;
            _slowQueryThresholdMilliseconds = GetPositiveIntOrDefault(configuration, AutoTuningKey("SlowQueryThresholdMilliseconds"), 500);
            _analysisBatchSize = GetPositiveIntOrDefault(configuration, AutoTuningKey("AnalysisBatchSize"), 20);
            _triggerCount = GetPositiveIntOrDefault(configuration, AutoTuningKey("TriggerCount"), 3);
            _maxSuggestionsPerCycle = GetPositiveIntOrDefault(configuration, AutoTuningKey("MaxActionsPerCycle"), 3);
            _maxQueueSize = GetPositiveIntOrDefault(configuration, AutoTuningKey("MaxQueueSize"), 1000);
            _aggregationTopN = GetPositiveIntOrDefault(configuration, AutoTuningKey("AggregationTopN"), 10);
            _alertDebounceMinCallCount = GetPositiveIntOrDefault(configuration, AutoTuningKey("AlertDebounceMinCallCount"), _triggerCount);
            _alertP99Milliseconds = GetPositiveIntOrDefault(configuration, AutoTuningKey("AlertP99Milliseconds"), 500);
            _alertTimeoutRatePercent = GetDecimalOrDefault(configuration, AutoTuningKey("AlertTimeoutRatePercent"), 1m);
            _alertDeadlockCount = GetPositiveIntOrDefault(configuration, AutoTuningKey("AlertDeadlockCount"), 1);
            _alertDebounceWindow = GetPositiveSecondsAsTimeSpanOrDefault(configuration, AutoTuningKey("AlertDebounceWindowSeconds"), TimeSpan.FromMinutes(10));
            _alertConsecutiveWindows = GetPositiveIntOrDefault(configuration, AutoTuningKey("AlertConsecutiveWindows"), 1);
            _alertRecoveryConsecutiveWindows = GetPositiveIntOrDefault(configuration, AutoTuningKey("AlertRecoveryConsecutiveWindows"), 1);
            _dailyReportTime = GetTimeOfDayOrDefault(configuration, AutoTuningKey("DailyReportLocalTime"), new TimeSpan(2, 30, 0));
            _nextDailyReportTime = BuildNextDailyReportTime(DateTime.Now);
        }

        /// <summary>采集慢查询样本（含错误、超时、死锁标记）。</summary>
        public void Collect(string commandText, TimeSpan elapsed, int affectedRows = 0, Exception? exception = null) {
            if (string.IsNullOrWhiteSpace(commandText)) {
                return;
            }

            if (commandText.Contains(AutoTuningMarker, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            var isError = exception is not null;
            var elapsedMilliseconds = elapsed.TotalMilliseconds;
            var isSlow = elapsedMilliseconds >= _slowQueryThresholdMilliseconds;
            if (!isSlow && !isError) {
                return;
            }

            var isTimeout = IsTimeoutException(exception);
            var isDeadlock = IsDeadlockException(exception);
            var normalizedSql = NormalizeSql(commandText);
            var sqlFingerprint = BuildSqlFingerprint(normalizedSql);
            lock (_queueSync) {
                while (_slowQueries.Count >= _maxQueueSize && _slowQueries.TryDequeue(out _)) {
                    _droppedCount++;
                }

                _slowQueries.Enqueue(new SlowQuerySample(
                    commandText: commandText,
                    sqlFingerprint: sqlFingerprint,
                    elapsedMilliseconds: elapsedMilliseconds,
                    affectedRows: Math.Max(affectedRows, 0),
                    isError: isError,
                    isTimeout: isTimeout,
                    isDeadlock: isDeadlock,
                    occurredTime: DateTime.Now));
            }
        }

        /// <summary>分析窗口内样本并生成指标、建议与告警。</summary>
        public SlowQueryAnalysisResult Analyze(IDatabaseDialect dialect) {
            var now = DateTime.Now;
            var shouldEmitDailyReport = ShouldEmitDailyReport(now);
            var window = DequeueWindow();
            _observability.EmitMetric("autotuning.analysis.window_size", window.Count);
            if (window.Count == 0) {
                return SlowQueryAnalysisResult.Empty with {
                    GeneratedTime = now,
                    DroppedSamples = GetDroppedCount(),
                    ShouldEmitDailyReport = shouldEmitDailyReport
                };
            }

            var groups = window
                .GroupBy(static q => q.SqlFingerprint)
                .Select(group => BuildMetric(group.Key, group))
                .OrderByDescending(static x => x.P99Milliseconds)
                .ThenByDescending(static x => x.P95Milliseconds)
                .ThenByDescending(static x => x.CallCount)
                .Take(_aggregationTopN)
                .ToList();

            var tuningCandidates = BuildTuningCandidates(dialect, groups);
            var suggestionInsights = BuildReadOnlySuggestions(tuningCandidates, groups);
            var alerts = BuildAlerts(groups, now);
            foreach (var notification in alerts) {
                _observability.EmitEvent(
                    "autotuning.alert.notification",
                    notification.IsRecovery ? LogLevel.Information : LogLevel.Warning,
                    notification.Message,
                    new Dictionary<string, string> {
                        ["fingerprint"] = notification.SqlFingerprint,
                        ["type"] = notification.AlertType,
                        ["recovery"] = notification.IsRecovery ? "true" : "false"
                    });
            }
            var alertMessages = alerts
                .Where(static notification => !notification.IsRecovery)
                .Select(static notification => notification.Message)
                .ToList();
            var recoveryMessages = alerts
                .Where(static notification => notification.IsRecovery)
                .Select(static notification => notification.Message)
                .ToList();

            return new SlowQueryAnalysisResult(
                GeneratedTime: now,
                DroppedSamples: GetDroppedCount(),
                Metrics: groups,
                TuningCandidates: tuningCandidates,
                ReadOnlySuggestions: suggestionInsights.Select(static insight => insight.SuggestionSql).ToList(),
                SuggestionInsights: suggestionInsights,
                Alerts: alertMessages,
                RecoveryNotifications: recoveryMessages,
                AlertNotifications: alerts,
                ShouldEmitDailyReport: shouldEmitDailyReport);
        }

        /// <summary>从队列中取出单次分析窗口样本。</summary>
        private List<SlowQuerySample> DequeueWindow() {
            var result = new List<SlowQuerySample>(_analysisBatchSize);
            lock (_queueSync) {
                while (result.Count < _analysisBatchSize && _slowQueries.TryDequeue(out var sample)) {
                    result.Add(sample);
                }
            }

            return result;
        }

        /// <summary>获取当前累计丢弃样本数量。</summary>
        private int GetDroppedCount() {
            lock (_queueSync) {
                return _droppedCount;
            }
        }

        /// <summary>归一化 SQL 文本，消除参数与格式噪音。</summary>
        private static string NormalizeSql(string sql) {
            var withoutStringLiterals = StringLiteralRegex.Replace(sql, "?");
            var withoutParameters = ParameterRegex.Replace(withoutStringLiterals, "?");
            var normalized = MultiWhitespaceRegex.Replace(withoutParameters, " ").Trim();
            return normalized.Length <= 512 ? normalized : normalized[..512];
        }

        /// <summary>基于聚合结果生成自动调优候选动作。</summary>
        private IReadOnlyList<SlowQueryTuningCandidate> BuildTuningCandidates(IDatabaseDialect dialect, IReadOnlyList<SlowQueryMetric> groups) {
            var candidates = new List<SlowQueryTuningCandidate>();
            var existedSuggestions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var actionCount = 0;

            foreach (var metric in groups) {
                if (metric.CallCount < _triggerCount) {
                    continue;
                }

                if (!TryExtractTableAndColumns(metric.SampleSql, out var schemaName, out var tableName, out var whereColumns)) {
                    continue;
                }

                var dialectActions = dialect.BuildAutomaticTuningSql(schemaName, tableName, whereColumns);
                var uniqueActions = new List<string>();
                foreach (var action in dialectActions) {
                    if (string.IsNullOrWhiteSpace(action)) {
                        continue;
                    }

                    if (actionCount >= _maxSuggestionsPerCycle) {
                        break;
                    }

                    if (existedSuggestions.Add(action)) {
                        uniqueActions.Add(action);
                        actionCount++;
                    }
                }

                if (uniqueActions.Count > 0) {
                    candidates.Add(new SlowQueryTuningCandidate(
                        SqlFingerprint: metric.SqlFingerprint,
                        SchemaName: schemaName,
                        TableName: tableName,
                        WhereColumns: whereColumns,
                        SuggestedActions: uniqueActions));
                }

                if (actionCount >= _maxSuggestionsPerCycle) {
                    break;
                }
            }

            return candidates;
        }

        /// <summary>将候选动作转换为只读建议文案。</summary>
        private static IReadOnlyList<SlowQuerySuggestionInsight> BuildReadOnlySuggestions(
            IReadOnlyList<SlowQueryTuningCandidate> candidates,
            IReadOnlyList<SlowQueryMetric> metrics) {
            var metricsByFingerprint = metrics.ToDictionary(static metric => metric.SqlFingerprint, StringComparer.OrdinalIgnoreCase);
            var suggestions = new List<SlowQuerySuggestionInsight>();
            foreach (var candidate in candidates) {
                metricsByFingerprint.TryGetValue(candidate.SqlFingerprint, out var metric);
                var (riskLevel, confidence, reason) = BuildSuggestionDiagnostics(metric);
                foreach (var action in candidate.SuggestedActions) {
                    suggestions.Add(new SlowQuerySuggestionInsight(
                        SqlFingerprint: candidate.SqlFingerprint,
                        SuggestionSql: $"/*{AutoTuningMarker}_READ_ONLY*/ {action}",
                        Reason: reason,
                        RiskLevel: riskLevel,
                        Confidence: confidence));
                }
            }

            return suggestions;
        }

        /// <summary>根据阈值规则生成告警列表。</summary>
        private IReadOnlyList<SlowQueryAlertNotification> BuildAlerts(IReadOnlyList<SlowQueryMetric> groups, DateTime now) {
            var alerts = new List<SlowQueryAlertNotification>();
            var observedAlertKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var metric in groups) {
                if (metric.CallCount < _alertDebounceMinCallCount) {
                    continue;
                }

                if (metric.P99Milliseconds > _alertP99Milliseconds) {
                    var alertKey = BuildAlertKey(metric.SqlFingerprint, "P99");
                    observedAlertKeys.Add(alertKey);
                    TryTrackAlert(
                        now,
                        metric,
                        alertKey,
                        "P99",
                        $"P99 超阈值：Fingerprint={metric.SqlFingerprint}, Calls={metric.CallCount}, P99Ms={metric.P99Milliseconds:F2}, ThresholdMs={_alertP99Milliseconds}",
                        alerts);
                }

                if (metric.TimeoutRatePercent > _alertTimeoutRatePercent) {
                    var alertKey = BuildAlertKey(metric.SqlFingerprint, "TIMEOUT");
                    observedAlertKeys.Add(alertKey);
                    TryTrackAlert(
                        now,
                        metric,
                        alertKey,
                        "TIMEOUT",
                        $"超时率超阈值：Fingerprint={metric.SqlFingerprint}, Calls={metric.CallCount}, TimeoutRatePercent={metric.TimeoutRatePercent:F2}, ThresholdPercent={_alertTimeoutRatePercent:F2}",
                        alerts);
                }

                if (metric.DeadlockCount >= _alertDeadlockCount) {
                    var alertKey = BuildAlertKey(metric.SqlFingerprint, "DEADLOCK");
                    observedAlertKeys.Add(alertKey);
                    TryTrackAlert(
                        now,
                        metric,
                        alertKey,
                        "DEADLOCK",
                        $"死锁次数超阈值：Fingerprint={metric.SqlFingerprint}, Calls={metric.CallCount}, DeadlockCount={metric.DeadlockCount}, Threshold={_alertDeadlockCount}",
                        alerts);
                }
            }

            TryTrackRecoveries(now, observedAlertKeys, alerts);
            return alerts;
        }

        private void TryTrackAlert(
            DateTime now,
            SlowQueryMetric metric,
            string alertKey,
            string alertType,
            string message,
            List<SlowQueryAlertNotification> notifications) {
            if (!_alertStates.TryGetValue(alertKey, out var state)) {
                state = new AlertTrackingState();
                _alertStates[alertKey] = state;
            }

            state.LastSeenTime = now;
            state.ConsecutiveTriggeredWindows++;
            state.ConsecutiveRecoveredWindows = 0;
            var hasReachedConsecutiveThreshold = state.ConsecutiveTriggeredWindows >= _alertConsecutiveWindows;
            if (!hasReachedConsecutiveThreshold) {
                return;
            }

            var shouldSuppressByWindow = state.LastNotifiedTime.HasValue && now - state.LastNotifiedTime.Value < _alertDebounceWindow;
            if (shouldSuppressByWindow) {
                state.IsActive = true;
                return;
            }

            state.IsActive = true;
            state.LastNotifiedTime = now;
            notifications.Add(new SlowQueryAlertNotification(
                SqlFingerprint: metric.SqlFingerprint,
                AlertType: alertType,
                Message: message,
                IsRecovery: false,
                TriggeredTime: now));
        }

        private void TryTrackRecoveries(
            DateTime now,
            HashSet<string> observedAlertKeys,
            List<SlowQueryAlertNotification> notifications) {
            foreach (var pair in _alertStates.ToArray()) {
                var alertKey = pair.Key;
                var state = pair.Value;
                if (observedAlertKeys.Contains(alertKey)) {
                    continue;
                }

                state.ConsecutiveTriggeredWindows = 0;
                if (!state.IsActive) {
                    state.ConsecutiveRecoveredWindows = 0;
                    if (now - state.LastSeenTime >= _alertDebounceWindow) {
                        _alertStates.Remove(alertKey);
                    }
                    continue;
                }

                state.ConsecutiveRecoveredWindows++;
                if (state.ConsecutiveRecoveredWindows < _alertRecoveryConsecutiveWindows) {
                    continue;
                }

                state.IsActive = false;
                state.ConsecutiveRecoveredWindows = 0;
                notifications.Add(new SlowQueryAlertNotification(
                    SqlFingerprint: TryExtractFingerprintFromAlertKey(alertKey),
                    AlertType: TryExtractTypeFromAlertKey(alertKey),
                    Message: $"告警恢复：{alertKey}",
                    IsRecovery: true,
                    TriggeredTime: now));
            }

            PruneAlertStateCapacity();
        }

        private void PruneAlertStateCapacity() {
            var overflow = _alertStates.Count - MaxAlertTrackingStates;
            if (overflow <= 0) {
                return;
            }

            var keysToRemove = _alertStates
                .OrderBy(static pair => pair.Value.IsActive)
                .ThenBy(static pair => pair.Value.LastSeenTime)
                .Select(static pair => pair.Key)
                .Take(overflow)
                .ToList();
            foreach (var key in keysToRemove) {
                _alertStates.Remove(key);
            }
        }

        /// <summary>判断当前是否到达日报输出时点。</summary>
        private bool ShouldEmitDailyReport(DateTime now) {
            lock (_queueSync) {
                if (now < _nextDailyReportTime) {
                    return false;
                }

                _nextDailyReportTime = BuildNextDailyReportTime(now);
                return true;
            }
        }

        /// <summary>计算下一次日报触发时间。</summary>
        private DateTime BuildNextDailyReportTime(DateTime now) {
            if (_dailyReportTime < TimeSpan.Zero || _dailyReportTime >= TimeSpan.FromDays(1)) {
                throw new InvalidOperationException("Persistence:AutoTuning:DailyReportLocalTime 必须位于 [00:00:00, 24:00:00) 区间。");
            }

            var next = now.Date.Add(_dailyReportTime);
            if (next <= now) {
                next = next.AddDays(1);
            }

            return next;
        }

        /// <summary>将一组样本聚合为单条慢查询指标。</summary>
        private static SlowQueryMetric BuildMetric(string fingerprint, IGrouping<string, SlowQuerySample> group) {
            var samples = group.ToList();
            var callCount = samples.Count;
            var elapsedValues = samples
                .Select(static s => s.ElapsedMilliseconds)
                .OrderBy(static x => x)
                .ToArray();
            var totalRows = samples.Sum(static s => s.AffectedRows);
            var errorCount = samples.Count(static s => s.IsError);
            var timeoutCount = samples.Count(static s => s.IsTimeout);
            var deadlockCount = samples.Count(static s => s.IsDeadlock);
            var sampleSql = samples[0].CommandText;

            return new SlowQueryMetric(
                SqlFingerprint: fingerprint,
                SampleSql: sampleSql,
                CallCount: callCount,
                TotalAffectedRows: totalRows,
                ErrorRatePercent: errorCount * 100m / Math.Max(callCount, 1),
                TimeoutRatePercent: timeoutCount * 100m / Math.Max(callCount, 1),
                DeadlockCount: deadlockCount,
                P95Milliseconds: CalculatePercentile(elapsedValues, 95),
                P99Milliseconds: CalculatePercentile(elapsedValues, 99),
                MaxMilliseconds: elapsedValues[^1],
                LockWaitCount: null);
        }

        /// <summary>计算指定分位点值（输入必须为升序数组）。</summary>
        private static double CalculatePercentile(IReadOnlyList<double> sorted, int percentile) {
            if (sorted.Count == 0) {
                return 0d;
            }

            var rank = (int)Math.Ceiling(percentile / 100d * sorted.Count);
            var index = Math.Clamp(rank - 1, 0, sorted.Count - 1);
            return sorted[index];
        }

        /// <summary>计算标准化 SQL 指纹。</summary>
        private static string BuildSqlFingerprint(string normalizedSql) {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSql));
            return Convert.ToHexString(hashBytes[..8]).ToLowerInvariant();
        }

        /// <summary>判断异常是否属于超时类。</summary>
        private static bool IsTimeoutException(Exception? exception) {
            if (exception is null) {
                return false;
            }

            if (exception is TimeoutException) {
                return true;
            }

            return DatabaseProviderExceptionHelper.TryGetProviderErrorNumber(exception, out var number)
                && (number == -2 || number == 3024);
        }

        /// <summary>判断异常是否属于死锁类。</summary>
        private static bool IsDeadlockException(Exception? exception) {
            if (exception is null) {
                return false;
            }

            return DatabaseProviderExceptionHelper.TryGetProviderErrorNumber(exception, out var number)
                && (number == 1205 || number == 1213);
        }

        /// <summary>从 SQL 中提取 schema/table 与 where 列候选。</summary>
        private static bool TryExtractTableAndColumns(string sql, out string? schemaName, out string tableName, out IReadOnlyList<string> whereColumns) {
            schemaName = null;
            tableName = string.Empty;
            whereColumns = Array.Empty<string>();

            var tableMatch = FromRegex.Match(sql);
            if (!tableMatch.Success) {
                tableMatch = UpdateRegex.Match(sql);
            }

            if (!tableMatch.Success) {
                return false;
            }

            var candidateSchema = tableMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(candidateSchema) && !SafeIdentifierRegex.IsMatch(candidateSchema)) {
                return false;
            }

            var candidateTable = tableMatch.Groups[2].Value.Trim();
            if (!SafeIdentifierRegex.IsMatch(candidateTable)) {
                return false;
            }

            var whereMatch = WhereRegex.Match(sql);
            if (!whereMatch.Success) {
                return false;
            }

            var columns = new List<string>(MaxWhereColumns);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match columnMatch in WhereColumnRegex.Matches(whereMatch.Groups["where"].Value)) {
                var column = columnMatch.Groups[1].Value.Trim();
                if (!SafeIdentifierRegex.IsMatch(column)) {
                    continue;
                }

                if (seen.Add(column)) {
                    columns.Add(column);
                }

                if (columns.Count >= MaxWhereColumns) {
                    break;
                }
            }

            if (columns.Count == 0) {
                return false;
            }

            schemaName = string.IsNullOrWhiteSpace(candidateSchema) ? null : candidateSchema;
            tableName = candidateTable;
            whereColumns = columns;
            return true;
        }

        /// <summary>读取正整数配置，非法值回退默认值。</summary>
        private static int GetPositiveIntOrDefault(IConfiguration configuration, string key, int fallback) {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        }

        /// <summary>读取非负小数配置，非法值回退默认值。</summary>
        private static decimal GetDecimalOrDefault(IConfiguration configuration, string key, decimal fallback) {
            var value = configuration[key];
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0m ? parsed : fallback;
        }

        private static TimeSpan GetPositiveSecondsAsTimeSpanOrDefault(IConfiguration configuration, string key, TimeSpan fallback) {
            var value = configuration[key];
            if (!int.TryParse(value, out var seconds) || seconds <= 0) {
                return fallback;
            }

            return TimeSpan.FromSeconds(seconds);
        }

        /// <summary>读取本地时间（HH:mm 或 HH:mm:ss）配置。</summary>
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

        /// <summary>生成 AutoTuning 配置全路径键名。</summary>
        private static string AutoTuningKey(string suffix) => $"{AutoTuningConfigPrefix}:{suffix}";

        private static (string RiskLevel, decimal Confidence, string Reason) BuildSuggestionDiagnostics(SlowQueryMetric? metric) {
            if (metric is null) {
                return ("medium", 0.55m, "缺少完整观测样本，建议先在只读模式下验证");
            }

            var riskLevel = "low";
            if (metric.DeadlockCount > 0 || metric.TimeoutRatePercent >= 2m || metric.ErrorRatePercent >= 2m) {
                riskLevel = "high";
            } else if (metric.TimeoutRatePercent >= 0.5m || metric.ErrorRatePercent >= 0.5m || metric.P99Milliseconds >= 1000d) {
                riskLevel = "medium";
            }

            var confidence = Math.Min(0.95m, 0.45m + metric.CallCount / 100m);
            var reason = $"基于慢 SQL 聚合样本（Calls={metric.CallCount}, P99Ms={metric.P99Milliseconds:F2}, TimeoutRate={metric.TimeoutRatePercent:F2}%）生成建议";
            return (riskLevel, confidence, reason);
        }

        private static string BuildAlertKey(string fingerprint, string type) => $"{fingerprint}|{type}";

        private static string TryExtractFingerprintFromAlertKey(string alertKey) {
            var separatorIndex = alertKey.IndexOf('|');
            return separatorIndex < 0 ? alertKey : alertKey[..separatorIndex];
        }

        private static string TryExtractTypeFromAlertKey(string alertKey) {
            var separatorIndex = alertKey.IndexOf('|');
            return separatorIndex < 0 ? "UNKNOWN" : alertKey[(separatorIndex + 1)..];
        }

        private sealed class AlertTrackingState {
            public int ConsecutiveTriggeredWindows { get; set; }
            public int ConsecutiveRecoveredWindows { get; set; }
            public bool IsActive { get; set; }
            public DateTime? LastNotifiedTime { get; set; }
            public DateTime LastSeenTime { get; set; }
        }
    }

    public sealed record SlowQueryMetric(
        string SqlFingerprint,
        string SampleSql,
        int CallCount,
        int TotalAffectedRows,
        decimal ErrorRatePercent,
        decimal TimeoutRatePercent,
        int DeadlockCount,
        double P95Milliseconds,
        double P99Milliseconds,
        double MaxMilliseconds,
        int? LockWaitCount);

    public sealed record SlowQuerySuggestionInsight(
        string SqlFingerprint,
        string SuggestionSql,
        string Reason,
        string RiskLevel,
        decimal Confidence);

    public sealed record SlowQueryAlertNotification(
        string SqlFingerprint,
        string AlertType,
        string Message,
        bool IsRecovery,
        DateTime TriggeredTime);

    public sealed record SlowQueryAnalysisResult(
        DateTime GeneratedTime,
        int DroppedSamples,
        IReadOnlyList<SlowQueryMetric> Metrics,
        IReadOnlyList<SlowQueryTuningCandidate> TuningCandidates,
        IReadOnlyList<string> ReadOnlySuggestions,
        IReadOnlyList<SlowQuerySuggestionInsight> SuggestionInsights,
        IReadOnlyList<string> Alerts,
        IReadOnlyList<string> RecoveryNotifications,
        IReadOnlyList<SlowQueryAlertNotification> AlertNotifications,
        bool ShouldEmitDailyReport) {
        public static SlowQueryAnalysisResult Empty => new(
            GeneratedTime: DateTime.Now,
            DroppedSamples: 0,
            Metrics: Array.Empty<SlowQueryMetric>(),
            TuningCandidates: Array.Empty<SlowQueryTuningCandidate>(),
            ReadOnlySuggestions: Array.Empty<string>(),
            Alerts: Array.Empty<string>(),
            SuggestionInsights: Array.Empty<SlowQuerySuggestionInsight>(),
            RecoveryNotifications: Array.Empty<string>(),
            AlertNotifications: Array.Empty<SlowQueryAlertNotification>(),
            ShouldEmitDailyReport: false);
    }

    public sealed record SlowQueryTuningCandidate(
        string SqlFingerprint,
        string? SchemaName,
        string TableName,
        IReadOnlyList<string> WhereColumns,
        IReadOnlyList<string> SuggestedActions);
}
