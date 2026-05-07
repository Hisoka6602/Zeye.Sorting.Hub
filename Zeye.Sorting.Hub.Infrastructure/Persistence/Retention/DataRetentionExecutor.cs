using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;

/// <summary>
/// 数据保留执行器。
/// </summary>
public sealed class DataRetentionExecutor {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 数据保留计划器。
    /// </summary>
    private readonly DataRetentionPlanner _planner;

    /// <summary>
    /// 数据保留配置。
    /// </summary>
    private readonly DataRetentionOptions _options;

    /// <summary>
    /// 最近一次审计记录。
    /// </summary>
    private DataRetentionAuditRecord? _lastAuditRecord;

    /// <summary>
    /// 初始化数据保留执行器。
    /// </summary>
    /// <param name="planner">数据保留计划器。</param>
    /// <param name="options">数据保留配置。</param>
    public DataRetentionExecutor(DataRetentionPlanner planner, IOptions<DataRetentionOptions> options) {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 获取最近一次审计记录。
    /// </summary>
    /// <returns>审计记录。</returns>
    public DataRetentionAuditRecord? GetLastAuditRecord() {
        return Volatile.Read(ref _lastAuditRecord);
    }

    /// <summary>
    /// 执行一轮数据保留治理。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>审计记录。</returns>
    public async Task<DataRetentionAuditRecord> ExecuteAsync(CancellationToken cancellationToken) {
        if (!_options.IsEnabled) {
            return PublishRecord(DataRetentionAuditRecord.CreateDisabled());
        }

        var policies = _planner.GetEffectivePolicies();
        if (policies.Count == 0) {
            return PublishRecord(DataRetentionAuditRecord.CreateNoPolicies(_options.DryRun));
        }

        try {
            var now = DateTime.Now;
            var planItems = new List<(DataRetentionPolicy Policy, DateTime ExpireBefore, int PlannedCount)>(policies.Count);
            foreach (var policy in policies) {
                var expireBefore = DataRetentionPlanner.BuildExpireBefore(policy, now);
                var plannedCount = await _planner.CountPlannedAsync(policy, expireBefore, _options.BatchSize, cancellationToken);
                planItems.Add((policy, expireBefore, plannedCount));
            }

            var totalPlannedCount = planItems.Sum(static item => item.PlannedCount);
            var decision = ActionIsolationPolicy.Evaluate(
                _options.EnableGuard,
                _options.AllowDangerousActionExecution,
                _options.DryRun,
                dangerousAction: totalPlannedCount > 0,
                isRollback: false);

            if (decision != ActionIsolationDecision.Execute) {
                var record = BuildSkippedRecord(planItems, totalPlannedCount, decision);
                return PublishRecord(record);
            }

            var policySummaries = new List<string>(planItems.Count);
            var executedCount = 0;
            var failedPolicyCount = 0;
            foreach (var planItem in planItems) {
                try {
                    var currentExecutedCount = await _planner.ExecuteAsync(planItem.Policy, planItem.ExpireBefore, _options.BatchSize, cancellationToken);
                    executedCount += currentExecutedCount;
                    policySummaries.Add($"{planItem.Policy.Name}: planned={planItem.PlannedCount}, executed={currentExecutedCount}, retentionDays={planItem.Policy.RetentionDays}");
                }
                catch (Exception exception) {
                    failedPolicyCount++;
                    Logger.Error(exception, "数据保留策略执行失败，PolicyName={PolicyName}", planItem.Policy.Name);
                    policySummaries.Add($"{planItem.Policy.Name}: planned={planItem.PlannedCount}, executed=0, retentionDays={planItem.Policy.RetentionDays}, failed=true");
                }
            }

            var summary = failedPolicyCount == 0
                ? $"数据保留治理执行完成，PlannedCount={totalPlannedCount}, ExecutedCount={executedCount}。"
                : $"数据保留治理执行完成，但存在 {failedPolicyCount} 个策略失败，PlannedCount={totalPlannedCount}, ExecutedCount={executedCount}。";
            var completedRecord = new DataRetentionAuditRecord {
                RecordedAtLocal = DateTime.Now,
                Status = failedPolicyCount == 0 ? DataRetentionAuditRecord.CompletedStatus : DataRetentionAuditRecord.FailedStatus,
                IsEnabled = true,
                Decision = ActionIsolationDecision.Execute,
                IsDryRun = false,
                PolicyCount = planItems.Count,
                PlannedCount = totalPlannedCount,
                ExecutedCount = executedCount,
                FailedPolicyCount = failedPolicyCount,
                Summary = summary,
                PolicySummaries = policySummaries
            };
            return PublishRecord(completedRecord);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            Logger.Warn("数据保留治理执行收到取消信号。");
            throw;
        }
        catch (Exception exception) {
            Logger.Error(exception, "数据保留治理执行失败。");
            var failedRecord = new DataRetentionAuditRecord {
                RecordedAtLocal = DateTime.Now,
                Status = DataRetentionAuditRecord.FailedStatus,
                IsEnabled = true,
                Decision = ActionIsolationDecision.Execute,
                IsDryRun = false,
                PolicyCount = policies.Count,
                PlannedCount = 0,
                ExecutedCount = 0,
                FailedPolicyCount = policies.Count,
                Summary = $"数据保留治理执行失败：{exception.Message}",
                PolicySummaries = []
            };
            return PublishRecord(failedRecord);
        }
    }

    /// <summary>
    /// 发布并记录审计结果。
    /// </summary>
    /// <param name="record">审计记录。</param>
    /// <returns>审计记录。</returns>
    private DataRetentionAuditRecord PublishRecord(DataRetentionAuditRecord record) {
        Volatile.Write(ref _lastAuditRecord, record);
        Logger.Info(
            "数据保留治理审计：Status={Status}, Decision={Decision}, PlannedCount={PlannedCount}, ExecutedCount={ExecutedCount}, FailedPolicyCount={FailedPolicyCount}, Summary={Summary}",
            record.Status,
            record.Decision,
            record.PlannedCount,
            record.ExecutedCount,
            record.FailedPolicyCount,
            record.Summary);
        return record;
    }

    /// <summary>
    /// 构建阻断或 dry-run 审计记录。
    /// </summary>
    /// <param name="planItems">计划项。</param>
    /// <param name="totalPlannedCount">计划总量。</param>
    /// <param name="decision">执行决策。</param>
    /// <returns>审计记录。</returns>
    private static DataRetentionAuditRecord BuildSkippedRecord(
        IReadOnlyList<(DataRetentionPolicy Policy, DateTime ExpireBefore, int PlannedCount)> planItems,
        int totalPlannedCount,
        ActionIsolationDecision decision) {
        var policySummaries = planItems
            .Select(static item => $"{item.Policy.Name}: planned={item.PlannedCount}, executed=0, retentionDays={item.Policy.RetentionDays}")
            .ToArray();
        var summary = decision == ActionIsolationDecision.BlockedByGuard
            ? $"数据保留治理被危险动作守卫阻断，PlannedCount={totalPlannedCount}。"
            : $"数据保留治理当前处于 dry-run，仅输出计划，PlannedCount={totalPlannedCount}。";
        return new DataRetentionAuditRecord {
            RecordedAtLocal = DateTime.Now,
            Status = DataRetentionAuditRecord.CompletedStatus,
            IsEnabled = true,
            Decision = decision,
            IsDryRun = decision == ActionIsolationDecision.DryRunOnly,
            PolicyCount = planItems.Count,
            PlannedCount = totalPlannedCount,
            ExecutedCount = 0,
            FailedPolicyCount = 0,
            Summary = summary,
            PolicySummaries = policySummaries
        };
    }
}
