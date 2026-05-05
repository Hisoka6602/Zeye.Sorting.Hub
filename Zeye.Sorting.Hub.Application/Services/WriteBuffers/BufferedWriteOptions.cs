namespace Zeye.Sorting.Hub.Application.Services.WriteBuffers;

/// <summary>
/// Parcel 批量缓冲写入配置。
/// </summary>
public sealed class BufferedWriteOptions {
    /// <summary>
    /// 配置节路径。
    /// </summary>
    public const string SectionPath = "Persistence:WriteBuffering";

    /// <summary>
    /// 通道容量最小值。
    /// </summary>
    public const int MinChannelCapacity = 1;

    /// <summary>
    /// 通道容量最大值。
    /// </summary>
    public const int MaxChannelCapacity = 100000;

    /// <summary>
    /// 批次大小最小值。
    /// </summary>
    public const int MinBatchSize = 1;

    /// <summary>
    /// 批次大小最大值。
    /// </summary>
    public const int MaxBatchSize = 5000;

    /// <summary>
    /// 刷新间隔最小值（毫秒）。
    /// </summary>
    public const int MinFlushIntervalMilliseconds = 10;

    /// <summary>
    /// 刷新间隔最大值（毫秒）。
    /// </summary>
    public const int MaxFlushIntervalMilliseconds = 60000;

    /// <summary>
    /// 最大重试次数最小值。
    /// </summary>
    public const int MinMaxRetryCount = 0;

    /// <summary>
    /// 最大重试次数最大值。
    /// </summary>
    public const int MaxMaxRetryCount = 10;

    /// <summary>
    /// 死信容量最小值。
    /// </summary>
    public const int MinDeadLetterCapacity = 1;

    /// <summary>
    /// 死信容量最大值。
    /// </summary>
    public const int MaxDeadLetterCapacity = 100000;

    /// <summary>
    /// 是否启用批量缓冲写入。
    /// 可填写范围：true / false。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 通道容量上限。
    /// 可填写范围：1~100000。
    /// </summary>
    public int ChannelCapacity { get; set; } = 10000;

    /// <summary>
    /// 单次批量刷新最大条数。
    /// 可填写范围：1~5000。
    /// </summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>
    /// 刷新间隔（毫秒）。
    /// 可填写范围：10~60000。
    /// </summary>
    public int FlushIntervalMilliseconds { get; set; } = 200;

    /// <summary>
    /// 单条记录最大重试次数。
    /// 可填写范围：0~10。
    /// </summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>
    /// 触发背压拒绝的队列深度阈值。
    /// 可填写范围：1~ChannelCapacity。
    /// </summary>
    public int BackpressureRejectThreshold { get; set; } = 9000;

    /// <summary>
    /// 死信容量上限。
    /// 可填写范围：1~100000。
    /// </summary>
    public int DeadLetterCapacity { get; set; } = 10000;
}
