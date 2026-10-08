namespace Zeye.Sorting.Hub.Contracts.Models.Diagnostics;

/// <summary>
/// 慢查询画像响应合同。
/// </summary>
public sealed record SlowQueryProfileResponse {
    /// <summary>观测类别：query 为 SQL，connection 为连接等待，request 为请求累计。</summary>
    public string Kind { get; init; } = "query";
    /// <summary>数据库提供器名称。</summary>
    public string Provider { get; init; } = "";
    /// <summary>数据库用途，区分业务和配置历史读写。</summary>
    public string DatabaseRole { get; init; } = "business";
    /// <summary>最近样本的请求追踪标识，关联请求审计。</summary>
    public string TraceId { get; init; } = "";
    /// <summary>最近样本的活动跨度标识。</summary>
    public string SpanId { get; init; } = "";
    /// <summary>最近一次实际数据库操作标识。</summary>
    public string CommandId { get; init; } = "";
    /// <summary>取消的调用次数。</summary>
    public int CanceledCount { get; init; }
    /// <summary>提前结束当前结果读取的调用次数。</summary>
    public int PartialReadCount { get; init; }
    /// <summary>平均数据库执行耗时，请求类别表示全部实际数据库操作累计耗时。</summary>
    public decimal AverageExecuteMilliseconds { get; init; }
    /// <summary>平均提供器读调用耗时。</summary>
    public decimal AverageReadMilliseconds { get; init; }
    /// <summary>平均读取器存续中的消费耗时，包含物化、应用处理和等待。</summary>
    public decimal AverageConsumerMilliseconds { get; init; }
    /// <summary>平均连接打开等待耗时。</summary>
    public decimal AverageConnectionMilliseconds { get; init; }
    /// <summary>实际读取累计行数。</summary>
    public long TotalRowsRead { get; init; }
    /// <summary>最近请求或 SQL 中的实际命令数量。</summary>
    public long LatestCommandCount { get; init; }
    /// <summary>最近一次异常类型，不包含异常正文。</summary>
    public string ExceptionType { get; init; } = "";
    /// <summary>最近慢请求的响应状态码。</summary>
    public int StatusCode { get; init; }
    /// <summary>
    /// SQL 指纹。
    /// </summary>
    public required string Fingerprint { get; init; }

    /// <summary>
    /// 去参数化后的标准 SQL。
    /// </summary>
    public required string NormalizedSql { get; init; }

    /// <summary>
    /// 最近一条样例 SQL。
    /// </summary>
    public required string SampleSql { get; init; }

    /// <summary>
    /// 调用次数。
    /// </summary>
    public int CallCount { get; init; }

    /// <summary>
    /// 平均耗时（毫秒）。
    /// </summary>
    public decimal AverageElapsedMilliseconds { get; init; }

    /// <summary>
    /// P95 耗时（毫秒）。
    /// </summary>
    public decimal P95Milliseconds { get; init; }

    /// <summary>
    /// P99 耗时（毫秒）。
    /// </summary>
    public decimal P99Milliseconds { get; init; }

    /// <summary>
    /// 最大耗时（毫秒）。
    /// </summary>
    public decimal MaxMilliseconds { get; init; }

    /// <summary>
    /// 超时次数。
    /// </summary>
    public int TimeoutCount { get; init; }

    /// <summary>
    /// 异常次数。
    /// </summary>
    public int ErrorCount { get; init; }

    /// <summary>
    /// 死锁次数。
    /// </summary>
    public int DeadlockCount { get; init; }

    /// <summary>
    /// 累计影响行数。
    /// </summary>
    public int TotalAffectedRows { get; init; }

    /// <summary>
    /// 当前窗口开始时间（本地时间语义）。
    /// </summary>
    public DateTime WindowStartedAtLocal { get; init; }

    /// <summary>
    /// 当前窗口结束时间（本地时间语义）。
    /// </summary>
    public DateTime WindowEndedAtLocal { get; init; }

    /// <summary>
    /// 最近一次出现时间（本地时间语义）。
    /// </summary>
    public DateTime LastOccurredAtLocal { get; init; }
}
