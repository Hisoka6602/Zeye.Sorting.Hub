using System;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

/// <summary>
/// 迁移治理预演计划。
/// </summary>
public sealed record class MigrationPlan {
    /// <summary>
    /// 计划生成时间（本地时间）。
    /// </summary>
    public required DateTime GeneratedAtLocal { get; init; }

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
    /// 生产环境是否阻断危险迁移。
    /// </summary>
    public required bool BlockDangerousMigrationInProduction { get; init; }

    /// <summary>
    /// 当前是否生产环境。
    /// </summary>
    public required bool IsProductionEnvironment { get; init; }

    /// <summary>
    /// 代码中定义的全部迁移列表。
    /// </summary>
    public required IReadOnlyList<string> AllMigrations { get; init; }

    /// <summary>
    /// 已应用迁移列表。
    /// </summary>
    public required IReadOnlyList<string> AppliedMigrations { get; init; }

    /// <summary>
    /// 待执行迁移列表。
    /// </summary>
    public required IReadOnlyList<string> PendingMigrations { get; init; }

    /// <summary>
    /// 危险 SQL 命中列表。
    /// </summary>
    public required IReadOnlyList<string> DangerousOperations { get; init; }

    /// <summary>
    /// 是否允许后续初始化流程执行真实迁移。
    /// </summary>
    public required bool ShouldApplyMigrations { get; init; }

    /// <summary>
    /// 不允许执行真实迁移时的阻断原因。
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// 正向迁移脚本归档路径。
    /// </summary>
    public string? ArchivedForwardScriptPath { get; init; }

    /// <summary>
    /// 回滚参考脚本归档路径。
    /// </summary>
    public string? ArchivedRollbackScriptPath { get; init; }

    /// <summary>
    /// 是否存在待执行迁移。
    /// </summary>
    public bool HasPendingMigrations => PendingMigrations.Count > 0;

    /// <summary>
    /// 是否检测到危险迁移。
    /// </summary>
    public bool IsDangerous => DangerousOperations.Count > 0;
}
