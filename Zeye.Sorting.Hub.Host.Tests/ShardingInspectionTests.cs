using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Domain.Enums.Sharding;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 分表巡检、预建计划与健康检查回归测试。
/// </summary>
public sealed class ShardingInspectionTests {
    /// <summary>
    /// 验证场景：规划构建器按 PerDay 生成 Parcel 与 WebRequestAuditLog 物理表名。
    /// </summary>
    [Fact]
    public void ShardingPhysicalTablePlanBuilder_WithPerDay_ShouldBuildParcelAndAuditTables() {
        using var dbContext = CreateDbContext();
        var decision = CreatePerDayDecision();
        var builder = new ShardingPhysicalTablePlanBuilder(decision);
        var startAtLocal = LocalTimeTestConstraint.CreateLocalTime(2026, 5, 5, 10, 0, 0);

        var tables = builder.BuildExpectedPhysicalTableNames(dbContext, startAtLocal, aheadHours: 24, shouldIncludeNextPeriod: true);

        Assert.Contains("Parcels_20260505", tables);
        Assert.Contains("Parcels_20260506", tables);
        Assert.Contains("WebRequestAuditLogs_20260505", tables);
        Assert.Contains("WebRequestAuditLogDetails_20260505", tables);
    }

    /// <summary>
    /// 验证场景：预建计划服务在 dry-run 模式下只输出计划与缺失项，不执行 DDL。
    /// </summary>
    [Fact]
    public async Task ShardingTablePrebuildService_WhenDryRun_ShouldReturnMissingPlan() {
        var probe = new ConfigurableShardingPhysicalTableProbe(allRequestedTablesMissing: true);
        var service = CreatePrebuildService(probe);

        var plan = await service.BuildPlanAsync(CancellationToken.None);

        Assert.True(plan.IsDryRun);
        Assert.True(plan.PlannedPhysicalTables.Count > 0);
        Assert.Equal(plan.PlannedPhysicalTables.Count, plan.MissingPhysicalTables.Count);
        Assert.Equal(1, probe.FindMissingTablesCallCount);
        LocalTimeTestConstraint.AssertIsLocalTime(plan.GeneratedAtLocal);
    }

    /// <summary>
    /// 验证场景：索引巡检可发现 Parcel 关键索引缺失。
    /// </summary>
    [Fact]
    public async Task ShardingIndexInspectionService_WhenParcelIndexMissing_ShouldReportMissingIndex() {
        using var dbContext = CreateDbContext();
        var probe = new ConfigurableShardingPhysicalTableProbe(
            missingIndexesByTable: new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) {
                ["Parcels_202605"] = [ParcelIndexNames.BagCodeScannedTime]
            });
        var service = new ShardingIndexInspectionService(probe, new MySqlDialect());

        var missingIndexes = await service.FindMissingIndexesAsync(dbContext, null, ["Parcels_202605"], CancellationToken.None);

        var description = Assert.Single(missingIndexes);
        Assert.Contains(ParcelIndexNames.BagCodeScannedTime, description, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：表巡检发现缺表时报告不健康。
    /// </summary>
    [Fact]
    public async Task ShardingTableInspectionService_WhenMissingTables_ShouldReturnUnhealthyReport() {
        var probe = new ConfigurableShardingPhysicalTableProbe(allRequestedTablesMissing: true);
        var service = CreateInspectionService(probe, shouldCheckIndexes: false, shouldCheckCapacity: false);

        var report = await service.InspectAsync(CancellationToken.None);

        Assert.False(report.IsHealthy);
        Assert.NotEmpty(report.MissingPhysicalTables);
        Assert.Equal(report, service.GetLastReport());
    }

    /// <summary>
    /// 验证场景：健康检查在巡检报告不健康时返回 Unhealthy。
    /// </summary>
    [Fact]
    public async Task ShardingGovernanceHealthCheck_WhenInspectionUnhealthy_ShouldReturnUnhealthy() {
        var probe = new ConfigurableShardingPhysicalTableProbe(allRequestedTablesMissing: true);
        var inspectionService = CreateInspectionService(probe, shouldCheckIndexes: false, shouldCheckCapacity: false);
        var prebuildService = CreatePrebuildService(new ConfigurableShardingPhysicalTableProbe());
        await inspectionService.InspectAsync(CancellationToken.None);
        var healthCheck = new Zeye.Sorting.Hub.Host.HealthChecks.ShardingGovernanceHealthCheck(inspectionService, prebuildService);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.True((int)result.Data["missingPhysicalTableCount"] > 0);
    }

    /// <summary>
    /// 创建分表巡检服务。
    /// </summary>
    /// <param name="probe">物理对象探测桩。</param>
    /// <param name="shouldCheckIndexes">是否检查索引。</param>
    /// <param name="shouldCheckCapacity">是否检查容量。</param>
    /// <returns>分表巡检服务。</returns>
    private static ShardingTableInspectionService CreateInspectionService(
        ConfigurableShardingPhysicalTableProbe probe,
        bool shouldCheckIndexes = true,
        bool shouldCheckCapacity = true) {
        var dbContextFactory = CreateDbContextFactory();
        var configuration = CreateConfiguration();
        var decision = CreatePerDayDecision();
        var dialect = new MySqlDialect();
        return new ShardingTableInspectionService(
            dbContextFactory,
            probe,
            dialect,
            new ShardingPhysicalTablePlanBuilder(decision),
            new ShardingIndexInspectionService(probe, dialect),
            new ShardingCapacitySnapshotService(configuration),
            Microsoft.Extensions.Options.Options.Create(new ShardingRuntimeInspectionOptions {
                IsEnabled = true,
                InspectionIntervalMinutes = 30,
                ShouldCheckIndexes = shouldCheckIndexes,
                ShouldCheckNextPeriodTables = true,
                ShouldCheckCapacity = shouldCheckCapacity
            }));
    }

    /// <summary>
    /// 创建分表预建计划服务。
    /// </summary>
    /// <param name="probe">物理对象探测桩。</param>
    /// <returns>分表预建计划服务。</returns>
    private static ShardingTablePrebuildService CreatePrebuildService(ConfigurableShardingPhysicalTableProbe probe) {
        var decision = CreatePerDayDecision();
        return new ShardingTablePrebuildService(
            CreateDbContextFactory(),
            probe,
            new MySqlDialect(),
            new ShardingPhysicalTablePlanBuilder(decision),
            Microsoft.Extensions.Options.Options.Create(new ShardingPrebuildOptions {
                IsEnabled = true,
                DryRun = true,
                PrebuildAheadHours = 24
            }));
    }

    /// <summary>
    /// 创建 PerDay 分表策略决策。
    /// </summary>
    /// <returns>分表策略决策。</returns>
    private static ParcelShardingStrategyDecision CreatePerDayDecision() {
        return ParcelShardingStrategyEvaluator.Evaluate(CreateConfiguration()).Decision;
    }

    /// <summary>
    /// 创建测试配置。
    /// </summary>
    /// <returns>配置根。</returns>
    private static IConfigurationRoot CreateConfiguration() {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Sharding:Strategy:Mode"] = "Time",
                ["Persistence:Sharding:Strategy:Time:Granularity"] = "PerDay"
            })
            .Build();
    }

    /// <summary>
    /// 创建测试数据库上下文。
    /// </summary>
    /// <returns>数据库上下文。</returns>
    private static SortingHubDbContext CreateDbContext() {
        return CreateDbContextFactory().CreateDbContext();
    }

    /// <summary>
    /// 创建测试数据库上下文工厂。
    /// </summary>
    /// <returns>数据库上下文工厂。</returns>
    private static SortingHubTestDbContextFactory CreateDbContextFactory() {
        var options = new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase($"sharding-inspection-{Guid.NewGuid():N}")
            .Options;
        return new SortingHubTestDbContextFactory(options);
    }
}
