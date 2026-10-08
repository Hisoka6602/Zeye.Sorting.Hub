using System.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>一次命令的不可变身份与读取计时，完成或失败只发布一个样本。</summary>
internal sealed class SlowQueryExecution {
    /// <summary>发起操作时的 SQL 快照，不保留可被释放的命令对象。</summary>
    private readonly string _sql;
    /// <summary>采集管线。</summary>
    private readonly SlowQueryAutoTuningPipeline _pipeline;
    /// <summary>原始追踪与提供器标识。</summary>
    private readonly SlowQueryObservation _identity;
    /// <summary>原始请求数据库观测范围。</summary>
    private readonly SlowQueryRequestScope? _request;
    /// <summary>返回读取器时已测得的执行耗时。</summary>
    private readonly TimeSpan _execute;
    /// <summary>读取器开始存续的单调时间戳。</summary>
    private readonly long _started = Stopwatch.GetTimestamp();
    /// <summary>提供器实际读调用累计计时刻度。</summary>
    private long _readTicks;
    /// <summary>实际成功读取行数。</summary>
    private long _rows;
    /// <summary>发布互斥标记。</summary>
    private int _completed;
    /// <summary>本次执行的身份快照。</summary>
    internal SlowQueryExecution(SlowQueryAutoTuningPipeline pipeline, string sql, TimeSpan execute, string provider, string commandId, string databaseRole = "business") {
        _pipeline = pipeline; _sql = sql; _execute = execute; _request = SlowQueryRequestScope.Capture();
        _identity = new() { Provider = provider, CommandId = commandId, DatabaseRole = databaseRole,
            TraceId = Activity.Current?.TraceId.ToString() ?? _request?.TraceId ?? "", SpanId = Activity.Current?.SpanId.ToString() ?? "" };
    }
    /// <summary>累加实际读调用时间及成功读取行。</summary>
    internal void ReadFinished(long timestamp, bool row = false) {
        Interlocked.Add(ref _readTicks, Stopwatch.GetElapsedTime(timestamp).Ticks);
        if (row) Interlocked.Increment(ref _rows);
    }
    /// <summary>读取器关闭、失败或取消时恰好发布一次，原始异常继续由调用方抛出。</summary>
    internal void Complete(Exception? exception = null, bool partial = false, int affectedRows = 0) {
        if (Interlocked.Exchange(ref _completed, 1) != 0) return;
        _pipeline.Finished(_identity.CommandId);
        var read = TimeSpan.FromTicks(Interlocked.Read(ref _readTicks));
        var lifetime = Stopwatch.GetElapsedTime(_started);
        var rows = Interlocked.Read(ref _rows);
        var observation = _identity with {
            ExecuteMilliseconds = _execute.Ticks / (decimal)TimeSpan.TicksPerMillisecond,
            ReadMilliseconds = read.Ticks / (decimal)TimeSpan.TicksPerMillisecond,
            ConsumerMilliseconds = Math.Max(0m, (lifetime - read).Ticks / (decimal)TimeSpan.TicksPerMillisecond),
            RowsRead = rows, CommandCount = 1, IsPartialRead = partial, IsCanceled = exception is OperationCanceledException
        };
        _request?.RecordCommand(_execute + read, rows);
        _pipeline.Collect(_sql, _execute + lifetime, affectedRows, exception, observation);
    }
}
