using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 迁移治理测试。
/// </summary>
public sealed class MigrationGovernanceTests {
    /// <summary>
    /// 无待执行迁移时健康检查应返回 Healthy。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task MigrationGovernanceHealthCheck_WhenNoPendingMigrations_ShouldReturnHealthy() {
        var store = new MigrationGovernanceStateStore();
        var plan = CreatePlan(
            pendingMigrations: [],
            shouldApplyMigrations: true,
            skipReason: null,
            dangerousOperations: []);
        store.SetLatestPlan(plan);
        store.SetLatestExecutionRecord(MigrationExecutionRecord.CreateSucceeded(plan, "未发现待执行迁移。"));

        var healthCheck = new MigrationGovernanceHealthCheck(store);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    /// <summary>
    /// 有待执行迁移且被跳过时健康检查应返回 Degraded。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task MigrationGovernanceHealthCheck_WhenPendingMigrationsSkipped_ShouldReturnDegraded() {
        var store = new MigrationGovernanceStateStore();
        var plan = CreatePlan(
            pendingMigrations: ["202605050001_AddDangerousMigration"],
            shouldApplyMigrations: false,
            skipReason: "当前处于 dry-run 模式，待执行迁移仅归档不执行。",
            dangerousOperations: ["DROP TABLE: DROP TABLE ArchiveTasks"]);
        store.SetLatestPlan(plan);
        store.SetLatestExecutionRecord(MigrationExecutionRecord.CreateSkipped(plan, plan.SkipReason!));

        var healthCheck = new MigrationGovernanceHealthCheck(store);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    /// <summary>
    /// 危险 SQL 识别器应覆盖路线图要求的关键类型。
    /// </summary>
    [Fact]
    public void MigrationSafetyEvaluator_ShouldDetectDangerousSqlPatterns() {
        var sqlScript = """
            DROP TABLE ArchiveTasks;
            ALTER TABLE ArchiveTasks DROP COLUMN FailureMessage;
            TRUNCATE TABLE ArchiveTasks;
            ALTER TABLE ArchiveTasks ALTER COLUMN TaskType nvarchar(64);
            RENAME TABLE ArchiveTasks TO ArchiveTasksHistory;
            DELETE FROM ArchiveTasks;
            UPDATE ArchiveTasks SET RetryCount = RetryCount + 1;
            """;
        var evaluator = new MigrationSafetyEvaluator();

        var dangerousOperations = evaluator.EvaluateDangerousOperations(sqlScript);

        Assert.Contains(dangerousOperations, static item => item.StartsWith("DROP TABLE", StringComparison.Ordinal));
        Assert.Contains(dangerousOperations, static item => item.StartsWith("DROP COLUMN", StringComparison.Ordinal));
        Assert.Contains(dangerousOperations, static item => item.StartsWith("TRUNCATE", StringComparison.Ordinal));
        Assert.Contains(dangerousOperations, static item => item.StartsWith("ALTER COLUMN", StringComparison.Ordinal));
        Assert.Contains(dangerousOperations, static item => item.StartsWith("RENAME TABLE", StringComparison.Ordinal));
        Assert.Contains(dangerousOperations, static item => item.StartsWith("DELETE FROM", StringComparison.Ordinal));
        Assert.Contains(dangerousOperations, static item => item.StartsWith("UPDATE without WHERE", StringComparison.Ordinal));
    }

    /// <summary>
    /// dry-run 模式下应阻断真实迁移执行。
    /// </summary>
    [Fact]
    public void MigrationGovernanceHostedService_EvaluateShouldApplyMigrations_WhenDryRun_ShouldSkipExecution() {
        var (shouldApplyMigrations, skipReason) = MigrationGovernanceHostedService.EvaluateShouldApplyMigrations(
            hasPendingMigrations: true,
            isDryRun: true,
            isProductionEnvironment: false,
            blockDangerousMigrationInProduction: true,
            dangerousOperations: ["DROP TABLE: DROP TABLE ArchiveTasks"]);

        Assert.False(shouldApplyMigrations);
        Assert.Equal("当前处于 dry-run 模式，待执行迁移仅归档不执行。", skipReason);
    }

    /// <summary>
    /// 脚本归档路径应落在配置目录下。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task MigrationScriptArchiveService_ShouldWriteArtifactUnderConfiguredDirectory() {
        var tempRootPath = Path.Combine(Path.GetTempPath(), $"migration-governance-archive-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRootPath);
        try {
            var hostEnvironment = new TestHostEnvironment("Development") {
                ContentRootPath = tempRootPath
            };
            var archiveService = new MigrationScriptArchiveService(hostEnvironment);

            var archivedPath = await archiveService.ArchiveForwardScriptAsync(
                "migration-scripts",
                "MySql",
                "202605050001_AddArchiveTasks",
                "-- forward script",
                CancellationToken.None);

            Assert.StartsWith(Path.Combine(tempRootPath, "migration-scripts"), archivedPath, StringComparison.Ordinal);
            Assert.True(File.Exists(archivedPath));
        }
        finally {
            if (Directory.Exists(tempRootPath)) {
                Directory.Delete(tempRootPath, recursive: true);
            }
        }
    }

    /// <summary>
    /// 迁移治理预演异常时不应向外抛出，并应写入失败记录。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task MigrationGovernanceHostedService_StartAsync_WhenInspectionThrows_ShouldRecordFailure() {
        var tempRootPath = Path.Combine(Path.GetTempPath(), $"migration-governance-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRootPath);
        try {
            var services = new ServiceCollection();
            services.AddDbContextFactory<SortingHubDbContext>(options =>
                options.UseSqlServer("Server=127.0.0.1,1;Database=MigrationGovernanceFailure;User Id=sa;******;TrustServerCertificate=True;Connect Timeout=1"));
            var serviceProvider = services.BuildServiceProvider();
            var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<SortingHubDbContext>>();
            var hostEnvironment = new TestHostEnvironment("Development") {
                ContentRootPath = tempRootPath
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> {
                    ["Persistence:MigrationGovernance:IsEnabled"] = "true",
                    ["Persistence:MigrationGovernance:DryRun"] = "false",
                    ["Persistence:MigrationGovernance:ArchiveDirectory"] = "migration-scripts",
                    ["Persistence:MigrationGovernance:BlockDangerousMigrationInProduction"] = "true"
                })
                .Build();
            var store = new MigrationGovernanceStateStore();
            var service = new MigrationGovernanceHostedService(
                dbContextFactory,
                new TestSqlServerDialect(),
                hostEnvironment,
                configuration,
                new MigrationSafetyEvaluator(),
                new MigrationScriptArchiveService(hostEnvironment),
                new MigrationRollbackScriptProvider(),
                store);

            await service.StartAsync(CancellationToken.None);

            var executionRecord = store.GetLatestExecutionRecord();
            Assert.NotNull(executionRecord);
            Assert.Equal(MigrationExecutionRecord.FailedStatus, executionRecord!.Status);
            Assert.False(executionRecord.ShouldApplyMigrations);
        }
        finally {
            if (Directory.Exists(tempRootPath)) {
                Directory.Delete(tempRootPath, recursive: true);
            }
        }
    }

    /// <summary>
    /// 创建迁移计划测试桩。
    /// </summary>
    /// <param name="pendingMigrations">待执行迁移。</param>
    /// <param name="shouldApplyMigrations">是否允许真实执行。</param>
    /// <param name="skipReason">阻断原因。</param>
    /// <param name="dangerousOperations">危险操作。</param>
    /// <returns>迁移计划。</returns>
    private static MigrationPlan CreatePlan(
        IReadOnlyList<string> pendingMigrations,
        bool shouldApplyMigrations,
        string? skipReason,
        IReadOnlyList<string> dangerousOperations) {
        return new MigrationPlan {
            GeneratedAtLocal = DateTime.Now,
            ProviderName = "MySql",
            EnvironmentName = "Development",
            IsEnabled = true,
            IsDryRun = !shouldApplyMigrations,
            BlockDangerousMigrationInProduction = true,
            IsProductionEnvironment = false,
            AllMigrations = ["202605010001_InitialCreate"],
            AppliedMigrations = ["202605010001_InitialCreate"],
            PendingMigrations = pendingMigrations,
            DangerousOperations = dangerousOperations,
            ShouldApplyMigrations = shouldApplyMigrations,
            SkipReason = skipReason,
            ArchivedForwardScriptPath = "/tmp/forward.sql",
            ArchivedRollbackScriptPath = "/tmp/rollback.sql"
        };
    }
}
