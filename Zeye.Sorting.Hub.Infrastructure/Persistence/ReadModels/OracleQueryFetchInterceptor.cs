using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Oracle.ManagedDataAccess.Client;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>EF Oracle 查询按行元数据批量取数，减少大结果集网络往返，不改变 SQL、LOB 或重试语义。</summary>
internal sealed class OracleQueryFetchInterceptor : DbCommandInterceptor {
    /// <summary>一次网络取数的目标行数，不限制最终结果集行数。</summary>
    internal const int TargetRows = 1024;
    /// <summary>额外驱动缓冲最多两 MiB；已有更大的显式提供器配置保持不变。</summary>
    internal const long MaximumFetchBytes = 2 * 1024 * 1024;

    /// <summary>在正数行元数据上计算有界缓冲，避免溢出、缩减既有配置或放大单行标量查询。</summary>
    internal static long FetchBytes(long current, long rowSize) => rowSize <= 0 ? current
        : Math.Max(current, rowSize > MaximumFetchBytes / TargetRows ? MaximumFetchBytes : rowSize * TargetRows);

    /// <summary>必须在诊断包装读取器之前、首次 Read 之前设置批量大小，仅处理 EF LINQ 查询。</summary>
    private static DbDataReader Configure(DbCommand command, CommandExecutedEventData data, DbDataReader reader) {
        if (data.CommandSource == CommandSource.LinqQuery && command is OracleCommand oracle && reader is OracleDataReader rows)
            rows.FetchSize = FetchBytes(rows.FetchSize, oracle.RowSize);
        return reader;
    }

    /// <inheritdoc />
    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result) => Configure(command, eventData, result);

    /// <inheritdoc />
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default) => ValueTask.FromResult(Configure(command, eventData, result));
}
