using System.Data;
using System.Data.Common;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// 数据库连接打开协调器。
    /// </summary>
    internal static class DatabaseConnectionOpenCoordinator {
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
                await connection.OpenAsync(cancellationToken);
            }
        }
    }
}
