namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份命令文本格式化工具。
/// </summary>
internal static class BackupCommandTextFormatter {
    /// <summary>
    /// 使用 POSIX Shell 单引号规则包装参数。
    /// </summary>
    /// <param name="value">原始参数。</param>
    /// <returns>安全参数文本。</returns>
    internal static string QuotePosixShellArgument(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }

    /// <summary>
    /// 使用 Shell 双引号规则包装参数。
    /// </summary>
    /// <param name="value">原始参数。</param>
    /// <returns>安全参数文本。</returns>
    internal static string QuoteDoubleQuotedShellArgument(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }

    /// <summary>
    /// 转义 SQL Server 字符串字面量。
    /// </summary>
    /// <param name="value">原始文本。</param>
    /// <returns>转义后文本。</returns>
    internal static string EscapeSqlServerStringLiteral(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
