using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>
/// 慢查询指纹聚合辅助器。
/// </summary>
public static class SlowQueryFingerprintAggregator {
    /// <summary>
    /// 多空白折叠正则。
    /// </summary>
    private static readonly Regex MultiWhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// EF Core / ADO.NET 命名参数占位符正则。
    /// </summary>
    private static readonly Regex NamedParameterRegex = new(
        @"@[A-Za-z_][A-Za-z0-9_]*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 数值字面量正则。
    /// </summary>
    private static readonly Regex NumericLiteralRegex = new(
        @"(?<![A-Za-z0-9_])[-+]?(?:\d+\.\d+|\d+)(?![A-Za-z0-9_])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 字符串字面量正则。
    /// </summary>
    private static readonly Regex StringLiteralRegex = new(
        @"'(?:''|[^'])*'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 生成慢查询指纹。
    /// </summary>
    /// <param name="commandText">原始 SQL。</param>
    /// <returns>标准化指纹结果。</returns>
    public static SlowQueryFingerprint Create(string commandText) {
        var normalizedSql = NormalizeSql(commandText);
        return new SlowQueryFingerprint(
            Fingerprint: BuildFingerprintId(normalizedSql),
            NormalizedSql: normalizedSql);
    }

    /// <summary>
    /// 归一化 SQL 文本。
    /// </summary>
    /// <param name="sql">原始 SQL。</param>
    /// <returns>去参数化后的标准 SQL。</returns>
    public static string NormalizeSql(string sql) {
        if (string.IsNullOrWhiteSpace(sql)) {
            return string.Empty;
        }

        // 步骤 1：剥离字符串与参数占位符，避免业务实参影响指纹稳定性。
        var withoutStringLiterals = StringLiteralRegex.Replace(sql, "?");
        var withoutNamedParameters = NamedParameterRegex.Replace(withoutStringLiterals, "?");

        // 步骤 2：将直接内联的数值常量统一替换为占位符，覆盖 limit/top/where id=1 等语句。
        var withoutNumericLiterals = NumericLiteralRegex.Replace(withoutNamedParameters, "?");

        // 步骤 3：压缩空白并统一小写，保证同义 SQL 产生稳定指纹。
        var normalized = MultiWhitespaceRegex.Replace(withoutNumericLiterals, " ").Trim().ToLowerInvariant();
        return normalized.Length <= 512 ? normalized : normalized[..512];
    }

    /// <summary>
    /// 基于标准 SQL 构建指纹标识。
    /// </summary>
    /// <param name="normalizedSql">标准 SQL。</param>
    /// <returns>16 位十六进制指纹。</returns>
    public static string BuildFingerprintId(string normalizedSql) {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedSql));
        return Convert.ToHexString(hashBytes[..8]).ToLowerInvariant();
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
        var orderedSamples = samples
            .OrderBy(static sample => sample.OccurredTime)
            .ToArray();
        var orderedElapsed = orderedSamples
            .Select(static sample => sample.ElapsedMilliseconds)
            .OrderBy(static elapsed => elapsed)
            .ToArray();
        var latestSample = orderedSamples[^1];

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

        var averageElapsedMilliseconds = orderedSamples.Average(static sample => sample.ElapsedMilliseconds);

        // 步骤 3：输出窗口起止、脱敏样例 SQL 及高位分位数，供 API 直接返回只读快照。
        return new SlowQueryProfileSnapshot(
            Fingerprint: fingerprint.Fingerprint,
            NormalizedSql: fingerprint.NormalizedSql,
            SampleSql: NormalizeSql(latestSample.CommandText),
            CallCount: callCount,
            AverageElapsedMilliseconds: averageElapsedMilliseconds,
            P95Milliseconds: CalculatePercentile(orderedElapsed, 95),
            P99Milliseconds: CalculatePercentile(orderedElapsed, 99),
            MaxMilliseconds: orderedElapsed[^1],
            TimeoutCount: timeoutCount,
            ErrorCount: errorCount,
            DeadlockCount: deadlockCount,
            TotalAffectedRows: totalAffectedRows,
            WindowStartedAtLocal: orderedSamples[0].OccurredTime,
            WindowEndedAtLocal: latestSample.OccurredTime,
            LastOccurredAtLocal: latestSample.OccurredTime);
    }

    /// <summary>
    /// 计算分位点。
    /// </summary>
    /// <param name="sortedValues">升序耗时数组。</param>
    /// <param name="percentile">分位点。</param>
    /// <returns>分位点值。</returns>
    private static double CalculatePercentile(IReadOnlyList<double> sortedValues, int percentile) {
        if (sortedValues.Count == 0) {
            return 0d;
        }

        var rank = (int)Math.Ceiling(percentile / 100d * sortedValues.Count);
        var index = Math.Clamp(rank - 1, 0, sortedValues.Count - 1);
        return sortedValues[index];
    }
}
