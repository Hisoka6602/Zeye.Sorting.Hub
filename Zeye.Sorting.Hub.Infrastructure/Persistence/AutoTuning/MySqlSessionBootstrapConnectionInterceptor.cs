using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;
using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning {

    /// <summary>MySQL 连接打开时会话级参数初始化拦截器</summary>
    public sealed class MySqlSessionBootstrapConnectionInterceptor : DbConnectionInterceptor {
        /// <summary>
        /// MySQL 会话级参数初始化 SQL 语句集合。
        /// </summary>
        private static readonly string[] SessionSql = {
            "SET SESSION optimizer_switch='index_merge=on,index_condition_pushdown=on,derived_merge=on'"
        };

        /// <summary>
        /// NLog 静态日志器实例，用于记录会话初始化执行异常。
        /// </summary>
        private static readonly ILogger NLogLogger = LogManager.GetCurrentClassLogger();

        /// <summary>连接打开后同步执行会话初始化 SQL。</summary>
        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) {
            if (connection is not MySqlConnection) {
                return;
            }

            ApplySessionSql(connection);
        }

        /// <summary>连接打开后异步执行会话初始化 SQL。</summary>
        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (connection is not MySqlConnection) {
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
                    NLogLogger.Warn(ex, "MySQL 会话初始化 SQL 执行失败，已降级忽略，Sql={Sql}", sql);
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
                    NLogLogger.Warn(ex, "MySQL 会话初始化 SQL 执行失败，已降级忽略，Sql={Sql}", sql);
                }
            }
        }

    }
}
