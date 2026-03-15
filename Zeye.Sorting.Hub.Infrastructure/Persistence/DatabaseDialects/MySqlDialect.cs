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

        public IReadOnlyList<string> GetOptionalBootstrapSql() => Array.Empty<string>();

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

        public bool ShouldIgnoreAutoTuningException(Exception exception) {
            return DatabaseProviderExceptionHelper.TryGetProviderErrorNumber(exception, out var errorNumber) && errorNumber == 1061;
        }

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
