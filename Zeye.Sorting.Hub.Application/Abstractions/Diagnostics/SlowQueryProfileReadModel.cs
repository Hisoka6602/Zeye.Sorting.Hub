namespace Zeye.Sorting.Hub.Application.Abstractions.Diagnostics;

/// <summary>
/// 慢查询画像读模型。
/// </summary>
public sealed record SlowQueryProfileReadModel(
    string Fingerprint,
    string NormalizedSql,
    string SampleSql,
    int CallCount,
    decimal AverageElapsedMilliseconds,
    decimal P95Milliseconds,
    decimal P99Milliseconds,
    decimal MaxMilliseconds,
    int TimeoutCount,
    int ErrorCount,
    int DeadlockCount,
    int TotalAffectedRows,
    DateTime WindowStartedAtLocal,
    DateTime WindowEndedAtLocal,
    DateTime LastOccurredAtLocal) {
    /// <summary>诊断类别，区分 SQL、连接和慢请求。</summary>
    public string Kind { get; init; } = "query";
    /// <summary>数据库提供器名称。</summary>
    public string Provider { get; init; } = "";
    /// <summary>数据库用途，区分业务库和配置历史库。</summary>
    public string DatabaseRole { get; init; } = "business";
    /// <summary>最近样本的请求追踪标识。</summary>
    public string TraceId { get; init; } = "";
    /// <summary>最近样本的追踪跨度标识。</summary>
    public string SpanId { get; init; } = "";
    /// <summary>最近一次错误的异常类型。</summary>
    public string ExceptionType { get; init; } = "";
    /// <summary>最近慢请求的响应状态码。</summary>
    public int StatusCode { get; init; }
    /// <summary>最近实际执行标识。</summary>
    public string CommandId { get; init; } = "";
    /// <summary>取消次数。</summary>
    public int CanceledCount { get; init; }
    /// <summary>提前结束读取次数。</summary>
    public int PartialReadCount { get; init; }
    /// <summary>平均执行耗时。</summary>
    public decimal AverageExecuteMilliseconds { get; init; }
    /// <summary>平均实际读调用耗时。</summary>
    public decimal AverageReadMilliseconds { get; init; }
    /// <summary>平均物化与消费耗时。</summary>
    public decimal AverageConsumerMilliseconds { get; init; }
    /// <summary>平均连接等待耗时。</summary>
    public decimal AverageConnectionMilliseconds { get; init; }
    /// <summary>累计读行数。</summary>
    public long TotalRowsRead { get; init; }
    /// <summary>最近请求的命令数量，暴露大量短查询累计变慢。</summary>
    public long LatestCommandCount { get; init; }
}
