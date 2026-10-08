using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zeye.Sorting.Hub.Tools.DatabaseVerification;

/// <summary>独立性能验收按需保留 EF 生成语句与执行计时，不记录参数值。</summary>
internal sealed class QueryCommandTraceInterceptor : DbCommandInterceptor {
    /// <summary>仅记录当前验收进程发出的命令，正式宿主不注册此拦截器。</summary>
    private readonly List<CommandTrace> _commands = [];
    /// <summary>按 EF 命令编号关联读取结束事件，区分服务器执行和结果消费。</summary>
    private readonly Dictionary<Guid, CommandTrace> _active = [];

    /// <summary>执行完成时记录读取命令，结果仍由真实 EF 读取。</summary>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default) {
        lock (_commands) {
            var trace = new CommandTrace { Sql = command.CommandText, Milliseconds = eventData.Duration.Ticks / (decimal)TimeSpan.TicksPerMillisecond,
                Fields = result.FieldCount };
            _commands.Add(trace); _active[eventData.CommandId] = trace;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>测量真实 EF 读取生命周期，不包装或改写提供器的逐行读取。</summary>
    public override InterceptionResult DataReaderDisposing(DbCommand command, DataReaderDisposingEventData eventData, InterceptionResult result) {
        lock (_commands) if (_active.Remove(eventData.CommandId, out var trace)) {
            trace.ReadMilliseconds = eventData.Duration.Ticks / (decimal)TimeSpan.TicksPerMillisecond; trace.Rows = eventData.ReadCount;
        }
        return result;
    }

    /// <summary>仅在隔离验收结束后写证据，不在业务或查询热路径写文件。</summary>
    internal async Task WriteAsync() {
        var directory = Path.Combine("data", "business-history");
        Directory.CreateDirectory(directory);
        CommandTrace[] snapshot;
        lock (_commands) snapshot = _commands.ToArray();
        await File.WriteAllTextAsync(Path.Combine(directory, "query-command-trace.json"), JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

}
