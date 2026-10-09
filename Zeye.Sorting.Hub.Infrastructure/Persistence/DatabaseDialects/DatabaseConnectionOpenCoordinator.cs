using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// 数据库连接打开协调器。
    /// </summary>
    public static class DatabaseConnectionOpenCoordinator {
        /// <summary>启动前验证管理连接；允许创建的 SQLite 缺失文件留给受治理的初始化流程处理，探测不创建文件。</summary>
        public static async Task ProbeAdministrationConnectionAsync(IDatabaseDialect dialect, string connectionString, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            await using var connection = dialect.CreateAdministrationConnection(connectionString);
            if (connection is SqliteConnection sqlite
                && new SqliteConnectionStringBuilder(sqlite.ConnectionString).Mode == SqliteOpenMode.ReadWriteCreate
                && !await dialect.DatabaseExistsAsync(connection, dialect.ExtractDatabaseName(connectionString), cancellationToken)) {
                return;
            }
            if (connection is SqliteConnection) await EnsureOpenedAsync(connection, cancellationToken);
            else await dialect.DatabaseExistsAsync(connection, dialect.ExtractDatabaseName(connectionString), cancellationToken);
        }

        /// <summary>
        /// 确保连接处于可用打开状态。
        /// </summary>
        /// <param name="connection">数据库连接。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        internal static async Task EnsureOpenedAsync(DbConnection connection, CancellationToken cancellationToken) {
            ArgumentNullException.ThrowIfNull(connection);

            if (connection.State == ConnectionState.Open) {
                return;
            }

            if (connection.State == ConnectionState.Broken) {
                connection.Close();
            }

            if (connection.State != ConnectionState.Open) {
                await SlowQueryDbOperations.OpenAsync(connection, cancellationToken);
            }
        }
    }
}
