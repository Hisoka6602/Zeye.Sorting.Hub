using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 备份治理测试。
/// </summary>
public sealed class BackupGovernanceTests {
    /// <summary>
    /// 验证场景：MySQL Provider 应生成 mysqldump 命令。
    /// </summary>
    [Fact]
    public void MySqlBackupProvider_ShouldBuildBackupCommand() {
        var provider = new MySqlBackupProvider();

        var command = provider.BuildBackupCommand(
            "Server=127.0.0.1;Port=3306;Database=sorting_hub;User Id=root;Password=secret;",
            "/tmp/sorting-hub.sql");

        Assert.Contains("mysqldump", command, StringComparison.Ordinal);
        Assert.Contains("sorting_hub", command, StringComparison.Ordinal);
        Assert.Contains("<PASSWORD>", command, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：SQL Server Provider 应生成 BACKUP DATABASE 命令。
    /// </summary>
    [Fact]
    public void SqlServerBackupProvider_ShouldBuildBackupCommand() {
        var provider = new SqlServerBackupProvider();

        var command = provider.BuildBackupCommand(
            "Server=localhost;Initial Catalog=SortingHub;User Id=sa;Password=secret;",
            "/tmp/sorting-hub.bak");

        Assert.Contains("BACKUP DATABASE [SortingHub]", command, StringComparison.Ordinal);
        Assert.Contains("<PASSWORD>", command, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：dry-run 且存在演练记录时应返回成功记录。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BackupVerificationService_WhenDryRunAndDrillRecordExists_ShouldSucceed() {
        var tempRootPath = Path.Combine(Path.GetTempPath(), $"backup-governance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRootPath);
        try {
            Directory.CreateDirectory(Path.Combine(tempRootPath, "drill-records"));
            var drillRecordPath = Path.Combine(tempRootPath, "drill-records", "2026-Q2-恢复演练记录.md");
            await File.WriteAllTextAsync(drillRecordPath, "restore drill record");
            var service = CreateBackupVerificationService(
                tempRootPath,
                providerName: "MySql",
                provider: new MySqlBackupProvider(),
                options: new BackupOptions {
                    IsEnabled = true,
                    DryRun = true,
                    BackupDirectory = "backup-artifacts",
                    BackupFileNamePrefix = "sorting-hub",
                    VerificationIntervalMinutes = 60,
                    ExpectedBackupWithinHours = 24,
                    RestoreDrillDirectory = "drill-records"
                });

            var record = await service.ExecuteAsync(CancellationToken.None);

            Assert.Equal(BackupExecutionRecord.SucceededStatus, record.Status);
            Assert.True(record.HasRestoreDrillRecord);
            Assert.False(record.HasRecentBackupArtifact);
            Assert.Contains("dry-run", record.Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    /// <summary>
    /// 验证场景：缺少演练记录时健康检查应返回 Degraded。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BackupHealthCheck_WhenDrillRecordMissing_ShouldReturnDegraded() {
        var tempRootPath = Path.Combine(Path.GetTempPath(), $"backup-health-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRootPath);
        try {
            var backupDirectoryPath = Path.Combine(tempRootPath, "backup-artifacts");
            Directory.CreateDirectory(backupDirectoryPath);
            var latestBackupPath = Path.Combine(backupDirectoryPath, "sorting-hub-SortingHub-20260507010000.sql");
            await File.WriteAllTextAsync(latestBackupPath, "backup");
            File.SetLastWriteTime(latestBackupPath, DateTime.Now);
            var service = CreateBackupVerificationService(
                tempRootPath,
                providerName: "MySql",
                provider: new MySqlBackupProvider(),
                options: new BackupOptions {
                    IsEnabled = true,
                    DryRun = false,
                    BackupDirectory = "backup-artifacts",
                    BackupFileNamePrefix = "sorting-hub",
                    VerificationIntervalMinutes = 60,
                    ExpectedBackupWithinHours = 24,
                    RestoreDrillDirectory = "drill-records"
                });
            await service.ExecuteAsync(CancellationToken.None);
            var healthCheck = new BackupHealthCheck(service);

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Degraded, result.Status);
            Assert.False((bool)result.Data["hasRestoreDrillRecord"]);
        }
        finally {
            Directory.Delete(tempRootPath, recursive: true);
        }
    }

    /// <summary>
    /// 创建备份治理验证服务。
    /// </summary>
    /// <param name="contentRootPath">内容根目录。</param>
    /// <param name="providerName">配置 Provider。</param>
    /// <param name="provider">备份提供器。</param>
    /// <param name="options">备份配置。</param>
    /// <returns>备份治理验证服务。</returns>
    private static BackupVerificationService CreateBackupVerificationService(
        string contentRootPath,
        string providerName,
        IBackupProvider provider,
        BackupOptions options) {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Provider"] = providerName,
                [$"ConnectionStrings:{providerName}"] = providerName == "MySql"
                    ? "Server=127.0.0.1;Port=3306;Database=SortingHub;User Id=root;Password=secret;"
                    : "Server=localhost;Initial Catalog=SortingHub;User Id=sa;Password=secret;"
            })
            .Build();
        var hostEnvironment = new TestHostEnvironment("Development") {
            ContentRootPath = contentRootPath
        };
        return new BackupVerificationService(
            configuration,
            hostEnvironment,
            new TestOptionsMonitor<BackupOptions>(options),
            provider,
            new RestoreDrillPlanner());
    }
}
