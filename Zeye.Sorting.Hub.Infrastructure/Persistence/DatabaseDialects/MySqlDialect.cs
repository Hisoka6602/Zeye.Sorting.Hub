using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>MySQL 方言</summary>
    public sealed class MySqlDialect : IDatabaseDialect, IBatchShardingPhysicalTableProbe {
        /// <summary>当前方言提供器名称。</summary>
        public string ProviderName => "MySQL";

        /// <summary>返回可选初始化 SQL（MySQL 当前无需启动期 SQL）。</summary>
        public IReadOnlyList<string> GetOptionalBootstrapSql() => Array.Empty<string>();

        /// <summary>根据慢查询 where 列生成 MySQL 自动调优 SQL。</summary>
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

            var indexName = DatabaseProviderExceptionHelper.BuildIndexName(normalizedSchemaName, normalizedTableName, indexColumns, 60);

            var escapedTable = string.IsNullOrWhiteSpace(normalizedSchemaName)
                ? $"`{normalizedTableName}`"
                : $"`{normalizedSchemaName}`.`{normalizedTableName}`";
            var escapedColumns = string.Join(", ", indexColumns.Select(static col => $"`{col}`"));
            var escapedIndexName = $"`{indexName}`";

            return new[] {
                $"CREATE INDEX {escapedIndexName} ON {escapedTable} ({escapedColumns})",
                $"ANALYZE TABLE {escapedTable}"
            };
        }

        /// <summary>判断异常是否可被视为“已存在”并忽略。</summary>
        public bool ShouldIgnoreAutoTuningException(Exception exception) {
            return DatabaseProviderExceptionHelper.TryGetProviderErrorNumber(exception, out var errorNumber) && errorNumber == 1061;
        }

        /// <summary>生成闭环自治维护 SQL（高峰/高风险仅执行轻量动作）。</summary>
        public IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk) {
            if (string.IsNullOrWhiteSpace(tableName)) {
                return Array.Empty<string>();
            }

            var normalizedSchemaName = schemaName?.Trim();
            var normalizedTableName = tableName.Trim();
            var escapedTable = string.IsNullOrWhiteSpace(normalizedSchemaName)
                ? $"`{normalizedTableName}`"
                : $"`{normalizedSchemaName}`.`{normalizedTableName}`";
            var analyzeSql = $"ANALYZE TABLE {escapedTable}";

            if (inPeakWindow || highRisk) {
                return new[] { analyzeSql };
            }

            return new[] { analyzeSql, $"OPTIMIZE TABLE {escapedTable}" };
        }

        /// <summary>
        /// 基于 MySQL INFORMATION_SCHEMA.TABLES 判断物理分表是否存在。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="schemaName">schema 名称；为空时回退当前数据库。</param>
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

            var normalizedSchemaName = string.IsNullOrWhiteSpace(schemaName) ? string.Empty : schemaName.Trim();
            var normalizedPhysicalTableName = physicalTableName.Trim();

            const string sql = """
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = COALESCE(NULLIF(@p0, ''), DATABASE())
      AND TABLE_NAME = @p1
) THEN TRUE ELSE FALSE END
""";
            return await dbContext.Database
                .SqlQueryRaw<bool>(sql, normalizedSchemaName, normalizedPhysicalTableName)
                .SingleAsync(cancellationToken);
        }

        /// <summary>
        /// 批量探测 MySQL 物理分表缺失项（单次查询当前 schema 全量表名后做内存对比）。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="physicalTableNames">待探测物理表名集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>缺失物理表名集合。</returns>
        public async Task<IReadOnlyList<string>> FindMissingTablesAsync(
            DbContext dbContext,
            IReadOnlyList<string> physicalTableNames,
            CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(dbContext);
            ArgumentNullException.ThrowIfNull(physicalTableNames);
            if (physicalTableNames.Count == 0) {
                return Array.Empty<string>();
            }

            var normalizedExpectedTables = physicalTableNames
                .Where(static tableName => !string.IsNullOrWhiteSpace(tableName))
                .Select(static tableName => tableName.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (normalizedExpectedTables.Length == 0) {
                return Array.Empty<string>();
            }

            const string sql = """
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = DATABASE()
""";
            var existingTables = await dbContext.Database
                .SqlQueryRaw<string>(sql)
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
