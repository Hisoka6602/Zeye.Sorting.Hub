using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

/// <summary>
/// 迁移脚本危险操作识别器。
/// </summary>
public sealed partial class MigrationSafetyEvaluator {
    /// <summary>
    /// 语句片段最大保留长度。
    /// </summary>
    private const int MaxStatementPreviewLength = 180;

    /// <summary>
    /// 危险语句匹配规则。
    /// </summary>
    private static readonly IReadOnlyList<(string Label, Regex Pattern)> DangerousStatementRules = [
        ("DROP TABLE", BuildStatementRegex(@"DROP\s+TABLE\b")),
        ("DROP COLUMN", BuildStatementRegex(@"DROP\s+COLUMN\b")),
        ("TRUNCATE", BuildStatementRegex(@"TRUNCATE(\s+TABLE)?\b")),
        ("ALTER COLUMN", BuildStatementRegex(@"ALTER\s+COLUMN\b")),
        ("RENAME COLUMN", BuildStatementRegex(@"RENAME\s+COLUMN\b")),
        ("RENAME TABLE", BuildStatementRegex(@"RENAME\s+TABLE\b|SP_RENAME\b")),
        ("DELETE FROM", BuildStatementRegex(@"DELETE\s+FROM\b"))
    ];

    /// <summary>
    /// UPDATE 无 WHERE 识别正则。
    /// </summary>
    private static readonly Regex UpdateWithoutWhereRegex = BuildStatementRegex(@"UPDATE\b[\s\S]*?\bSET\b(?![\s\S]*\bWHERE\b)");

    /// <summary>
    /// 批处理分割正则。
    /// </summary>
    private static readonly Regex BatchSplitterRegex = BuildBatchSplitterRegex();

    /// <summary>
    /// 识别迁移脚本中的危险操作。
    /// </summary>
    /// <param name="sqlScript">迁移脚本。</param>
    /// <returns>危险操作列表。</returns>
    public IReadOnlyList<string> EvaluateDangerousOperations(string? sqlScript) {
        if (string.IsNullOrWhiteSpace(sqlScript)) {
            return [];
        }

        var matches = new List<string>();
        foreach (var statement in SplitStatements(sqlScript)) {
            foreach (var rule in DangerousStatementRules) {
                if (rule.Pattern.IsMatch(statement)) {
                    matches.Add(BuildDangerousOperationMessage(rule.Label, statement));
                }
            }

            if (UpdateWithoutWhereRegex.IsMatch(statement)) {
                matches.Add(BuildDangerousOperationMessage("UPDATE without WHERE", statement));
            }
        }

        return matches
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// 切分 SQL 脚本中的批处理语句。
    /// </summary>
    /// <param name="sqlScript">迁移脚本。</param>
    /// <returns>语句集合。</returns>
    internal static IReadOnlyList<string> SplitStatements(string sqlScript) {
        return BatchSplitterRegex
            .Split(sqlScript)
            .Select(static statement => statement.Trim())
            .Where(static statement => !string.IsNullOrWhiteSpace(statement))
            .ToArray();
    }

    /// <summary>
    /// 生成危险操作消息。
    /// </summary>
    /// <param name="label">危险类型。</param>
    /// <param name="statement">原始语句。</param>
    /// <returns>危险操作说明。</returns>
    private static string BuildDangerousOperationMessage(string label, string statement) {
        var normalizedStatement = string.Join(' ', statement
            .Split(default(char[]), StringSplitOptions.RemoveEmptyEntries));
        if (normalizedStatement.Length > MaxStatementPreviewLength) {
            normalizedStatement = normalizedStatement[..MaxStatementPreviewLength];
        }

        return $"{label}: {normalizedStatement}";
    }

    /// <summary>
    /// 构建危险语句匹配正则。
    /// </summary>
    /// <param name="pattern">正则文本。</param>
    /// <returns>正则对象。</returns>
    private static Regex BuildStatementRegex(string pattern) {
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    /// <summary>
    /// 构建批处理分割正则。
    /// </summary>
    /// <returns>正则对象。</returns>
    [GeneratedRegex(@"(?im)^\s*GO\s*$|;", RegexOptions.CultureInvariant)]
    private static partial Regex BuildBatchSplitterRegex();
}
