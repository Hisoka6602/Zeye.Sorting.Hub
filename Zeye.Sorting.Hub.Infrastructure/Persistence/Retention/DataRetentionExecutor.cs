using Microsoft.Extensions.Options;
using NLog;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

/// <summary>
/// 数据保留治理执行器。
/// </summary>
public sealed class DataRetentionExecutor {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 数据保留治理计划器。
    /// </summary>
    private readonly DataRetentionPlanner _planner;

    /// <summary>
    /// 数据保留治理配置监视器。
    /// </summary>
    private readonly IOptionsMonitor<DataRetentionOptions> _optionsMonitor;

    /// <summary>
    /// 并发访问锁。
    /// </summary>
    private readonly object _syncRoot = new();

    /// <summary>
    /// 最近一次执行记录。
    /// </summary>
    private DataRetentionAuditRecord? _latestRecord;

    /// <summary>
    /// 初始化数据保留治理执行器。
    /// </summary>
    /// <param name="planner">计划器。</param>
    /// <param name="optionsMonitor">配置监视器。</param>
    public DataRetentionExecutor(
        DataRetentionPlanner planner,
        IOptionsMonitor<DataRetentionOptions> optionsMonitor) {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
    }

    /// <summary>
    /// 执行一次数据保留治理。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>执行记录。</returns>
    public async Task<DataRetentionAuditRecord> ExecuteAsync(CancellationToken cancellationToken) {
        var options = _optionsMonitor.CurrentValue;
        if (!options.IsEnabled) {
            var disabledRecord = DataRetentionAuditRecord.CreateDisabled(options);
            SetLatestRecord(disabledRecord);
            Logger.Info("数据保留治理未启用，BatchSize={BatchSize}, PolicyCount={PolicyCount}", options.BatchSize, options.Policies.Count);
            return disabledRecord;
        }

        if (!options.DryRun) {
            var failureRecord = DataRetentionAuditRecord.CreateFailed(options, "当前版本仅支持 dry-run 数据保留治理。真实清理需后续接入危险动作隔离器。");
            SetLatestRecord(failureRecord);
            Logger.Error("数据保留治理配置非法：当前版本仅支持 dry-run，BatchSize={BatchSize}", options.BatchSize);
            return failureRecord;
        }

        try {
            var candidateCounts = await _planner.BuildCandidateCountsAsync(options, cancellationToken);
            var totalCandidateCount = candidateCounts.Values.Sum();
            var summary = totalCandidateCount > 0
                ? $"数据保留 dry-run 完成，发现 {totalCandidateCount} 条候选记录。"
                : "数据保留 dry-run 完成，未发现候选记录。";
            var auditRecord = DataRetentionAuditRecord.CreateSucceeded(options, candidateCounts, summary);
            SetLatestRecord(auditRecord);
            Logger.Info(
                "数据保留治理执行完成，DryRun={DryRun}, BatchSize={BatchSize}, TotalCandidateCount={TotalCandidateCount}, PolicySummary={PolicySummary}",
                auditRecord.IsDryRun,
                auditRecord.BatchSize,
                auditRecord.TotalCandidateCount,
                BuildPolicySummary(auditRecord.CandidateCounts));
            return auditRecord;
        }
        catch (OperationCanceledException) {
            Logger.Info("数据保留治理执行已取消。");
            throw;
        }
        catch (Exception exception) {
            var failureRecord = DataRetentionAuditRecord.CreateFailed(options, exception.Message);
            SetLatestRecord(failureRecord);
            Logger.Error(exception, "数据保留治理执行失败，PolicyCount={PolicyCount}, BatchSize={BatchSize}", options.Policies.Count, options.BatchSize);
            return failureRecord;
        }
    }

    /// <summary>
    /// 读取最近一次执行记录。
    /// </summary>
    /// <returns>执行记录。</returns>
    public DataRetentionAuditRecord? GetLatestRecord() {
        lock (_syncRoot) {
            return _latestRecord;
        }
    }

    /// <summary>
    /// 写入最近一次执行记录。
    /// </summary>
    /// <param name="record">执行记录。</param>
    private void SetLatestRecord(DataRetentionAuditRecord record) {
        lock (_syncRoot) {
            _latestRecord = record;
        }
    }

    /// <summary>
    /// 构建策略候选摘要。
    /// </summary>
    /// <param name="candidateCounts">候选数量字典。</param>
    /// <returns>摘要文本。</returns>
    private static string BuildPolicySummary(IReadOnlyDictionary<string, int> candidateCounts) {
        if (candidateCounts.Count == 0) {
            return "none";
        }

        return string.Join(", ",
            candidateCounts.Select(static pair => $"{pair.Key}={pair.Value}"));
    }
}
