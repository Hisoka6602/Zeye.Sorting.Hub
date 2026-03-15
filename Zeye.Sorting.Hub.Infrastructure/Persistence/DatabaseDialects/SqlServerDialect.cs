using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// SQL Server 方言
    /// </summary>
    public sealed class SqlServerDialect : IDatabaseDialect {
        public string ProviderName => "SQLServer";

        public IReadOnlyList<string> GetOptionalBootstrapSql() => new[] {
            "ALTER DATABASE CURRENT SET QUERY_STORE = ON",
            "ALTER DATABASE CURRENT SET QUERY_STORE (OPERATION_MODE = READ_WRITE, QUERY_CAPTURE_MODE = AUTO)",
            "ALTER DATABASE CURRENT SET AUTOMATIC_TUNING (FORCE_LAST_GOOD_PLAN = ON)",
            "ALTER DATABASE CURRENT SET AUTO_CREATE_STATISTICS ON",
            "ALTER DATABASE CURRENT SET AUTO_UPDATE_STATISTICS ON"
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
            if (indexName.Length > 120) {
                indexName = indexName[..120];
            }

            var escapedTable = $"[{normalizedTableName}]";
            var escapedColumns = string.Join(", ", indexColumns.Select(static col => $"[{col}]"));
            var escapedIndexName = $"[{indexName}]";
            var escapedIndexNameLiteral = indexName.Replace("'", "''", StringComparison.Ordinal);
            var escapedObjectNameLiteral = normalizedTableName.Replace("'", "''", StringComparison.Ordinal);

            return new[] {
                $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{escapedIndexNameLiteral}' AND object_id = OBJECT_ID(N'{escapedObjectNameLiteral}')) CREATE INDEX {escapedIndexName} ON {escapedTable} ({escapedColumns})",
                $"UPDATE STATISTICS {escapedTable} WITH RESAMPLE"
            };
        }
    }
}
