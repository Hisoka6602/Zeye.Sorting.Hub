namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>命令、读取、连接和请求的分阶段诊断；只保存标识与计数，不保存参数值或连接密钥。</summary>
public sealed record SlowQueryObservation {
    /// <summary>已确认业务调用失败，避免错误被外层响应处理后丢失。</summary>
    public bool IsFailed { get; init; }
    /// <summary>异常类型，不保存可能包含业务数据的异常消息。</summary>
    public string ExceptionType { get; init; } = "";
    /// <summary>数据库用途，配置历史等辅助库的 SQL 不能驱动业务库自动调优。</summary>
    public string DatabaseRole { get; init; } = "business";
    /// <summary>请求响应状态码，SQL 和连接观测不填写。</summary>
    public int StatusCode { get; init; }
    /// <summary>诊断类别：query、connection、transaction 或 request。</summary>
    public string Kind { get; init; } = "query";
    /// <summary>提供器名称。</summary>
    public string Provider { get; init; } = "";
    /// <summary>发起操作时的请求追踪标识。</summary>
    public string TraceId { get; init; } = "";
    /// <summary>发起操作时的跨度标识。</summary>
    public string SpanId { get; init; } = "";
    /// <summary>一次实际执行的标识；重试有不同标识。</summary>
    public string CommandId { get; init; } = "";
    /// <summary>执行至返回读取器或标量的耗时。</summary>
    public decimal ExecuteMilliseconds { get; init; }
    /// <summary>提供器读取、切换结果集和释放结果的累计耗时。</summary>
    public decimal ReadMilliseconds { get; init; }
    /// <summary>读取器存续期间，提供器读调用之外的消费时间。</summary>
    public decimal ConsumerMilliseconds { get; init; }
    /// <summary>连接打开的累计耗时，包含连接池等待。</summary>
    public decimal ConnectionMilliseconds { get; init; }
    /// <summary>实际读取的行数，不使用受影响行数替代。</summary>
    public long RowsRead { get; init; }
    /// <summary>请求中实际完成的命令数量，包含未达到单条慢查询阈值的命令。</summary>
    public long CommandCount { get; init; }
    /// <summary>是否在执行、读取或释放时取消。</summary>
    public bool IsCanceled { get; init; }
    /// <summary>是否在尚未读完全部结果时提前释放。</summary>
    public bool IsPartialRead { get; init; }
}
