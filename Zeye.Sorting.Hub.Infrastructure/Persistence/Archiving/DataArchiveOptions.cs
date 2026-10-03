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

    /// <summary>单次 dry-run 执行的超时秒数。可填写范围：5~3600，且小于遗留任务恢复窗口。</summary>
    public int ExecutionTimeoutSeconds { get; set; } = 120;

    /// <summary>无人值守恢复 Running 遗留任务的分钟数。可填写范围：5~1440。</summary>
    public int AbandonedTaskTimeoutMinutes { get; set; } = 15;

    /// <summary>遗留任务最多自动重新入队次数。可填写范围：0~10，0 表示仅标记失败。</summary>
    public int MaxAutomaticRecoveryAttempts { get; set; } = 3;

    /// <summary>单轮最多恢复遗留任务数。可填写范围：1~100。</summary>
    public int AutomaticRecoveryBatchSize { get; set; } = 20;
}
