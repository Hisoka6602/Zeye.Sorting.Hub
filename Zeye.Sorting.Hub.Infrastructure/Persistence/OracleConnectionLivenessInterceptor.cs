using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Oracle.ManagedDataAccess.Client;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence;

/// <summary>在创建 Oracle 物理连接之前启用 TCP 存活探测，识别断网后的失效连接。</summary>
public sealed class OracleConnectionLivenessInterceptor : DbConnectionInterceptor {
    /// <summary>无请求状态的共享拦截器，供池化上下文和分表上下文复用。</summary>
    internal static readonly OracleConnectionLivenessInterceptor Instance = new();

    /// <summary>同步连接建立前应用相同的探测策略。</summary>
    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) {
        ConfigureConnection(connection);
        return result;
    }

    /// <summary>异步连接建立前应用相同的探测策略，不添加数据库往返。</summary>
    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default) {
        ConfigureConnection(connection);
        return ValueTask.FromResult(result);
    }

    /// <summary>空闲 30 秒开始探测，未确认探测间隔 3 秒；管理连接也使用相同策略。</summary>
    internal static void ConfigureConnection(DbConnection connection) {
        if (connection is not OracleConnection oracle) return;
        oracle.KeepAlive = true;
        oracle.KeepAliveTime = 30;
        oracle.KeepAliveInterval = 3;
    }
}
