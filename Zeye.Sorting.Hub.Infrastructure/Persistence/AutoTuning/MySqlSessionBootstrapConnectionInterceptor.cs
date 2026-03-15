using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>
    /// MySQL 连接打开时会话级参数初始化拦截器
    /// </summary>
    public sealed class MySqlSessionBootstrapConnectionInterceptor : DbConnectionInterceptor {
        private static readonly string[] SessionSql = {
            "SET SESSION optimizer_switch='index_merge=on,index_condition_pushdown=on,derived_merge=on'",
            "SET SESSION innodb_stats_on_metadata=OFF"
        };

        private readonly ILogger<MySqlSessionBootstrapConnectionInterceptor> _logger;

        public MySqlSessionBootstrapConnectionInterceptor(ILogger<MySqlSessionBootstrapConnectionInterceptor> logger) {
            _logger = logger;
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) {
            if (!IsMySqlConnection(connection)) {
                return;
            }

            ApplySessionSql(connection);
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (!IsMySqlConnection(connection)) {
                return;
            }

            await ApplySessionSqlAsync(connection, cancellationToken);
        }

        private void ApplySessionSql(DbConnection connection) {
            foreach (var sql in SessionSql) {
                try {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
                catch (Exception ex) {
                    _logger.LogWarning(ex, "MySQL 会话初始化 SQL 执行失败，已降级忽略，Sql={Sql}", sql);
                }
            }
        }

        private async Task ApplySessionSqlAsync(DbConnection connection, CancellationToken cancellationToken) {
            foreach (var sql in SessionSql) {
                try {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception ex) {
                    _logger.LogWarning(ex, "MySQL 会话初始化 SQL 执行失败，已降级忽略，Sql={Sql}", sql);
                }
            }
        }

        private static bool IsMySqlConnection(DbConnection connection) {
            return connection is MySqlConnection;
        }
    }
}
