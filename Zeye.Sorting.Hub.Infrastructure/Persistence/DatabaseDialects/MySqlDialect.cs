using System;
using System.Linq;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

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

        /// <summary>
        /// 从连接字符串提取目标数据库名。
        /// </summary>
        /// <param name="connectionString">原始连接字符串。</param>
        /// <returns>目标数据库名。</returns>
        public string ExtractDatabaseName(string connectionString) {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            return DatabaseIdentifierGuard.NormalizeDatabaseName(builder.Database, nameof(connectionString));
        }

        /// <summary>
        /// 基于业务连接字符串构建服务器级管理连接（不指定 Database）。
        /// </summary>
        /// <param name="connectionString">原始连接字符串。</param>
        /// <returns>服务器级连接。</returns>
        public DbConnection CreateAdministrationConnection(string connectionString) {
            var builder = new MySqlConnectionStringBuilder(connectionString) {
                Database = string.Empty
            };
            return new MySqlConnection(builder.ConnectionString);
        }

        /// <summary>
        /// 探测目标数据库是否存在。
        /// </summary>
        /// <param name="administrationConnection">服务器级连接。</param>
        /// <param name="databaseName">目标数据库名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>存在返回 true，否则 false。</returns>
        public async Task<bool> DatabaseExistsAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(administrationConnection);
            var normalizedDatabaseName = DatabaseIdentifierGuard.NormalizeDatabaseName(databaseName, nameof(databaseName));

            await EnsureConnectionOpenedAsync(administrationConnection, cancellationToken);
            await using var command = administrationConnection.CreateCommand();
            command.CommandText = """
SELECT CASE WHEN EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.SCHEMATA
    WHERE SCHEMA_NAME = @databaseName
) THEN TRUE ELSE FALSE END
""";
            var databaseNameParameter = command.CreateParameter();
            databaseNameParameter.ParameterName = "@databaseName";
            databaseNameParameter.DbType = DbType.String;
            databaseNameParameter.Value = normalizedDatabaseName;
            command.Parameters.Add(databaseNameParameter);
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            return scalar is true || (scalar is bool value && value);
        }

        /// <summary>
        /// 创建目标数据库（MySQL 幂等语义：CREATE DATABASE IF NOT EXISTS）。
        /// </summary>
        /// <param name="administrationConnection">服务器级连接。</param>
        /// <param name="databaseName">目标数据库名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public async Task CreateDatabaseAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(administrationConnection);
            var normalizedDatabaseName = DatabaseIdentifierGuard.NormalizeDatabaseName(databaseName, nameof(databaseName));
            var escapedDatabaseName = DatabaseIdentifierGuard.EscapeMySqlIdentifier(normalizedDatabaseName);

            await EnsureConnectionOpenedAsync(administrationConnection, cancellationToken);
            await using var command = administrationConnection.CreateCommand();
            command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{escapedDatabaseName}`";
            _ = await command.ExecuteNonQueryAsync(cancellationToken);
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
        /// 基于 MySQL INFORMATION_SCHEMA.STATISTICS 探测物理分表缺失索引（仅探测，不执行 DDL）。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="schemaName">schema 名称；为空时回退当前数据库。</param>
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

            var normalizedSchemaName = string.IsNullOrWhiteSpace(schemaName) ? string.Empty : schemaName.Trim();
            var normalizedPhysicalTableName = physicalTableName.Trim();

            const string sql = """
SELECT DISTINCT INDEX_NAME
FROM INFORMATION_SCHEMA.STATISTICS
WHERE TABLE_SCHEMA = COALESCE(NULLIF(@p0, ''), DATABASE())
  AND TABLE_NAME = @p1
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
        /// 批量探测 MySQL 物理分表缺失项（单次查询目标 schema 全量表名后做内存对比；为空回退当前数据库）。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="schemaName">schema 名称；为空时默认使用当前数据库。</param>
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

            var normalizedSchemaName = string.IsNullOrWhiteSpace(schemaName) ? null : schemaName.Trim();

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
WHERE TABLE_SCHEMA = COALESCE(NULLIF(@p0, ''), DATABASE())
""";
            var existingTables = await dbContext.Database
                .SqlQueryRaw<string>(sql, normalizedSchemaName ?? string.Empty)
                .ToListAsync(cancellationToken);
            var existingTableSet = existingTables
                .Where(static tableName => !string.IsNullOrWhiteSpace(tableName))
                .ToHashSet(StringComparer.Ordinal);

            return normalizedExpectedTables
                .Where(tableName => !existingTableSet.Contains(tableName))
                .ToArray();
        }

        /// <summary>
        /// 按逻辑基础表名前缀列出已存在的物理分表名。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="schemaName">schema 名称；为空时默认使用当前数据库。</param>
        /// <param name="baseTableName">逻辑基础表名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>已存在物理分表名集合。</returns>
        public async Task<IReadOnlyList<string>> ListPhysicalTablesByBaseNameAsync(
            DbContext dbContext,
            string? schemaName,
            string baseTableName,
            CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(dbContext);
            if (string.IsNullOrWhiteSpace(baseTableName)) {
                throw new ArgumentException("逻辑基础表名不能为空。", nameof(baseTableName));
            }

            var normalizedSchemaName = string.IsNullOrWhiteSpace(schemaName) ? null : schemaName.Trim();
            var normalizedBaseTableName = baseTableName.Trim();
            var likePattern = $"{normalizedBaseTableName}\\_%";

            const string sql = """
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = COALESCE(NULLIF(@p0, ''), DATABASE())
  AND TABLE_NAME LIKE @p1 ESCAPE '\'
""";
            var tableNames = await dbContext.Database
                .SqlQueryRaw<string>(sql, normalizedSchemaName ?? string.Empty, likePattern)
                .ToListAsync(cancellationToken);
            return tableNames
                .Where(static tableName => !string.IsNullOrWhiteSpace(tableName))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 确保连接处于打开状态。
        /// </summary>
        /// <param name="connection">数据库连接。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        private static async Task EnsureConnectionOpenedAsync(DbConnection connection, CancellationToken cancellationToken) {
            if (connection.State == ConnectionState.Open) {
                return;
            }

            await connection.OpenAsync(cancellationToken);
        }

    }
}
