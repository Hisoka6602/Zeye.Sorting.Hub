namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份命令文本格式化工具。
/// </summary>
internal static class BackupCommandTextFormatter {
    /// <summary>
    /// 使用 POSIX Shell 单引号规则包装参数。
    /// </summary>
    /// <remarks>
    /// POSIX Shell 的单引号内部不支持直接转义单引号；
    /// 因此这里通过 <c>'"'"'"'"'"'"'"'"'</c> 序列实现“结束单引号 → 输出字面量单引号 → 重新开启单引号”。
    /// </remarks>
    /// <param name="value">原始参数。</param>
    /// <returns>安全参数文本。</returns>
    internal static string QuotePosixShellArgument(string value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }

    /// <summary>
     /// 使用 Shell 双引号规则包装参数。
    /// </summary>
    /// <remarks>
    /// 转义顺序必须先处理反斜杠，再处理双引号；
    /// 否则原文中的转义前缀会被重复放大，导致命令参数含义漂移。
    /// </remarks>
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
