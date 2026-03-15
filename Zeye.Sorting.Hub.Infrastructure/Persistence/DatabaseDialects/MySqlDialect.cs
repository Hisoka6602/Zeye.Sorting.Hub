using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// MySQL 方言
    /// </summary>
    public sealed class MySqlDialect : IDatabaseDialect {
        public string ProviderName => "MySQL";

        public IReadOnlyList<string> GetOptionalBootstrapSql() => new[] {
            "SET SESSION optimizer_switch='index_merge=on,index_condition_pushdown=on,derived_merge=on'",
            "SET SESSION innodb_stats_on_metadata=OFF"
        };

        public IReadOnlyList<string> BuildAutomaticTuningSql(string tableName, IReadOnlyList<string> whereColumns) {
            if (whereColumns.Count == 0) {
                return Array.Empty<string>();
            }

            var normalizedTableName = tableName.Trim();
            var indexColumns = whereColumns
                .Where(static c => !string.IsNullOrWhiteSpace(c))
                .Take(3)
                .Select(static c => c.Trim())
                .ToArray();

            if (indexColumns.Length == 0) {
                return Array.Empty<string>();
            }

            var indexName = BuildIndexName(normalizedTableName, indexColumns, 60);

            var escapedTable = $"`{normalizedTableName}`";
            var escapedColumns = string.Join(", ", indexColumns.Select(static col => $"`{col}`"));
            var escapedIndexName = $"`{indexName}`";

            return new[] {
                $"CREATE INDEX IF NOT EXISTS {escapedIndexName} ON {escapedTable} ({escapedColumns})",
                $"ANALYZE TABLE {escapedTable}"
            };
        }

        private static string BuildIndexName(string tableName, IReadOnlyList<string> columns, int maxLength) {
            var seed = $"{tableName}:{string.Join(",", columns)}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
            var hash = Convert.ToHexString(hashBytes[..4]).ToLowerInvariant();
            var prefix = $"idx_auto_{tableName}_{string.Join("_", columns)}";

            var normalizedPrefix = prefix.Length > maxLength - hash.Length - 1
                ? prefix[..(maxLength - hash.Length - 1)]
                : prefix;

            return $"{normalizedPrefix}_{hash}";
        }
    }
}
