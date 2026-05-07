namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

/// <summary>
/// 数据保留治理配置。
/// </summary>
public sealed class DataRetentionOptions {
    /// <summary>
    /// 配置节路径。
    /// </summary>
    public const string SectionPath = "Persistence:Retention";

    /// <summary>
    /// 批次大小最小值。
    /// </summary>
    public const int MinBatchSize = 1;

    /// <summary>
    /// 批次大小最大值。
    /// </summary>
    public const int MaxBatchSize = 5000;

    /// <summary>
    /// 轮询间隔最小分钟数。
    /// </summary>
    public const int MinPollIntervalMinutes = 1;

    /// <summary>
    /// 轮询间隔最大分钟数。
    /// </summary>
    public const int MaxPollIntervalMinutes = 1440;

    /// <summary>
    /// 是否启用数据保留治理。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 是否开启危险动作守卫。
    /// </summary>
    public bool EnableGuard { get; set; } = true;

    /// <summary>
    /// 是否允许执行真实清理。
    /// </summary>
    public bool AllowDangerousActionExecution { get; set; }

    /// <summary>
    /// 是否仅执行 dry-run。
    /// </summary>
    public bool DryRun { get; set; } = true;

    /// <summary>
    /// 单轮每个策略的最大处理批次。
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// 后台治理轮询间隔（分钟）。
    /// </summary>
    public int PollIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// 保留策略清单。
    /// </summary>
    public IReadOnlyList<DataRetentionPolicy> Policies { get; set; } = DataRetentionPolicy.CreateDefaultPolicies();
}
