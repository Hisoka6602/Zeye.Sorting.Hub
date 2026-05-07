namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

/// <summary>
/// 数据保留治理审计记录。
/// </summary>
public sealed record class DataRetentionAuditRecord {
    /// <summary>
    /// Disabled 状态文本。
    /// </summary>
    public const string DisabledStatus = "Disabled";

    /// <summary>
    /// Succeeded 状态文本。
    /// </summary>
    public const string SucceededStatus = "Succeeded";

    /// <summary>
    /// Failed 状态文本。
    /// </summary>
    public const string FailedStatus = "Failed";

    /// <summary>
    /// 记录时间（本地时间）。
    /// </summary>
    public required DateTime RecordedAtLocal { get; init; }

    /// <summary>
    /// 当前状态。
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// 是否启用治理。
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// 是否 dry-run。
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// 单策略每批处理上限。
    /// </summary>
    public required int BatchSize { get; init; }

    /// <summary>
    /// 策略数量。
    /// </summary>
    public required int PolicyCount { get; init; }

    /// <summary>
    /// 计划候选总数。
    /// </summary>
    public required int TotalCandidateCount { get; init; }

    /// <summary>
    /// 各策略候选数量。
    /// </summary>
    public required IReadOnlyDictionary<string, int> CandidateCounts { get; init; }

    /// <summary>
    /// 摘要。
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// 失败消息。
    /// </summary>
    public string? FailureMessage { get; init; }

    /// <summary>
    /// 创建未启用记录。
    /// </summary>
    /// <param name="options">当前配置。</param>
    /// <returns>审计记录。</returns>
    public static DataRetentionAuditRecord CreateDisabled(DataRetentionOptions options) {
        return new DataRetentionAuditRecord {
            RecordedAtLocal = DateTime.Now,
            Status = DisabledStatus,
            IsEnabled = false,
            IsDryRun = options.DryRun,
            BatchSize = options.BatchSize,
            PolicyCount = options.Policies.Count,
            TotalCandidateCount = 0,
            CandidateCounts = CreateEmptyCandidateCounts(options.Policies),
            Summary = "数据保留治理未启用。"
        };
    }

    /// <summary>
    /// 创建成功记录。
    /// </summary>
    /// <param name="options">当前配置。</param>
    /// <param name="candidateCounts">候选数量。</param>
    /// <param name="summary">摘要。</param>
    /// <returns>审计记录。</returns>
    public static DataRetentionAuditRecord CreateSucceeded(
        DataRetentionOptions options,
        IReadOnlyDictionary<string, int> candidateCounts,
        string summary) {
        return new DataRetentionAuditRecord {
            RecordedAtLocal = DateTime.Now,
            Status = SucceededStatus,
            IsEnabled = true,
            IsDryRun = options.DryRun,
            BatchSize = options.BatchSize,
            PolicyCount = options.Policies.Count,
            TotalCandidateCount = candidateCounts.Values.Sum(),
            CandidateCounts = new Dictionary<string, int>(candidateCounts, StringComparer.Ordinal),
            Summary = summary
        };
    }

    /// <summary>
    /// 创建失败记录。
    /// </summary>
    /// <param name="options">当前配置。</param>
    /// <param name="failureMessage">失败消息。</param>
    /// <returns>审计记录。</returns>
    public static DataRetentionAuditRecord CreateFailed(DataRetentionOptions options, string failureMessage) {
        return new DataRetentionAuditRecord {
            RecordedAtLocal = DateTime.Now,
            Status = FailedStatus,
            IsEnabled = options.IsEnabled,
            IsDryRun = options.DryRun,
            BatchSize = options.BatchSize,
            PolicyCount = options.Policies.Count,
            TotalCandidateCount = 0,
            CandidateCounts = CreateEmptyCandidateCounts(options.Policies),
            Summary = "数据保留治理执行失败。",
            FailureMessage = failureMessage
        };
    }

    /// <summary>
    /// 创建空候选字典。
    /// </summary>
    /// <param name="policies">策略集合。</param>
    /// <returns>空候选字典。</returns>
    private static IReadOnlyDictionary<string, int> CreateEmptyCandidateCounts(IEnumerable<DataRetentionPolicy> policies) {
        return policies
            .Where(static policy => !string.IsNullOrWhiteSpace(policy.Name))
            .ToDictionary(static policy => policy.Name, static _ => 0, StringComparer.Ordinal);
    }
}
