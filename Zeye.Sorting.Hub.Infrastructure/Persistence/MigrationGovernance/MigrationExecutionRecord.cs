using System;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

/// <summary>
/// 迁移治理执行记录。
/// </summary>
public sealed record class MigrationExecutionRecord {
    /// <summary>
    /// 预演完成、执行完成或失败记录时间（本地时间）。
    /// </summary>
    public required DateTime RecordedAtLocal { get; init; }

    /// <summary>
    /// 数据库提供器名称。
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// 宿主环境名称。
    /// </summary>
    public required string EnvironmentName { get; init; }

    /// <summary>
    /// 是否启用迁移治理。
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// 是否启用 dry-run。
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// 当前状态：Disabled / Prepared / Skipped / Succeeded / Failed。
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// 是否允许执行真实迁移。
    /// </summary>
    public required bool ShouldApplyMigrations { get; init; }

    /// <summary>
    /// 当前记录摘要。
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// 失败原因。
    /// </summary>
    public string? FailureMessage { get; init; }

    /// <summary>
    /// 阻断原因。
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// 当前记录对应的待执行迁移数。
    /// </summary>
    public required int PendingMigrationCount { get; init; }

    /// <summary>
    /// 危险 SQL 命中数。
    /// </summary>
    public required int DangerousOperationCount { get; init; }

    /// <summary>
    /// 正向迁移脚本归档路径。
    /// </summary>
    public string? ArchivedForwardScriptPath { get; init; }

    /// <summary>
    /// 回滚参考脚本归档路径。
    /// </summary>
    public string? ArchivedRollbackScriptPath { get; init; }

    /// <summary>
    /// Disabled 状态文本。
    /// </summary>
    public const string DisabledStatus = "Disabled";

    /// <summary>
    /// Prepared 状态文本。
    /// </summary>
    public const string PreparedStatus = "Prepared";

    /// <summary>
    /// Skipped 状态文本。
    /// </summary>
    public const string SkippedStatus = "Skipped";

    /// <summary>
    /// Succeeded 状态文本。
    /// </summary>
    public const string SucceededStatus = "Succeeded";

    /// <summary>
    /// Failed 状态文本。
    /// </summary>
    public const string FailedStatus = "Failed";

    /// <summary>
    /// 创建“未启用”记录。
    /// </summary>
    /// <param name="providerName">数据库提供器名称。</param>
    /// <param name="environmentName">宿主环境名称。</param>
    /// <returns>执行记录。</returns>
    public static MigrationExecutionRecord CreateDisabled(string providerName, string environmentName) {
        return new MigrationExecutionRecord {
            RecordedAtLocal = DateTime.Now,
            ProviderName = providerName,
            EnvironmentName = environmentName,
            IsEnabled = false,
            IsDryRun = false,
            Status = DisabledStatus,
            ShouldApplyMigrations = true,
            Summary = "迁移治理未启用。",
            PendingMigrationCount = 0,
            DangerousOperationCount = 0
        };
    }

    /// <summary>
    /// 创建“已预演”记录。
    /// </summary>
    /// <param name="plan">迁移计划。</param>
    /// <returns>执行记录。</returns>
    public static MigrationExecutionRecord CreatePrepared(MigrationPlan plan) {
        return CreateFromPlan(plan, PreparedStatus, "迁移治理预演完成，等待数据库初始化阶段决定是否执行真实迁移。");
    }

    /// <summary>
    /// 创建“已跳过”记录。
    /// </summary>
    /// <param name="plan">迁移计划。</param>
    /// <param name="summary">摘要。</param>
    /// <returns>执行记录。</returns>
    public static MigrationExecutionRecord CreateSkipped(MigrationPlan plan, string summary) {
        return CreateFromPlan(plan, SkippedStatus, summary, skipReason: plan.SkipReason);
    }

    /// <summary>
    /// 创建“已跳过”记录（无迁移计划版本）。
    /// </summary>
    /// <param name="providerName">数据库提供器名称。</param>
    /// <param name="environmentName">宿主环境名称。</param>
    /// <param name="isDryRun">是否 dry-run。</param>
    /// <param name="summary">摘要。</param>
    /// <returns>执行记录。</returns>
    public static MigrationExecutionRecord CreateSkipped(string providerName, string environmentName, bool isDryRun, string summary) {
        return new MigrationExecutionRecord {
            RecordedAtLocal = DateTime.Now,
            ProviderName = providerName,
            EnvironmentName = environmentName,
            IsEnabled = true,
            IsDryRun = isDryRun,
            Status = SkippedStatus,
            ShouldApplyMigrations = false,
            Summary = summary,
            PendingMigrationCount = 0,
            DangerousOperationCount = 0
        };
    }

    /// <summary>
    /// 创建“执行成功”记录。
    /// </summary>
    /// <param name="plan">迁移计划。</param>
    /// <param name="summary">摘要。</param>
    /// <returns>执行记录。</returns>
    public static MigrationExecutionRecord CreateSucceeded(MigrationPlan plan, string summary) {
        return CreateFromPlan(
            plan,
            SucceededStatus,
            summary,
            pendingMigrationCount: 0,
            shouldApplyMigrations: true,
            skipReason: null);
    }

    /// <summary>
    /// 创建“执行失败”记录。
    /// </summary>
    /// <param name="plan">迁移计划。</param>
    /// <param name="failureMessage">失败消息。</param>
    /// <param name="summary">摘要。</param>
    /// <returns>执行记录。</returns>
    public static MigrationExecutionRecord CreateFailed(MigrationPlan? plan, string failureMessage, string summary) {
        return new MigrationExecutionRecord {
            RecordedAtLocal = DateTime.Now,
            ProviderName = plan?.ProviderName ?? "Unknown",
            EnvironmentName = plan?.EnvironmentName ?? "Unknown",
            IsEnabled = plan?.IsEnabled ?? true,
            IsDryRun = plan?.IsDryRun ?? false,
            Status = FailedStatus,
            ShouldApplyMigrations = false,
            Summary = summary,
            FailureMessage = failureMessage,
            SkipReason = plan?.SkipReason,
            PendingMigrationCount = plan?.PendingMigrations.Count ?? 0,
            DangerousOperationCount = plan?.DangerousOperations.Count ?? 0,
            ArchivedForwardScriptPath = plan?.ArchivedForwardScriptPath,
            ArchivedRollbackScriptPath = plan?.ArchivedRollbackScriptPath
        };
    }

    /// <summary>
    /// 基于迁移计划创建记录。
    /// </summary>
    /// <param name="plan">迁移计划。</param>
    /// <param name="status">状态文本。</param>
    /// <param name="summary">摘要。</param>
    /// <param name="pendingMigrationCount">待执行迁移数。</param>
    /// <param name="shouldApplyMigrations">是否允许执行真实迁移。</param>
    /// <param name="skipReason">阻断原因。</param>
    /// <returns>执行记录。</returns>
    private static MigrationExecutionRecord CreateFromPlan(
        MigrationPlan plan,
        string status,
        string summary,
        int? pendingMigrationCount = null,
        bool? shouldApplyMigrations = null,
        string? skipReason = null) {
        return new MigrationExecutionRecord {
            RecordedAtLocal = DateTime.Now,
            ProviderName = plan.ProviderName,
            EnvironmentName = plan.EnvironmentName,
            IsEnabled = plan.IsEnabled,
            IsDryRun = plan.IsDryRun,
            Status = status,
            ShouldApplyMigrations = shouldApplyMigrations ?? plan.ShouldApplyMigrations,
            Summary = summary,
            SkipReason = skipReason,
            PendingMigrationCount = pendingMigrationCount ?? plan.PendingMigrations.Count,
            DangerousOperationCount = plan.DangerousOperations.Count,
            ArchivedForwardScriptPath = plan.ArchivedForwardScriptPath,
            ArchivedRollbackScriptPath = plan.ArchivedRollbackScriptPath
        };
    }
}
