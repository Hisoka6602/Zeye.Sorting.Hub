using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>在窄表已读完、缺失原事实开始读取前插入投影，验证后台补齐竞态不会漏算。</summary>
public sealed class DwsBackfillDuringReadInterceptor : DbCommandInterceptor {
    /// <summary>在本次实际原事实回退命令前执行一次真实补齐。</summary>
    public Func<CancellationToken, Task>? BackfillAsync { get; set; }
    /// <summary>已触发标记，避免补齐所用的查询再次进入回调。</summary>
    private int _triggered;
    /// <summary>确认测试确实走到了预期竞态窗口。</summary>
    public bool Triggered => Volatile.Read(ref _triggered) != 0;

    /// <summary>目标命令只包含缺失主键的量测标量查询，不拦截造数、候选主键或检测读取。</summary>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
        if (BackfillAsync is not null && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)
            && command.CommandText.Contains("Parcel_ProcessingRecords", StringComparison.Ordinal)
            && command.CommandText.Contains("WeightGrams", StringComparison.Ordinal)
            && command.Parameters.Cast<DbParameter>().Any(parameter => parameter.ParameterName.Contains("rawKeys", StringComparison.Ordinal))
            && Interlocked.CompareExchange(ref _triggered, 1, 0) == 0)
            await BackfillAsync(cancellationToken);
        return result;
    }
}
