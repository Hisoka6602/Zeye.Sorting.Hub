namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;

/// <summary>
/// 数据归档 dry-run 配置。
/// </summary>
public sealed class DataArchiveOptions {
    /// <summary>
    /// Worker 轮询最小秒数。
    /// </summary>
    public const int MinWorkerPollIntervalSeconds = 1;

    /// <summary>
    /// Worker 轮询最大秒数。
    /// </summary>
    public const int MaxWorkerPollIntervalSeconds = 3600;

    /// <summary>
    /// 样本条数最小值。
    /// </summary>
    public const int MinSampleItemLimit = 1;

    /// <summary>
    /// 样本条数最大值。
    /// </summary>
    public const int MaxSampleItemLimit = 100;

    /// <summary>
    /// 是否启用归档 Worker。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Worker 轮询间隔秒数。
    /// </summary>
    public int WorkerPollIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// dry-run 摘要中保留的样本条数。
    /// </summary>
    public int SampleItemLimit { get; set; } = 5;
}
