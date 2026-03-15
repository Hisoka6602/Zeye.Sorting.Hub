using System;
using System.Linq;
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

            var indexName = $"idx_auto_{normalizedTableName}_{string.Join("_", indexColumns)}";
            if (indexName.Length > 60) {
                indexName = indexName[..60];
            }

            var escapedTable = $"`{normalizedTableName}`";
            var escapedColumns = string.Join(", ", indexColumns.Select(static col => $"`{col}`"));
            var escapedIndexName = $"`{indexName}`";

            return new[] {
                $"CREATE INDEX IF NOT EXISTS {escapedIndexName} ON {escapedTable} ({escapedColumns})",
                $"ANALYZE TABLE {escapedTable}"
            };
        }
    }
}
