using System.Security.Cryptography;
using System.Text;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// 数据库提供器操作工具类，提供异常错误码提取、WHERE 列归一化与稳定索引名构造等共享能力，
    /// 供 MySQL 和 SQL Server 方言实现复用，消除重复实现。
    /// </summary>
    internal static class DatabaseProviderOperations {
        /// <summary>
        /// 尝试从异常链中提取数据库提供器错误码。
        /// </summary>
        public static bool TryGetProviderErrorNumber(Exception exception, out int errorNumber) {
            for (Exception? current = exception; current is not null; current = current.InnerException) {
                var numberProperty = current.GetType().GetProperty("Number");
                if (numberProperty?.PropertyType == typeof(int) && numberProperty.GetValue(current) is int number) {
                    errorNumber = number;
                    return true;
                }
            }

            errorNumber = 0;
            return false;
        }

        /// <summary>
        /// 归一化候选 where 列：过滤空白、限制最大列数并裁剪空格。
        /// </summary>
        /// <param name="whereColumns">候选 where 列。</param>
        /// <param name="maxColumnCount">允许参与索引构造的最大列数。</param>
        /// <returns>归一化后的列数组。</returns>
        public static string[] NormalizeWhereColumns(IReadOnlyList<string> whereColumns, int maxColumnCount) {
            ArgumentNullException.ThrowIfNull(whereColumns);
            if (whereColumns.Count == 0 || maxColumnCount <= 0) {
                return [];
            }

            return whereColumns
                .Where(static c => !string.IsNullOrWhiteSpace(c))
                .Take(maxColumnCount)
                .Select(static c => c.Trim())
                .ToArray();
        }

        /// <summary>
        /// 构造长度受限且稳定的自动索引名称。
        /// </summary>
        /// <param name="schemaName">schema 名。</param>
        /// <param name="tableName">表名。</param>
        /// <param name="columns">索引列。</param>
        /// <param name="maxLength">最大长度约束。</param>
        /// <returns>稳定且可复现的索引名。</returns>
        public static string BuildIndexName(string? schemaName, string tableName, IReadOnlyList<string> columns, int maxLength) {
            ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
            ArgumentNullException.ThrowIfNull(columns);
            if (columns.Count == 0) {
                throw new ArgumentException("索引列不能为空。", nameof(columns));
            }
            if (maxLength <= 0) {
                throw new ArgumentOutOfRangeException(nameof(maxLength), "最大长度必须大于 0。");
            }
            const int hashHexLength = 8;
            var minAllowedLength = hashHexLength + 1; // 哈希十六进制(8) + 下划线(1) = 9
            if (maxLength < minAllowedLength) {
                throw new ArgumentOutOfRangeException(nameof(maxLength), $"最大长度至少为 {minAllowedLength}，以容纳 '_'+哈希后缀。");
            }

            var schemaPart = schemaName ?? string.Empty;
            var seed = $"{schemaPart}:{tableName}:{string.Join(",", columns)}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
            var hash = Convert.ToHexString(hashBytes[..4]).ToLowerInvariant();
            var tableSeed = string.IsNullOrWhiteSpace(schemaName) ? tableName : $"{schemaName}_{tableName}";
            var prefix = $"idx_auto_{tableSeed}_{string.Join("_", columns)}";

            var normalizedPrefix = prefix.Length > maxLength - hash.Length - 1
                ? prefix[..(maxLength - hash.Length - 1)]
                : prefix;

            return $"{normalizedPrefix}_{hash}";
        }
    }
}
