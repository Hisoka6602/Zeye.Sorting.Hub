using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>慢查询采集、分析与自动动作编排管道</summary>
    public sealed class SlowQueryAutoTuningPipeline {
        /// <summary>
        /// 自动调优标记前缀，用于识别由自动调优链路生成的对象。
        /// </summary>
        private const string AutoTuningMarker = "AUTO_TUNING";
        /// <summary>
        /// 单条候选索引参与 WHERE 条件的最大列数上限。
        /// </summary>
        private const int MaxWhereColumns = 3;
        /// <summary>
        /// 告警状态跟踪上限，防止状态字典无限增长。
        /// </summary>
        private const int MaxAlertTrackingStates = 2048;
        /// <summary>
        /// 多空白折叠正则，用于 SQL 归一化。
        /// </summary>
        private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// 参数占位符正则（@Param）。
        /// </summary>
        private static readonly Regex ParameterRegex = new(@"@[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// 字符串字面量正则（用于脱敏归一化）。
        /// </summary>
        private static readonly Regex StringLiteralRegex = new(@"'[^']*'", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// FROM 子句表名提取正则。
        /// </summary>
        private static readonly Regex FromRegex = new(@"\bfrom\s+(?:[`""\[]?([A-Za-z_][A-Za-z0-9_]*)[`""\]]?\s*\.\s*)?[`""\[]?([A-Za-z_][A-Za-z0-9_]*)[`""\]]?", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// UPDATE 语句表名提取正则。
        /// </summary>
        private static readonly Regex UpdateRegex = new(@"\bupdate\s+(?:[`""\[]?([A-Za-z_][A-Za-z0-9_]*)[`""\]]?\s*\.\s*)?[`""\[]?([A-Za-z_][A-Za-z0-9_]*)[`""\]]?", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// WHERE 子句捕获正则。
        /// </summary>
        private static readonly Regex WhereRegex = new(@"\bwhere\b(?<where>.+?)(\border\s+by\b|\bgroup\s+by\b|\blimit\b|;|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);
        /// <summary>
        /// WHERE 列名与操作符提取正则。
        /// </summary>
        private static readonly Regex WhereColumnRegex = new(@"(?:[A-Za-z_][A-Za-z0-9_]*\.)?[`""\[]?([A-Za-z_][A-Za-z0-9_]*)[`""\]]?\s*(=|>|<|>=|<=|like\b|in\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// 安全标识符校验正则。
        /// </summary>
        private static readonly Regex SafeIdentifierRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        /// <summary>
        /// 慢查询样本队列，用于聚合分析。
        /// </summary>
        private readonly Queue<SlowQuerySample> _slowQueries = new();
        /// <summary>
        /// 慢查询阈值（毫秒）。
        /// </summary>
        private readonly int _slowQueryThresholdMilliseconds;
        /// <summary>
        /// 单次分析批次大小。
        /// </summary>
        private readonly int _analysisBatchSize;
        /// <summary>
        /// 触发调优动作所需最小调用次数。
        /// </summary>
        private readonly int _triggerCount;
        /// <summary>
        /// 每轮最多输出的调优建议数量。
        /// </summary>
        private readonly int _maxSuggestionsPerCycle;
        /// <summary>
        /// 慢查询样本队列最大容量。
        /// </summary>
        private readonly int _maxQueueSize;
        /// <summary>
        /// 聚合分析 TopN 上限。
        /// </summary>
        private readonly int _aggregationTopN;
        /// <summary>
        /// 告警防抖最小调用次数阈值。
        /// </summary>
        private readonly int _alertDebounceMinCallCount;
        /// <summary>
        /// P99 告警阈值（毫秒）。
        /// </summary>
        private readonly int _alertP99Milliseconds;
        /// <summary>
        /// 超时率告警阈值（百分比）。
        /// </summary>
        private readonly decimal _alertTimeoutRatePercent;
        /// <summary>
        /// 死锁告警阈值计数。
        /// </summary>
        private readonly int _alertDeadlockCount;
        /// <summary>
        /// 告警防抖窗口。
        /// </summary>
        private readonly TimeSpan _alertDebounceWindow;
        /// <summary>
        /// 告警触发所需连续窗口数。
        /// </summary>
        private readonly int _alertConsecutiveWindows;
        /// <summary>
        /// 告警恢复所需连续健康窗口数。
        /// </summary>
        private readonly int _alertRecoveryConsecutiveWindows;
        /// <summary>
        /// 每日汇总输出本地时间点。
        /// </summary>
        private readonly TimeSpan _dailyReportTime;
        /// <summary>
        /// 月报输出的每月日期（1~28）。
        /// </summary>
        private readonly int _monthlyReportDay;
        /// <summary>
        /// 可观测输出器（指标/事件）。
        /// </summary>
        private readonly IAutoTuningObservability _observability;
        /// <summary>
        /// 队列与状态访问同步锁。
        /// </summary>
        private readonly object _queueSync = new();
        /// <summary>
        /// 告警防抖状态字典（按 SQL 指纹跟踪）。
        /// </summary>
        private readonly Dictionary<string, AlertTrackingState> _alertStates = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// 队列溢出时的丢弃样本计数。
        /// </summary>
        private int _droppedCount;
        /// <summary>
        /// 下一次应输出日报的本地时间。
        /// </summary>
        private DateTime _nextDailyReportTime;
        /// <summary>
        /// 下一次应输出月报的本地日期。
        /// </summary>
        private DateTime _nextMonthlyReportDate;
        /// <summary>
        /// 年度运行看板输出月份（1~12），每年该月 1 日与日报同时触发年度看板生成。
        /// </summary>
        private readonly int _annualDashboardMonth;
        /// <summary>
        /// 下一次应输出年度运行看板的本地日期。
        /// </summary>
        private DateTime _nextAnnualDashboardDate;

        /// <summary>初始化慢查询采集、分析和告警阈值配置。</summary>
        public SlowQueryAutoTuningPipeline(IConfiguration configuration, IAutoTuningObservability observability) {
            _observability = observability;
            _slowQueryThresholdMilliseconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("SlowQueryThresholdMilliseconds"), 500);
            _analysisBatchSize = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AnalysisBatchSize"), 20);
            _triggerCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("TriggerCount"), 3);
            _maxSuggestionsPerCycle = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("MaxActionsPerCycle"), 3);
            _maxQueueSize = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("MaxQueueSize"), 1000);
            _aggregationTopN = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AggregationTopN"), 10);
            _alertDebounceMinCallCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AlertDebounceMinCallCount"), _triggerCount);
            _alertP99Milliseconds = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AlertP99Milliseconds"), 500);
            _alertTimeoutRatePercent = AutoTuningConfigurationHelper.GetNonNegativeDecimalOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AlertTimeoutRatePercent"), 1m);
            _alertDeadlockCount = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AlertDeadlockCount"), 1);
            _alertDebounceWindow = AutoTuningConfigurationHelper.GetPositiveSecondsAsTimeSpanOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AlertDebounceWindowSeconds"), TimeSpan.FromMinutes(10));
            _alertConsecutiveWindows = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AlertConsecutiveWindows"), 1);
            _alertRecoveryConsecutiveWindows = AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AlertRecoveryConsecutiveWindows"), 1);
            _dailyReportTime = AutoTuningConfigurationHelper.GetTimeOfDayOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("DailyReportLocalTime"), new TimeSpan(2, 30, 0));
            _monthlyReportDay = Math.Clamp(
                AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("MonthlyReportDay"), 1),
                1, 28);
            _annualDashboardMonth = Math.Clamp(
                AutoTuningConfigurationHelper.GetPositiveIntOrDefault(configuration, AutoTuningConfigurationHelper.BuildAutoTuningKey("AnnualDashboardMonth"), 1),
                1, 12);
            _nextDailyReportTime = BuildNextDailyReportTime(DateTime.Now);
            _nextMonthlyReportDate = BuildNextMonthlyReportDate(DateTime.Now);
            _nextAnnualDashboardDate = BuildNextAnnualDashboardDate(DateTime.Now);
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
            var shouldEmitMonthlyReport = ShouldEmitMonthlyReport(now);
            var shouldEmitAnnualDashboard = ShouldEmitAnnualDashboard(now);
            var window = DequeueWindow();
            _observability.EmitMetric("autotuning.analysis.window_size", window.Count);
            if (window.Count == 0) {
                return SlowQueryAnalysisResult.Empty with {
                    GeneratedTime = now,
                    DroppedSamples = GetDroppedCount(),
                    ShouldEmitDailyReport = shouldEmitDailyReport,
                    ShouldEmitMonthlyReport = shouldEmitMonthlyReport,
                    ShouldEmitAnnualDashboard = shouldEmitAnnualDashboard
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
                    notification.IsRecovery ? LogLevel.Info : LogLevel.Warn,
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
                ShouldEmitDailyReport: shouldEmitDailyReport,
                ShouldEmitMonthlyReport: shouldEmitMonthlyReport,
                ShouldEmitAnnualDashboard: shouldEmitAnnualDashboard);
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

        /// <summary>
        /// 清理超过容量上限的最旧告警状态。
        /// </summary>
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

        /// <summary>判断当前是否到达月报输出时点（每月第 N 天日报时间点）。</summary>
        private bool ShouldEmitMonthlyReport(DateTime now) {
            lock (_queueSync) {
                if (now < _nextMonthlyReportDate) {
                    return false;
                }

                _nextMonthlyReportDate = BuildNextMonthlyReportDate(now);
                return true;
            }
        }

        /// <summary>计算下一次月报触发日期（每月第 N 天，与 DailyReportLocalTime 同时输出）。</summary>
        private DateTime BuildNextMonthlyReportDate(DateTime now) {
            // 步骤 1：基于 now.Date 构建本月候选值，保持与 now 一致的本地时间 DateTimeKind
            var currentMonthStart = now.Date.AddDays(1 - now.Day);
            var candidate = currentMonthStart.AddDays(_monthlyReportDay - 1).Add(_dailyReportTime);
            if (candidate > now) {
                return candidate;
            }

            // 步骤 2：推进到下个月的同一天，复用已校验的 _dailyReportTime
            return currentMonthStart.AddMonths(1).AddDays(_monthlyReportDay - 1).Add(_dailyReportTime);
        }

        /// <summary>判断当前是否到达年度运行看板输出时点（每年指定月 1 日日报时间点）。</summary>
        private bool ShouldEmitAnnualDashboard(DateTime now) {
            lock (_queueSync) {
                if (now < _nextAnnualDashboardDate) {
                    return false;
                }

                _nextAnnualDashboardDate = BuildNextAnnualDashboardDate(now);
                return true;
            }
        }

        /// <summary>计算下一次年度运行看板触发日期（每年指定月 1 日，与 DailyReportLocalTime 同时输出）。</summary>
        private DateTime BuildNextAnnualDashboardDate(DateTime now) {
            // 步骤 1：构建当年目标月 1 日候选值
            var thisYear = new DateTime(now.Year, _annualDashboardMonth, 1, 0, 0, 0, DateTimeKind.Local).Add(_dailyReportTime);
            if (thisYear > now) {
                return thisYear;
            }

            // 步骤 2：已过本年触发点，推进到下一年同月 1 日
            return new DateTime(now.Year + 1, _annualDashboardMonth, 1, 0, 0, 0, DateTimeKind.Local).Add(_dailyReportTime);
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

        /// <summary>从 SQL 中提取主表 schema/table。</summary>
        public static bool TryExtractPrimaryTable(string sql, out string? schemaName, out string tableName) {
            schemaName = null;
            tableName = string.Empty;

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

            schemaName = string.IsNullOrWhiteSpace(candidateSchema) ? null : candidateSchema;
            tableName = candidateTable;
            return true;
        }

        /// <summary>从 SQL 中提取 schema/table 与 where 列候选。</summary>
        private static bool TryExtractTableAndColumns(string sql, out string? schemaName, out string tableName, out IReadOnlyList<string> whereColumns) {
            whereColumns = Array.Empty<string>();

            if (!TryExtractPrimaryTable(sql, out schemaName, out tableName)) {
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

            whereColumns = columns;
            return true;
        }

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

        /// <summary>
        /// 构建告警唯一键（fingerprint|type）。
        /// </summary>
        private static string BuildAlertKey(string fingerprint, string type) => $"{fingerprint}|{type}";

        /// <summary>
        /// 从告警键 <c>fingerprint|type</c> 中提取 <c>fingerprint</c>；若缺少分隔符 <c>|</c> 则返回原始键值。
        /// </summary>
        private static string TryExtractFingerprintFromAlertKey(string alertKey) {
            var separatorIndex = alertKey.IndexOf('|');
            return separatorIndex < 0 ? alertKey : alertKey[..separatorIndex];
        }

        /// <summary>
        /// 从告警键 <c>fingerprint|type</c> 中提取 <c>type</c>；若缺少分隔符 <c>|</c> 则返回默认值 <c>UNKNOWN</c>，且不额外做大小写转换。
        /// </summary>
        private static string TryExtractTypeFromAlertKey(string alertKey) {
            var separatorIndex = alertKey.IndexOf('|');
            return separatorIndex < 0 ? "UNKNOWN" : alertKey[(separatorIndex + 1)..];
        }

    }
}
