using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>SQL Server 方言</summary>
    public sealed class SqlServerDialect : IDatabaseDialect {
        /// <summary>当前方言提供器名称。</summary>
        public string ProviderName => "SQLServer";

        /// <summary>返回 SQL Server 可选初始化 SQL。</summary>
        public IReadOnlyList<string> GetOptionalBootstrapSql() => new[] {
            "ALTER DATABASE CURRENT SET QUERY_STORE = ON",
            "ALTER DATABASE CURRENT SET QUERY_STORE (OPERATION_MODE = READ_WRITE, QUERY_CAPTURE_MODE = AUTO)",
            "ALTER DATABASE CURRENT SET AUTOMATIC_TUNING (FORCE_LAST_GOOD_PLAN = ON)",
            "ALTER DATABASE CURRENT SET AUTO_CREATE_STATISTICS ON",
            "ALTER DATABASE CURRENT SET AUTO_UPDATE_STATISTICS ON"
        };

        /// <summary>根据慢查询 where 列生成 SQL Server 自动调优 SQL。</summary>
        public IReadOnlyList<string> BuildAutomaticTuningSql(string? schemaName, string tableName, IReadOnlyList<string> whereColumns) {
            if (whereColumns.Count == 0) {
                return Array.Empty<string>();
            }

            var normalizedSchemaName = schemaName?.Trim();
            var normalizedTableName = tableName.Trim();
            var indexColumns = DatabaseProviderExceptionHelper.NormalizeWhereColumns(whereColumns, 3);

            if (indexColumns.Length == 0) {
                return Array.Empty<string>();
            }

            var indexName = DatabaseProviderExceptionHelper.BuildIndexName(normalizedSchemaName, normalizedTableName, indexColumns, 120);

            var escapedTable = string.IsNullOrWhiteSpace(normalizedSchemaName)
                ? $"[{normalizedTableName}]"
                : $"[{normalizedSchemaName}].[{normalizedTableName}]";
            var escapedColumns = string.Join(", ", indexColumns.Select(static col => $"[{col}]"));
            var escapedIndexName = $"[{indexName}]";
            var escapedIndexNameLiteral = indexName.Replace("'", "''", StringComparison.Ordinal);
            var objectNameLiteral = string.IsNullOrWhiteSpace(normalizedSchemaName)
                ? normalizedTableName
                : $"{normalizedSchemaName}.{normalizedTableName}";
            var escapedObjectNameLiteral = objectNameLiteral.Replace("'", "''", StringComparison.Ordinal);

            return new[] {
                $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{escapedIndexNameLiteral}' AND object_id = OBJECT_ID(N'{escapedObjectNameLiteral}')) CREATE INDEX {escapedIndexName} ON {escapedTable} ({escapedColumns})",
                $"UPDATE STATISTICS {escapedTable} WITH RESAMPLE"
            };
        }

        /// <summary>判断异常是否可被视为“已存在”并忽略。</summary>
        public bool ShouldIgnoreAutoTuningException(Exception exception) {
            return DatabaseProviderExceptionHelper.TryGetProviderErrorNumber(exception, out var errorNumber) && errorNumber == 1913;
        }

        /// <summary>生成闭环自治维护 SQL（高峰/高风险仅执行轻量动作）。</summary>
        public IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) {
            if (string.IsNullOrWhiteSpace(tableName)) {
                return Array.Empty<string>();
            }

            var normalizedSchemaName = schemaName?.Trim();
            var normalizedTableName = tableName.Trim();
            var escapedTable = string.IsNullOrWhiteSpace(normalizedSchemaName)
                ? $"[{normalizedTableName}]"
                : $"[{normalizedSchemaName}].[{normalizedTableName}]";
            var updateStatisticsSql = $"UPDATE STATISTICS {escapedTable} WITH RESAMPLE";

            if (inPeakWindow || highRisk) {
                return new[] { updateStatisticsSql };
            }

            return new[] { updateStatisticsSql, $"ALTER INDEX ALL ON {escapedTable} REORGANIZE" };
        }

    }
}
