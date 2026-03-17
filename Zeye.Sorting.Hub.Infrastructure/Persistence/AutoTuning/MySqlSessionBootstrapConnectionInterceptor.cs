using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>MySQL 连接打开时会话级参数初始化拦截器</summary>
    public sealed class MySqlSessionBootstrapConnectionInterceptor : DbConnectionInterceptor {
        private static readonly string[] SessionSql = {
            "SET SESSION optimizer_switch='index_merge=on,index_condition_pushdown=on,derived_merge=on'",
            "SET SESSION innodb_stats_on_metadata=OFF"
        };

        /// <summary>
        /// 字段：_logger。
        /// </summary>
        private readonly ILogger<MySqlSessionBootstrapConnectionInterceptor> _logger;

        /// <summary>初始化 MySQL 会话级参数拦截器。</summary>
        public MySqlSessionBootstrapConnectionInterceptor(ILogger<MySqlSessionBootstrapConnectionInterceptor> logger) {
            _logger = logger;
        }

        /// <summary>连接打开后同步执行会话初始化 SQL。</summary>
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) {
            if (!IsMySqlConnection(connection)) {
                return;
            }

            ApplySessionSql(connection);
        }

        /// <summary>连接打开后异步执行会话初始化 SQL。</summary>
        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (!IsMySqlConnection(connection)) {
                return;
            }

            await ApplySessionSqlAsync(connection, cancellationToken);
        }

        /// <summary>同步执行会话初始化 SQL，失败时降级忽略。</summary>
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

        /// <summary>异步执行会话初始化 SQL，失败时降级忽略。</summary>
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

        /// <summary>判断当前连接是否为 MySQL 驱动连接。</summary>
        private static bool IsMySqlConnection(DbConnection connection) {
            return connection is MySqlConnection;
        }
    }
}
