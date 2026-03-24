using System;
using System.Linq;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

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

        /// <summary>
        /// 从连接字符串提取目标数据库名。
        /// </summary>
        /// <param name="connectionString">原始连接字符串。</param>
        /// <returns>目标数据库名。</returns>
        public string ExtractDatabaseName(string connectionString) {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return DatabaseIdentifierGuard.NormalizeDatabaseName(builder.InitialCatalog, nameof(connectionString));
        }

        /// <summary>
        /// 基于业务连接字符串构建服务器级管理连接（切换到 master）。
        /// </summary>
        /// <param name="connectionString">原始连接字符串。</param>
        /// <returns>服务器级连接。</returns>
        public DbConnection CreateAdministrationConnection(string connectionString) {
            var builder = new SqlConnectionStringBuilder(connectionString) {
                InitialCatalog = "master"
            };
            return new SqlConnection(builder.ConnectionString);
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

            await DatabaseConnectionOpenHelper.EnsureOpenedAsync(administrationConnection, cancellationToken);
            await using var command = administrationConnection.CreateCommand();
            command.CommandText = "SELECT CASE WHEN DB_ID(@databaseName) IS NULL THEN CAST(0 AS bit) ELSE CAST(1 AS bit) END";
            var databaseNameParameter = command.CreateParameter();
            databaseNameParameter.ParameterName = "@databaseName";
            databaseNameParameter.DbType = DbType.String;
            databaseNameParameter.Value = normalizedDatabaseName;
            command.Parameters.Add(databaseNameParameter);
            var scalar = await command.ExecuteScalarAsync(cancellationToken);
            return scalar is true || (scalar is bool value && value);
        }

        /// <summary>
        /// 创建目标数据库（SQL Server 条件建库语义）。
        /// </summary>
        /// <param name="administrationConnection">服务器级连接。</param>
        /// <param name="databaseName">目标数据库名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        public async Task CreateDatabaseAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(administrationConnection);
            var normalizedDatabaseName = DatabaseIdentifierGuard.NormalizeDatabaseName(databaseName, nameof(databaseName));

            await DatabaseConnectionOpenHelper.EnsureOpenedAsync(administrationConnection, cancellationToken);
            await using var command = administrationConnection.CreateCommand();
            command.CommandText = """
IF DB_ID(@databaseName) IS NULL
BEGIN
    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@databaseName);
    EXEC (@sql);
END
""";
            var databaseNameParameter = command.CreateParameter();
            databaseNameParameter.ParameterName = "@databaseName";
            databaseNameParameter.DbType = DbType.String;
            databaseNameParameter.Value = normalizedDatabaseName;
            command.Parameters.Add(databaseNameParameter);
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

        /// <summary>
        /// 按逻辑基础表名前缀列出已存在的物理分表名。
        /// </summary>
        /// <param name="dbContext">数据库上下文。</param>
        /// <param name="schemaName">schema 名称；为空时默认使用 dbo。</param>
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

            var normalizedSchemaName = string.IsNullOrWhiteSpace(schemaName) ? "dbo" : schemaName.Trim();
            var normalizedBaseTableName = baseTableName.Trim();
            var likePattern = $"{EscapeSqlServerLikePattern(normalizedBaseTableName)}\\_%";

            const string sql = """
SELECT t.name
FROM sys.tables AS t
INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
WHERE s.name = @p0
  AND t.name LIKE @p1 ESCAPE '\'
""";
            var tableNames = await dbContext.Database
                .SqlQueryRaw<string>(sql, normalizedSchemaName, likePattern)
                .ToListAsync(cancellationToken);
            return tableNames
                .Where(static tableName => !string.IsNullOrWhiteSpace(tableName))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 转义 SQL Server LIKE 模式中的通配符与转义符本身。
        /// </summary>
        /// <param name="pattern">原始模式文本。</param>
        /// <returns>可安全用于 LIKE + ESCAPE '\' 的文本。</returns>
        private static string EscapeSqlServerLikePattern(string pattern) {
            return pattern
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal)
                .Replace("[", "\\[", StringComparison.Ordinal);
        }

    }
}
