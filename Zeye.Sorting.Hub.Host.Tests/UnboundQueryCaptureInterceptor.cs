using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>采集实际执行的处理记录查询和参数，用于验证窄查询、读取边界及真实执行计划。</summary>
public sealed class UnboundQueryCaptureInterceptor : DbCommandInterceptor {
    /// <summary>仅保存处理记录 SELECT，排除造数命令和其他测试数据。</summary>
    public ConcurrentQueue<(string Sql, (string Name, object Value, DbType Type)[] Parameters)> Commands { get; } = new();

    /// <summary>同步读取命令执行前保存查询。</summary>
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result) {
        Capture(command);
        return result;
    }

    /// <summary>异步读取命令执行前保存查询。</summary>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
        Capture(command);
        return ValueTask.FromResult(result);
    }

    /// <summary>复制参数值，避免执行计划验证复用已释放命令中的参数实例。</summary>
    private void Capture(DbCommand command) {
        if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
            && command.CommandText.Contains("Parcel_ProcessingRecords", StringComparison.Ordinal))
            Commands.Enqueue((command.CommandText, command.Parameters.Cast<DbParameter>()
                .Select(parameter => (parameter.ParameterName, parameter.Value ?? DBNull.Value, parameter.DbType)).ToArray()));
    }
}
