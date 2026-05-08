namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 生产运行资料规则测试。
/// </summary>
public sealed class ProductionReadinessRulesTests {
    /// <summary>
    /// 生产运行 Runbook 应覆盖 PR-T 要求的 20 个故障场景。
    /// </summary>
    [Fact]
    public void ProductionRunbook_ShouldCoverAllRequiredFailureScenarios() {
        var runbook = RepositoryFileReader.ReadAllText("生产运行Runbook.md");

        Assert.Contains("服务启动失败", runbook, StringComparison.Ordinal);
        Assert.Contains("数据库连接失败", runbook, StringComparison.Ordinal);
        Assert.Contains("数据库连接池耗尽", runbook, StringComparison.Ordinal);
        Assert.Contains("慢查询暴增", runbook, StringComparison.Ordinal);
        Assert.Contains("写入队列积压", runbook, StringComparison.Ordinal);
        Assert.Contains("死信堆积", runbook, StringComparison.Ordinal);
        Assert.Contains("分表缺失", runbook, StringComparison.Ordinal);
        Assert.Contains("索引缺失", runbook, StringComparison.Ordinal);
        Assert.Contains("磁盘空间不足", runbook, StringComparison.Ordinal);
        Assert.Contains("备份失败", runbook, StringComparison.Ordinal);
        Assert.Contains("迁移失败", runbook, StringComparison.Ordinal);
        Assert.Contains("归档任务失败", runbook, StringComparison.Ordinal);
        Assert.Contains("审计日志过大", runbook, StringComparison.Ordinal);
        Assert.Contains("查询 P99 升高", runbook, StringComparison.Ordinal);
        Assert.Contains("内存持续增长", runbook, StringComparison.Ordinal);
        Assert.Contains("CPU 持续过高", runbook, StringComparison.Ordinal);
        Assert.Contains("数据重复写入", runbook, StringComparison.Ordinal);
        Assert.Contains("幂等冲突", runbook, StringComparison.Ordinal);
        Assert.Contains("Outbox 堆积", runbook, StringComparison.Ordinal);
        Assert.Contains("Inbox 重复消费", runbook, StringComparison.Ordinal);
        Assert.Contains("/health/live", runbook, StringComparison.Ordinal);
        Assert.Contains("/health/ready", runbook, StringComparison.Ordinal);
    }

    /// <summary>
    /// 应急、分表、备份文档应引用现有底座实现与演练资产。
    /// </summary>
    [Fact]
    public void OperationalDocuments_ShouldReferenceExistingGovernanceAssets() {
        var emergencyPlan = RepositoryFileReader.ReadAllText("数据库故障应急预案.md");
        var shardingRunbook = RepositoryFileReader.ReadAllText("分表治理Runbook.md");
        var backupRunbook = RepositoryFileReader.ReadAllText("备份恢复演练Runbook.md");

        Assert.Contains("DatabaseConnectionDetailedHealthCheck", emergencyPlan, StringComparison.Ordinal);
        Assert.Contains("MigrationGovernanceHostedService", emergencyPlan, StringComparison.Ordinal);
        Assert.Contains("DataArchiveHostedService", emergencyPlan, StringComparison.Ordinal);
        Assert.Contains("BackupHostedService", emergencyPlan, StringComparison.Ordinal);
        Assert.Contains("ShardingGovernanceHealthCheck", shardingRunbook, StringComparison.Ordinal);
        Assert.Contains("ShardingInspectionHostedService", shardingRunbook, StringComparison.Ordinal);
        Assert.Contains("ShardingPrebuildHostedService", shardingRunbook, StringComparison.Ordinal);
        Assert.Contains("BackupHealthCheck", backupRunbook, StringComparison.Ordinal);
        Assert.Contains("RestoreDrillPlanner", backupRunbook, StringComparison.Ordinal);
        Assert.Contains("drill-records/2026-Q1-稳定性演练记录.md", backupRunbook, StringComparison.Ordinal);
    }

    /// <summary>
    /// 最终验收清单应反映从 PR-A 到 PR-T 的完成状态与最终放行条件。
    /// </summary>
    [Fact]
    public void AcceptanceChecklist_ShouldReflectPrAToPrTCompletion() {
        var checklist = RepositoryFileReader.ReadAllText("业务接入前底座验收清单.md");

        Assert.Contains("PR-A 数据库连接诊断与就绪状态增强", checklist, StringComparison.Ordinal);
        Assert.Contains("PR-S 压测工程与性能基线报告", checklist, StringComparison.Ordinal);
        Assert.Contains("PR-T 生产运行 Runbook、应急预案与最终底座验收", checklist, StringComparison.Ordinal);
        Assert.Contains("[x] 生产 Runbook", checklist, StringComparison.Ordinal);
        Assert.Contains("[x] 应急预案", checklist, StringComparison.Ordinal);
        Assert.Contains("[x] 性能基线报告", checklist, StringComparison.Ordinal);
        Assert.Contains("[x] 可开始进入业务模块开发", checklist, StringComparison.Ordinal);
    }

    /// <summary>
    /// 稳定性门禁与无人值守检查清单应纳入 PR-T 新增运行资料。
    /// </summary>
    [Fact]
    public void StabilityWorkflowAndChecklist_ShouldIncludeProductionReadinessArtifacts() {
        var workflowContent = RepositoryFileReader.ReadAllText(".github", "workflows", "stability-gates.yml");
        var unattendedChecklist = RepositoryFileReader.ReadAllText("无人值守运行检查清单.md");

        Assert.Contains("生产运行Runbook.md", workflowContent, StringComparison.Ordinal);
        Assert.Contains("数据库故障应急预案.md", workflowContent, StringComparison.Ordinal);
        Assert.Contains("分表治理Runbook.md", workflowContent, StringComparison.Ordinal);
        Assert.Contains("备份恢复演练Runbook.md", workflowContent, StringComparison.Ordinal);
        Assert.Contains("业务接入前底座验收清单.md", workflowContent, StringComparison.Ordinal);
        Assert.Contains("无人值守运行检查清单.md", workflowContent, StringComparison.Ordinal);
        Assert.Contains("每日检查", unattendedChecklist, StringComparison.Ordinal);
        Assert.Contains("每周检查", unattendedChecklist, StringComparison.Ordinal);
        Assert.Contains("每月检查", unattendedChecklist, StringComparison.Ordinal);
        Assert.Contains("每季度检查", unattendedChecklist, StringComparison.Ordinal);
        Assert.Contains("drill-records/", unattendedChecklist, StringComparison.Ordinal);
    }
}
