using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
        private readonly int _maxActionsPerCycle;
        private readonly int _maxQueueSize;
        private readonly object _queueSync = new();
        private int _droppedCount;

        public SlowQueryAutoTuningPipeline(IConfiguration configuration) {
            _slowQueryThresholdMilliseconds = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:SlowQueryThresholdMilliseconds", 500);
            _analysisBatchSize = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:AnalysisBatchSize", 20);
            _triggerCount = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:TriggerCount", 3);
            _maxActionsPerCycle = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:MaxActionsPerCycle", 3);
            _maxQueueSize = GetPositiveIntOrDefault(configuration, "Persistence:AutoTuning:MaxQueueSize", 1000);
        }

        public void Collect(string commandText, TimeSpan elapsed) {
            if (elapsed.TotalMilliseconds < _slowQueryThresholdMilliseconds || string.IsNullOrWhiteSpace(commandText)) {
                return;
            }

            if (commandText.Contains(AutoTuningMarker, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

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
                    elapsedMilliseconds: elapsed.TotalMilliseconds,
                    occurredTime: DateTime.Now));
            }
        }

        public IReadOnlyList<string> BuildActions(IDatabaseDialect dialect, ILogger logger) {
            var window = DequeueWindow();
            if (window.Count == 0) {
                return Array.Empty<string>();
            }

            var groups = window
                .GroupBy(static q => NormalizeSql(q.CommandText))
                .OrderByDescending(static g => g.Count())
                .ToList();

            var actions = new List<string>();
            var existedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in groups) {
                if (group.Count() < _triggerCount) {
                    continue;
                }

                var sample = group.First();
                if (!TryExtractTableAndColumns(sample.CommandText, out var schemaName, out var tableName, out var whereColumns)) {
                    continue;
                }

                var dialectActions = dialect.BuildAutomaticTuningSql(schemaName, tableName, whereColumns);
                foreach (var action in dialectActions) {
                    if (string.IsNullOrWhiteSpace(action)) {
                        continue;
                    }

                    if (actions.Count >= _maxActionsPerCycle) {
                        break;
                    }

                    if (existedActions.Add(action)) {
                        actions.Add($"/*{AutoTuningMarker}*/ {action}");
                    }
                }

                if (actions.Count >= _maxActionsPerCycle) {
                    break;
                }
            }

            if (actions.Count > 0) {
                var topSample = window.MaxBy(static s => s.ElapsedMilliseconds);
                logger.LogInformation(
                    "慢查询自动调谐已生成动作，Count={Count}, MaxElapsedMs={MaxElapsedMs}, LastOccurredTime={OccurredTime}, DroppedSamples={DroppedSamples}",
                    actions.Count,
                    topSample?.ElapsedMilliseconds ?? 0d,
                    topSample?.OccurredTime,
                    GetDroppedCount());
            }

            return actions;
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
    }
}
