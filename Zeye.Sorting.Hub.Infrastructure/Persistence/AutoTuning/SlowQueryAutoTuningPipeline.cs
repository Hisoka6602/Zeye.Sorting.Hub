using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>
    /// 慢查询采集、分析与自动动作编排管道
    /// </summary>
    public sealed class SlowQueryAutoTuningPipeline {
        private const string AutoTuningMarker = "AUTO_TUNING";
        private const int MaxWhereColumns = 3;
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
        private readonly int _alertP99Milliseconds;
        private readonly decimal _alertTimeoutRatePercent;
        private readonly int _alertDeadlockCount;
        private readonly TimeSpan _dailyReportTime;
        private readonly object _queueSync = new();
        private int _droppedCount;
        private DateTime _nextDailyReportTime;

        public SlowQueryAutoTuningPipeline(IConfiguration configuration) {
            _slowQueryThresholdMilliseconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:SlowQueryThresholdMilliseconds", 500);
            _analysisBatchSize = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:AnalysisBatchSize", 20);
            _triggerCount = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:TriggerCount", 3);
            _maxSuggestionsPerCycle = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:MaxActionsPerCycle", 3);
            _maxQueueSize = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:MaxQueueSize", 1000);
            _aggregationTopN = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:AggregationTopN", 10);
            _alertP99Milliseconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:AlertP99Milliseconds", 500);
            _alertTimeoutRatePercent = GetDecimalOrDefault(configuration, "Persistence:AutoTuning:AlertTimeoutRatePercent", 1m);
            _alertDeadlockCount = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:AlertDeadlockCount", 1);
            _dailyReportTime = GetTimeOfDayOrDefault(configuration, "Persistence:AutoTuning:DailyReportLocalTime", new TimeSpan(2, 30, 0));
            _nextDailyReportTime = BuildNextDailyReportTime(DateTime.Now);
        }

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

                if (_slowQueries.Count >= _maxQueueSize) {
                    _droppedCount++;
                    return;
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

        public SlowQueryAnalysisResult Analyze(IDatabaseDialect dialect) {
            var window = DequeueWindow();
            if (window.Count == 0) {
                return SlowQueryAnalysisResult.Empty;
            }

            var groups = window
                .GroupBy(static q => q.SqlFingerprint)
                .Select(group => BuildMetric(group.Key, group))
                .OrderByDescending(static x => x.P99Milliseconds)
                .ThenByDescending(static x => x.CallCount)
                .Take(_aggregationTopN)
                .ToList();

            var suggestions = BuildSuggestions(dialect, groups);
            var alerts = BuildAlerts(groups);
            var now = DateTime.Now;
            var shouldEmitDailyReport = ShouldEmitDailyReport(now);

            return new SlowQueryAnalysisResult(
                GeneratedTime: now,
                DroppedSamples: GetDroppedCount(),
                Metrics: groups,
                ReadOnlySuggestions: suggestions,
                Alerts: alerts,
                ShouldEmitDailyReport: shouldEmitDailyReport);
        }

        private List<SlowQuerySample> DequeueWindow() {
            var result = new List<SlowQuerySample>(_analysisBatchSize);
            lock (_queueSync) {
                while (result.Count < _analysisBatchSize && _slowQueries.TryDequeue(out var sample)) {
                    result.Add(sample);
                }
            }

            return result;
        }

        private int GetDroppedCount() {
            lock (_queueSync) {
                return _droppedCount;
            }
        }

        private static string NormalizeSql(string sql) {
            var withoutStringLiterals = StringLiteralRegex.Replace(sql, "?");
            var withoutParameters = ParameterRegex.Replace(withoutStringLiterals, "?");
            var normalized = MultiWhitespaceRegex.Replace(withoutParameters, " ").Trim();
            return normalized.Length <= 512 ? normalized : normalized[..512];
        }

        private IReadOnlyList<string> BuildSuggestions(IDatabaseDialect dialect, IReadOnlyList<SlowQueryMetric> groups) {
            var suggestions = new List<string>();
            var existedSuggestions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var metric in groups) {
                if (metric.CallCount < _triggerCount) {
                    continue;
                }

                if (!TryExtractTableAndColumns(metric.SampleSql, out var schemaName, out var tableName, out var whereColumns)) {
                    continue;
                }

                var dialectActions = dialect.BuildAutomaticTuningSql(schemaName, tableName, whereColumns);
                foreach (var action in dialectActions) {
                    if (string.IsNullOrWhiteSpace(action)) {
                        continue;
                    }

                    if (suggestions.Count >= _maxSuggestionsPerCycle) {
                        break;
                    }

                    if (existedSuggestions.Add(action)) {
                        suggestions.Add($"/*{AutoTuningMarker}_READ_ONLY*/ {action}");
                    }
                }

                if (suggestions.Count >= _maxSuggestionsPerCycle) {
                    break;
                }
            }

            return suggestions;
        }

        private IReadOnlyList<string> BuildAlerts(IReadOnlyList<SlowQueryMetric> groups) {
            var alerts = new List<string>();
            foreach (var metric in groups) {
                if (metric.P99Milliseconds > _alertP99Milliseconds) {
                    alerts.Add($"P99 超阈值：Fingerprint={metric.SqlFingerprint}, P99Ms={metric.P99Milliseconds:F2}, ThresholdMs={_alertP99Milliseconds}");
                }

                if (metric.TimeoutRatePercent > _alertTimeoutRatePercent) {
                    alerts.Add($"超时率超阈值：Fingerprint={metric.SqlFingerprint}, TimeoutRatePercent={metric.TimeoutRatePercent:F2}, ThresholdPercent={_alertTimeoutRatePercent:F2}");
                }

                if (metric.DeadlockCount >= _alertDeadlockCount) {
                    alerts.Add($"死锁次数超阈值：Fingerprint={metric.SqlFingerprint}, DeadlockCount={metric.DeadlockCount}, Threshold={_alertDeadlockCount}");
                }
            }

            return alerts;
        }

        private bool ShouldEmitDailyReport(DateTime now) {
            lock (_queueSync) {
                if (now < _nextDailyReportTime) {
                    return false;
                }

                _nextDailyReportTime = BuildNextDailyReportTime(now);
                return true;
            }
        }

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
                MaxMilliseconds: elapsedValues[^1]);
        }

        private static double CalculatePercentile(IReadOnlyList<double> sorted, int percentile) {
            if (sorted.Count == 0) {
                return 0d;
            }

            var rank = (int)Math.Ceiling(percentile / 100d * sorted.Count);
            var index = Math.Clamp(rank - 1, 0, sorted.Count - 1);
            return sorted[index];
        }

        private static string BuildSqlFingerprint(string normalizedSql) {
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSql));
            return Convert.ToHexString(hashBytes[..8]).ToLowerInvariant();
        }

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

        private static bool IsDeadlockException(Exception? exception) {
            if (exception is null) {
                return false;
            }

            return DatabaseProviderExceptionHelper.TryGetProviderErrorNumber(exception, out var number)
                && (number == 1205 || number == 1213);
        }

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

        private static int GetPositiveIntOrDefault(IConfiguration configuration, string key, int fallback) {
            var value = configuration[key];
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        }

        private static decimal GetDecimalOrDefault(IConfiguration configuration, string key, decimal fallback) {
            var value = configuration[key];
            return decimal.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
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
        double MaxMilliseconds);

    public sealed record SlowQueryAnalysisResult(
        DateTime GeneratedTime,
        int DroppedSamples,
        IReadOnlyList<SlowQueryMetric> Metrics,
        IReadOnlyList<string> ReadOnlySuggestions,
        IReadOnlyList<string> Alerts,
        bool ShouldEmitDailyReport) {
        public static SlowQueryAnalysisResult Empty => new(
            GeneratedTime: DateTime.Now,
            DroppedSamples: 0,
            Metrics: Array.Empty<SlowQueryMetric>(),
            ReadOnlySuggestions: Array.Empty<string>(),
            Alerts: Array.Empty<string>(),
            ShouldEmitDailyReport: false);
    }
}
