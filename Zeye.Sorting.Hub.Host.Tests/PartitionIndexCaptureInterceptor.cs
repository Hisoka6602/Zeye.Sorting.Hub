using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>捕获测试数据库实际执行的索引 DDL，用于证明已建索引不会被重复创建。</summary>
public sealed class PartitionIndexCaptureInterceptor : DbCommandInterceptor {
    /// <summary>成功执行的索引命令。</summary>
    public ConcurrentQueue<string> Commands { get; } = new();

    /// <summary>记录同步索引命令。</summary>
    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result) {
        Capture(command.CommandText);
        return result;
    }

    /// <summary>记录异步索引命令。</summary>
    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default) {
        Capture(command.CommandText);
        return ValueTask.FromResult(result);
    }

    /// <summary>只保存索引维护命令，避免把实体写入计入建索引次数。</summary>
    private void Capture(string commandText) {
        if (commandText.TrimStart().StartsWith("CREATE INDEX", StringComparison.OrdinalIgnoreCase)
            || commandText.TrimStart().StartsWith("DROP INDEX", StringComparison.OrdinalIgnoreCase)) Commands.Enqueue(commandText);
    }
}
