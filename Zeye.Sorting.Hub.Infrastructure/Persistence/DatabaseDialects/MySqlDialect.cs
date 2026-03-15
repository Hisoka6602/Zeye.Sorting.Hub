using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>MySQL 方言</summary>
    public sealed class MySqlDialect : IDatabaseDialect {
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
            var indexColumns = whereColumns
                .Where(static c => !string.IsNullOrWhiteSpace(c))
                .Take(3)
                .Select(static c => c.Trim())
                .ToArray();

            if (indexColumns.Length == 0) {
                return Array.Empty<string>();
            }

            var indexName = BuildIndexName(normalizedSchemaName, normalizedTableName, indexColumns, 60);

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

        /// <summary>构造长度受限且稳定的自动索引名称。</summary>
        private static string BuildIndexName(string? schemaName, string tableName, IReadOnlyList<string> columns, int maxLength) {
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
