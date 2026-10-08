using System.Net.Sockets;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NLog;
using Oracle.ManagedDataAccess.Client;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence;

/// <summary>网络故障后的关联连接池清理，共享有界节流及原始异常保护。</summary>
internal static class ConnectionPoolRecovery {
    /// <summary>固定大小节流条带，不累积连接字符串或请求对象，每条带最多五秒清理一次。</summary>
    private static readonly long[] LastRecoveryTicks = new long[32];
    /// <summary>仅记录提供器、错误编号与恢复动作，不记录凭据、连接字符串或 SQL。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>结果读取、事务或连接失败返回前清理关联池，不添加重试，不影响原异常。</summary>
    internal static void Recover(DbContext context, Exception exception) {
        var knownFailure = exception switch {
            SqlException sql => sql.Errors.Cast<SqlError>().Any(error => error.Number is -2 or 20 or 64 or 233 or 10053 or 10054 or 10060),
            OracleException oracle => oracle.Number is 3113 or 3114 or 3135 or 12154 or 12170 or 12535 or 12537 or 12541 or 12570 or 12571,
            _ => false
        };
        if (!knownFailure && (!(exception is SqlException or OracleException) || !HasSocketFailure(exception))) return;
        try {
            var connection = context.Database.GetDbConnection();
            if (connection is not (SqlConnection or OracleConnection)) return;
            var stripe = (uint)StringComparer.Ordinal.GetHashCode(connection.ConnectionString) % (uint)LastRecoveryTicks.Length;
            var now = Environment.TickCount64;
            var previous = Volatile.Read(ref LastRecoveryTicks[stripe]);
            if (previous != 0 && now - previous < 5000 || Interlocked.CompareExchange(ref LastRecoveryTicks[stripe], now, previous) != previous) return;
            // 在用连接归还时淘汰，避免故障后的连接归还再次进行旧地址会话复位。
            if (connection is SqlConnection sqlConnection) SqlConnection.ClearPool(sqlConnection);
            else {
                // 服务名在断网时解析失败后，驱动会缓存失败的描述符；只在该错误的节流恢复中刷新。
                // 正常读写不执行枚举或探针，错误配置仍返回原始异常，不会被当作成功。
                if (exception is OracleException { Number: 12154 }) {
                    using var sources = new OracleDataSourceEnumerator().GetDataSources();
                }
                OracleConnection.ClearPool((OracleConnection)connection);
            }
            Logger.Warn("数据库网络或超时故障后已清理关联连接池，Provider={Provider}, ErrorNumber={ErrorNumber}",
                connection.GetType().Name, exception is SqlException sql ? sql.Number : ((OracleException)exception).Number);
        }
        catch (Exception recoveryFailure) {
            Logger.Warn(recoveryFailure, "失效连接池清理失败，保留原始数据库异常。");
        }
    }

    /// <summary>识别跨平台的内层网络错误，Linux 原生编号不等同于驱动中的 SQL 错误编号。</summary>
    private static bool HasSocketFailure(Exception exception) {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException) {
            if (inner is SocketException { SocketErrorCode: SocketError.ConnectionReset or SocketError.ConnectionAborted
                or SocketError.TimedOut or SocketError.NetworkDown or SocketError.NetworkUnreachable
                or SocketError.HostUnreachable or SocketError.ConnectionRefused or SocketError.NotConnected }) return true;
        }
        return false;
    }
}
