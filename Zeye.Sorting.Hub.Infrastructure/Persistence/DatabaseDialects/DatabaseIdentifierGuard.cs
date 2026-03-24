using System.Text.RegularExpressions;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// 数据库标识符安全守卫。
    /// </summary>
    internal static class DatabaseIdentifierGuard {
        /// <summary>
        /// 数据库名安全格式：字母/数字/下划线，首字符必须是字母或下划线。
        /// </summary>
        private static readonly Regex SafeIdentifierRegex = new(
            "^[A-Za-z_][A-Za-z0-9_]*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// 规范化并校验数据库名。
        /// </summary>
        /// <param name="databaseName">原始数据库名。</param>
        /// <param name="paramName">参数名。</param>
        /// <returns>安全数据库名。</returns>
        internal static string NormalizeDatabaseName(string? databaseName, string paramName) {
            var normalized = databaseName?.Trim();
            if (string.IsNullOrWhiteSpace(normalized)) {
                throw new InvalidOperationException($"缺少数据库名参数：{paramName}。");
            }

            if (!SafeIdentifierRegex.IsMatch(normalized)) {
                throw new InvalidOperationException($"数据库名不安全或不受支持：{normalized}");
            }

            return normalized;
        }

        /// <summary>
        /// 转义 MySQL 标识符。
        /// </summary>
        /// <param name="identifier">标识符。</param>
        /// <returns>转义后文本。</returns>
        internal static string EscapeMySqlIdentifier(string identifier) {
            return NormalizeDatabaseName(identifier, nameof(identifier))
                .Replace("`", "``", StringComparison.Ordinal);
        }

        /// <summary>
        /// 转义 SQL Server 标识符。
        /// </summary>
        /// <param name="identifier">标识符。</param>
        /// <returns>转义后文本。</returns>
        internal static string EscapeSqlServerIdentifier(string identifier) {
            return NormalizeDatabaseName(identifier, nameof(identifier))
                .Replace("]", "]]", StringComparison.Ordinal);
        }
    }
}
