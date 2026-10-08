using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>保存包裹与投影队列实际读取命令，用于验证读取边界和执行计划。</summary>
public sealed class SlowQueryReadCaptureInterceptor : DbCommandInterceptor {
    /// <summary>复制参数值，执行计划检查不依赖已释放的命令对象。</summary>
    public ConcurrentQueue<(string Sql, (string Name, object Value, DbType Type)[] Parameters)> Commands { get; } = new();

    /// <summary>同步读取前复制业务查询。</summary>
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result) {
        Capture(command);
        return result;
    }

    /// <summary>异步读取前复制业务查询。</summary>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    /// <summary>仅捕获目标 SELECT，排除初始化、写入和其他业务内容。</summary>
    private void Capture(DbCommand command) {
        if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && (command.CommandText.Contains("Parcels", StringComparison.Ordinal)
                || command.CommandText.Contains("FusionFactReceipts", StringComparison.Ordinal)))
            Commands.Enqueue((command.CommandText, command.Parameters.Cast<DbParameter>()
                .Select(parameter => (parameter.ParameterName, parameter.Value ?? DBNull.Value, parameter.DbType)).ToArray()));
    }
}
