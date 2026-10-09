using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询指纹聚合辅助器。
/// </summary>
public static partial class SlowQueryFingerprintAggregator {
    /// <summary>Oracle 管理 DDL 的双引号密码也必须脱敏。</summary>
    [GeneratedRegex("(?i)(identified\\s+by\\s+)\"(?:\"\"|[^\"])*\"", RegexOptions.CultureInvariant)]
    private static partial Regex PasswordRegex();

    /// <summary>脱敏样例保留标识符大小写，调优解析不能错误修改大小写敏感的物理表名。</summary>
    public static string SanitizeSql(string sql, string provider = "") => SlowQuerySqlText.Transform(PasswordRegex().Replace(sql, "$1?"), false, provider);

    /// <summary>
    /// 生成慢查询指纹。
    /// </summary>
    /// <param name="commandText">原始 SQL。</param>
    /// <param name="provider">提供器名称，用于区分引号语义。</param>
    /// <returns>标准化指纹结果。</returns>
    public static SlowQueryFingerprint Create(string commandText, string provider = "") {
        var normalizedSql = NormalizeSql(commandText, provider);
        return new SlowQueryFingerprint(
            Fingerprint: BuildFingerprintId(normalizedSql),
            NormalizedSql: normalizedSql);
    }
    /// <summary>统一隔离请求、事务、连接和不同数据库用途，显式记录重载不能错误合并画像。</summary>
    internal static SlowQueryFingerprint CreateObservation(string commandText, SlowQueryObservation observation) {
        var fingerprint = Create(commandText, observation.Provider);
        return observation.Kind == "query" && observation.Provider.Length == 0 && observation.DatabaseRole == "business" ? fingerprint
            : fingerprint with { Fingerprint = BuildFingerprintId(observation.Kind + ":" + observation.Provider + ":" + observation.DatabaseRole + ":" + fingerprint.NormalizedSql) };
    }

    /// <summary>
    /// 归一化 SQL 文本。
    /// </summary>
    /// <param name="sql">原始 SQL。</param>
    /// <param name="provider">提供器名称，用于区分引号语义。</param>
    /// <returns>去参数化后的标准 SQL。</returns>
    public static string NormalizeSql(string sql, string provider = "") {
        if (string.IsNullOrWhiteSpace(sql)) {
            return string.Empty;
        }

        return SlowQuerySqlText.Transform(PasswordRegex().Replace(sql, "$1?"), true, provider);
    }

    /// <summary>
    /// 基于标准 SQL 构建指纹标识。
    /// </summary>
    /// <param name="normalizedSql">标准 SQL。</param>
    /// <returns>16 位十六进制指纹。</returns>
    public static string BuildFingerprintId(string normalizedSql) {
        // 大型分析 SQL 不使用无界栈分配；哈希只在慢样本发布时执行。
        var utf8Buffer = Encoding.UTF8.GetBytes(normalizedSql);
        Span<byte> hashBytes = stackalloc byte[32];
        SHA256.HashData(utf8Buffer, hashBytes);
        return Convert.ToHexStringLower(hashBytes[..8]);
    }

    /// <summary>
    /// 构建查询画像快照。
    /// </summary>
    /// <param name="fingerprint">慢查询指纹。</param>
    /// <param name="samples">窗口样本。</param>
    /// <returns>画像快照。</returns>
    public static SlowQueryProfileSnapshot BuildSnapshot(SlowQueryFingerprint fingerprint, IReadOnlyCollection<SlowQuerySample> samples) {
        ArgumentNullException.ThrowIfNull(fingerprint);
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count == 0) {
            throw new ArgumentException("慢查询画像快照至少需要一个样本。", nameof(samples));
        }

        // 步骤 1：按发生时间升序准备窗口样本，同时单独提取耗时升序数组用于分位点计算。
        var orderedSamples = samples.ToArray();
        var orderedElapsed = orderedSamples
            .Select(static sample => sample.ElapsedMilliseconds)
            .OrderBy(static elapsed => elapsed)
            .ToArray();
        var latestSample = orderedSamples[0];
        var earliestOccurredTime = latestSample.OccurredTime;

        // 步骤 2：计算窗口聚合指标。
        var callCount = orderedSamples.Length;
        var timeoutCount = 0;
        var errorCount = 0;
        var deadlockCount = 0;
        var totalAffectedRows = 0;
        foreach (var sample in orderedSamples) {
            if (sample.IsTimeout) {
                timeoutCount++;
            }

            if (sample.IsError) {
                errorCount++;
            }

            if (sample.IsDeadlock) {
                deadlockCount++;
            }

            totalAffectedRows += sample.AffectedRows;
        }

        var totalElapsedMilliseconds = 0m;
        foreach (var sample in orderedSamples) {
            if (sample.OccurredTime > latestSample.OccurredTime) {
                latestSample = sample;
            }

            if (sample.OccurredTime < earliestOccurredTime) {
                earliestOccurredTime = sample.OccurredTime;
            }

            totalElapsedMilliseconds += sample.ElapsedMilliseconds;
        }

        var averageElapsedMilliseconds = totalElapsedMilliseconds / callCount;

        // 步骤 3：输出窗口起止、脱敏样例 SQL 及高位分位数，供 API 直接返回只读快照。
        return new SlowQueryProfileSnapshot(
            Fingerprint: fingerprint.Fingerprint,
            NormalizedSql: fingerprint.NormalizedSql,
            SampleSql: NormalizeSql(latestSample.CommandText, latestSample.Observation.Provider),
            CallCount: callCount,
            AverageElapsedMilliseconds: averageElapsedMilliseconds,
            P95Milliseconds: CalculatePercentile(orderedElapsed, 95),
            P99Milliseconds: CalculatePercentile(orderedElapsed, 99),
            MaxMilliseconds: orderedElapsed[^1],
            TimeoutCount: timeoutCount,
            ErrorCount: errorCount,
            DeadlockCount: deadlockCount,
            TotalAffectedRows: totalAffectedRows,
            WindowStartedAtLocal: earliestOccurredTime,
            WindowEndedAtLocal: latestSample.OccurredTime,
            LastOccurredAtLocal: latestSample.OccurredTime) {
            Observation = latestSample.Observation,
            CanceledCount = orderedSamples.Count(sample => sample.Observation.IsCanceled),
            PartialReadCount = orderedSamples.Count(sample => sample.Observation.IsPartialRead),
            AverageExecuteMilliseconds = orderedSamples.Average(sample => sample.Observation.ExecuteMilliseconds),
            AverageReadMilliseconds = orderedSamples.Average(sample => sample.Observation.ReadMilliseconds),
            AverageConsumerMilliseconds = orderedSamples.Average(sample => sample.Observation.ConsumerMilliseconds),
            AverageConnectionMilliseconds = orderedSamples.Average(sample => sample.Observation.ConnectionMilliseconds),
            TotalRowsRead = orderedSamples.Sum(sample => sample.Observation.RowsRead)
        };
    }

    /// <summary>
    /// 计算分位点。
    /// </summary>
    /// <param name="sortedValues">升序耗时数组。</param>
    /// <param name="percentile">分位点。</param>
    /// <returns>分位点值。</returns>
    private static decimal CalculatePercentile(decimal[] sortedValues, int percentile) {
        if (sortedValues.Length == 0) {
            return 0m;
        }

        var rank = (int)Math.Ceiling(percentile / 100m * sortedValues.Length);
        var index = Math.Clamp(rank - 1, 0, sortedValues.Length - 1);
        return sortedValues[index];
    }
}
