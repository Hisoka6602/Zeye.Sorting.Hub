using System.Diagnostics;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>请求内线程安全地累加全部数据库操作，暴露多条短查询累计变慢和连接等待。</summary>
public sealed class SlowQueryRequestScope : IDisposable {
    /// <summary>当前异步调用链的观测范围。</summary>
    private static readonly AsyncLocal<SlowQueryRequestScope?> CurrentScope = new();
    /// <summary>父范围，嵌套诊断释放时恢复。</summary>
    private readonly SlowQueryRequestScope? _parent;
    /// <summary>没有活动追踪时沿用请求审计标识，防止读取器样本无法关联。</summary>
    internal string TraceId { get; }
    /// <summary>数据库操作累计计数。</summary>
    private long _commands;
    /// <summary>命令与实际提供器读调用的累计计时刻度。</summary>
    private long _databaseTicks;
    /// <summary>连接打开累计计时刻度。</summary>
    private long _connectionTicks;
    /// <summary>实际读取累计行数。</summary>
    private long _rows;
    /// <summary>创建范围并绑定当前异步调用链。</summary>
    private SlowQueryRequestScope(string traceId) { TraceId = traceId; _parent = CurrentScope.Value; CurrentScope.Value = this; }
    /// <summary>开始新的请求数据库观测范围。</summary>
    public static SlowQueryRequestScope Begin(string traceId = "") => new(traceId);
    /// <summary>获取发起操作时的范围，读取器完成后仍写入原始请求。</summary>
    internal static SlowQueryRequestScope? Capture() => CurrentScope.Value;
    /// <summary>记录一次实际命令；并行命令使用累计耗时，不能当作请求墙钟耗时相减。</summary>
    internal void RecordCommand(TimeSpan databaseElapsed, long rows) {
        Interlocked.Increment(ref _commands); Interlocked.Add(ref _databaseTicks, databaseElapsed.Ticks); Interlocked.Add(ref _rows, rows);
    }
    /// <summary>记录连接等待，不伪装成 SQL 执行。</summary>
    internal void RecordConnection(TimeSpan elapsed) => Interlocked.Add(ref _connectionTicks, elapsed.Ticks);
    /// <summary>累计事务动作等待，但不增加 SQL 命令次数。</summary>
    internal void RecordTransaction(TimeSpan elapsed) => Interlocked.Add(ref _databaseTicks, elapsed.Ticks);
    /// <summary>构造请求汇总快照，仅访问内存。</summary>
    public SlowQueryObservation Snapshot(string traceId) => new() {
        Kind = "request", TraceId = traceId, SpanId = Activity.Current?.SpanId.ToString() ?? "",
        ExecuteMilliseconds = Interlocked.Read(ref _databaseTicks) / (decimal)TimeSpan.TicksPerMillisecond,
        ConnectionMilliseconds = Interlocked.Read(ref _connectionTicks) / (decimal)TimeSpan.TicksPerMillisecond,
        CommandCount = Interlocked.Read(ref _commands), RowsRead = Interlocked.Read(ref _rows)
    };
    /// <summary>恢复父范围，不触发数据库或文件操作。</summary>
    public void Dispose() => CurrentScope.Value = _parent;
}
