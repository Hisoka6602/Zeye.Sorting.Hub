using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>SQL Server 方言</summary>
    public sealed class SqlServerDialect : IDatabaseDialect, IBatchShardingPhysicalTableProbe {
        /// <summary>
        /// 字段：BatchShardingProbeSql。
        /// </summary>
        internal const string BatchShardingProbeSql = """
SELECT t.name
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name = @p0
""";

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

        /// <summary>
        /// 基于 SQL Server sys.tables / sys.schemas 判断物理分表是否存在。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="schemaName">schema 名称；为空时回退 dbo。</param>
        /// <param name="physicalTableName">物理表名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>存在返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public async Task<bool> ExistsAsync(
            DbContext dbContext,
            string? schemaName,
            string physicalTableName,
            CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(dbContext);
            if (string.IsNullOrWhiteSpace(physicalTableName)) {
                throw new ArgumentException("物理表名不能为空。", nameof(physicalTableName));
            }

            var normalizedSchemaName = string.IsNullOrWhiteSpace(schemaName) ? "dbo" : schemaName.Trim();
            var normalizedPhysicalTableName = physicalTableName.Trim();

            const string sql = """
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM sys.tables AS t
    INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
    WHERE s.name = @p0
      AND t.name = @p1
) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
""";
            return await dbContext.Database
                .SqlQueryRaw<bool>(sql, normalizedSchemaName, normalizedPhysicalTableName)
                .SingleAsync(cancellationToken);
        }

        /// <summary>
        /// 基于 SQL Server sys.indexes/schemas/tables 探测物理分表缺失索引（仅探测，不执行 DDL）。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="schemaName">schema 名称；为空时回退 dbo。</param>
        /// <param name="physicalTableName">物理表名。</param>
        /// <param name="indexNames">期望存在的索引名集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>缺失索引名集合。</returns>
        public async Task<IReadOnlyList<string>> FindMissingIndexesAsync(
            DbContext dbContext,
            string? schemaName,
            string physicalTableName,
            IReadOnlyList<string> indexNames,
            CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(indexNames);
            if (string.IsNullOrWhiteSpace(physicalTableName)) {
                throw new ArgumentException("物理表名不能为空。", nameof(physicalTableName));
            }

            var normalizedExpectedIndexNames = indexNames
                .Where(static indexName => !string.IsNullOrWhiteSpace(indexName))
                .Select(static indexName => indexName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedExpectedIndexNames.Length == 0) {
                return Array.Empty<string>();
            }

            var normalizedSchemaName = string.IsNullOrWhiteSpace(schemaName) ? "dbo" : schemaName.Trim();
            var normalizedPhysicalTableName = physicalTableName.Trim();

            const string sql = """
SELECT i.name
FROM sys.indexes AS i
INNER JOIN sys.tables AS t ON t.object_id = i.object_id
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name = @p0
  AND t.name = @p1
  AND i.name IS NOT NULL
""";
            var existingIndexNames = await dbContext.Database
                .SqlQueryRaw<string>(sql, normalizedSchemaName, normalizedPhysicalTableName)
                .ToListAsync(cancellationToken);
            var existingIndexSet = existingIndexNames
                .Where(static indexName => !string.IsNullOrWhiteSpace(indexName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return normalizedExpectedIndexNames
                .Where(indexName => !existingIndexSet.Contains(indexName))
                .ToArray();
        }

        /// <summary>
        /// 批量探测 SQL Server 物理分表缺失项（单次查询目标 schema 全量表名后做内存对比）。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="schemaName">schema 名称；为空时默认使用 dbo。</param>
        /// <param name="physicalTableNames">待探测物理表名集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>缺失物理表名集合。</returns>
        public async Task<IReadOnlyList<string>> FindMissingTablesAsync(
            DbContext dbContext,
            string? schemaName,
            IReadOnlyList<string> physicalTableNames,
            CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(physicalTableNames);
            if (physicalTableNames.Count == 0) {
                return Array.Empty<string>();
            }

            var normalizedSchemaName = string.IsNullOrWhiteSpace(schemaName) ? "dbo" : schemaName.Trim();

            var normalizedExpectedTables = physicalTableNames
                .Where(static tableName => !string.IsNullOrWhiteSpace(tableName))
                .Select(static tableName => tableName.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (normalizedExpectedTables.Length == 0) {
                return Array.Empty<string>();
            }

            var existingTables = await dbContext.Database
                .SqlQueryRaw<string>(BatchShardingProbeSql, normalizedSchemaName)
                .ToListAsync(cancellationToken);
            var existingTableSet = existingTables
                .Where(static tableName => !string.IsNullOrWhiteSpace(tableName))
                .ToHashSet(StringComparer.Ordinal);

            return normalizedExpectedTables
                .Where(tableName => !existingTableSet.Contains(tableName))
                .ToArray();
        }

    }
}
