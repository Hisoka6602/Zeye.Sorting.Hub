using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>复制 EF 实际生成的统计命令与参数，用于执行计划检查和数据库往返次数断言。</summary>
internal sealed class StatisticsCaptureInterceptor : DbCommandInterceptor {
    /// <summary>只记录处理统计读取，不包含初始化或写入命令。</summary>
    public ConcurrentQueue<(string Sql, (string Name, object Value, DbType Type)[] Parameters)> Commands { get; } = new();

    /// <summary>异步读取前保存与实际查询完全一致的参数。</summary>
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
        if (command.CommandText.Contains("Parcel_ProcessingRecords", StringComparison.Ordinal)
            && command.CommandText.Contains("COUNT", StringComparison.Ordinal))
            Commands.Enqueue((command.CommandText, command.Parameters.Cast<DbParameter>()
                .Select(parameter => (parameter.ParameterName, parameter.Value ?? DBNull.Value, parameter.DbType)).ToArray()));
        return ValueTask.FromResult(result);
    }
}
