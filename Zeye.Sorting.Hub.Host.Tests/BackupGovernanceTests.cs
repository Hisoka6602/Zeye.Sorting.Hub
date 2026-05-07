using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 备份治理测试。
/// </summary>
public sealed class BackupGovernanceTests {
    /// <summary>
    /// MySQL 配置层 Provider 名称。
    /// </summary>
    private const string MySqlProvider = "MySql";

    /// <summary>
    /// SQL Server 配置层 Provider 名称。
    /// </summary>
    private const string SqlServerProvider = "SqlServer";

    /// <summary>
    /// dry-run 模式下若存在新鲜备份文件，应返回健康状态并生成演练资产。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BackupVerificationService_WhenRecentBackupExists_ShouldReturnHealthyAndWriteArtifacts() {
        var rootPath = CreateTempDirectory();
        try {
            var serviceProvider = BuildServiceProvider(rootPath, MySqlProvider, isEnabled: true, dryRun: true);
            var options = serviceProvider.GetRequiredService<IOptions<BackupOptions>>().Value;
            var backupProvider = serviceProvider.GetRequiredService<IBackupProvider>();
            var backupVerificationService = serviceProvider.GetRequiredService<BackupVerificationService>();
            var healthCheck = serviceProvider.GetRequiredService<BackupHealthCheck>();
            var now = DateTime.Now;
            var backupDirectoryPath = Path.Combine(rootPath, options.BackupDirectory, backupProvider.ConfiguredProviderName);
            Directory.CreateDirectory(backupDirectoryPath);
            var backupFilePath = Path.Combine(backupDirectoryPath, $"sorting-hub-{now:yyyyMMddHHmmss}-SortingHubDb.sql");
            await File.WriteAllTextAsync(backupFilePath, "mock-backup");
            File.SetLastWriteTime(backupFilePath, now.AddHours(-1));

            var record = await backupVerificationService.ExecuteAsync(CancellationToken.None);
            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(BackupExecutionRecord.CompletedStatus, record.Status);
            Assert.True(record.HasBackupFile);
            Assert.True(record.IsBackupFileFresh);
            Assert.Equal(HealthStatus.Healthy, result.Status);
            Assert.True(File.Exists(record.RestoreRunbookPath));
            Assert.True(File.Exists(record.DrillRecordPath));
            Assert.Contains("mysqldump", record.CommandText, StringComparison.Ordinal);
        }
        finally {
            DeleteTempDirectory(rootPath);
        }
    }

    /// <summary>
    /// 未发现备份文件时应返回降级状态。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BackupHealthCheck_WhenBackupMissing_ShouldReturnDegraded() {
        var rootPath = CreateTempDirectory();
        try {
            var serviceProvider = BuildServiceProvider(rootPath, MySqlProvider, isEnabled: true, dryRun: true);
            var backupVerificationService = serviceProvider.GetRequiredService<BackupVerificationService>();
            var healthCheck = serviceProvider.GetRequiredService<BackupHealthCheck>();

            var record = await backupVerificationService.ExecuteAsync(CancellationToken.None);
            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(BackupExecutionRecord.FailedStatus, record.Status);
            Assert.False(record.HasBackupFile);
            Assert.Equal(HealthStatus.Degraded, result.Status);
        }
        finally {
            DeleteTempDirectory(rootPath);
        }
    }

    /// <summary>
    /// SQL Server Provider 应生成 .bak 备份文件与 BACKUP DATABASE 命令。
    /// </summary>
    [Fact]
    public void SqlServerBackupProvider_BuildPlan_ShouldUseBakFileAndSqlcmdCommand() {
        var provider = new SqlServerBackupProvider();
        var options = new BackupOptions {
            BackupFilePrefix = "sorting-hub"
        };
        var generatedAtLocal = LocalTimeTestConstraint.CreateLocalTime(2026, 5, 7, 9, 0, 0);

        var plan = provider.BuildPlan(options, "/tmp/backup-tests", "SortingHubDb", generatedAtLocal);

        Assert.EndsWith(".bak", plan.PlannedBackupFilePath, StringComparison.Ordinal);
        Assert.Contains("BACKUP DATABASE [SortingHubDb]", plan.CommandText, StringComparison.Ordinal);
        Assert.Contains("sqlcmd", plan.CommandText, StringComparison.Ordinal);
    }

    /// <summary>
    /// MySQL Provider 应拒绝不安全数据库名。
    /// 数据库名包含分号时会被 DatabaseIdentifierPolicy.NormalizeDatabaseName 拒绝，
    /// 用于验证命令生成链路不会把危险字符直接拼接到 shell 命令中。
    /// </summary>
    [Fact]
    public void MySqlBackupProvider_BuildPlan_WhenDatabaseNameUnsafe_ShouldThrow() {
        var provider = new MySqlBackupProvider();
        var options = new BackupOptions {
            BackupFilePrefix = "sorting-hub"
        };

        Assert.Throws<InvalidOperationException>(() => provider.BuildPlan(options, "/tmp/backup-tests", "SortingHubDb;drop", DateTime.Now));
    }

    /// <summary>
    /// SQL Server 命令应转义路径中的单引号。
    /// </summary>
    [Fact]
    public void SqlServerBackupProvider_BuildPlan_WhenPathContainsQuote_ShouldEscapeLiteral() {
        var provider = new SqlServerBackupProvider();
        var options = new BackupOptions {
            BackupFilePrefix = "sorting-hub"
        };

        var plan = provider.BuildPlan(options, "/tmp/backup's", "SortingHubDb", DateTime.Now);

        Assert.Contains("backup''s", plan.CommandText, StringComparison.Ordinal);
    }

    /// <summary>
    /// 关闭治理时健康检查应返回 Healthy。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BackupHealthCheck_WhenBackupDisabled_ShouldReturnHealthy() {
        var rootPath = CreateTempDirectory();
        try {
            var serviceProvider = BuildServiceProvider(rootPath, SqlServerProvider, isEnabled: false, dryRun: true, includeConnectionStrings: false);
            var backupVerificationService = serviceProvider.GetRequiredService<BackupVerificationService>();
            var healthCheck = serviceProvider.GetRequiredService<BackupHealthCheck>();

            var record = await backupVerificationService.ExecuteAsync(CancellationToken.None);
            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.False(record.IsEnabled);
            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally {
            DeleteTempDirectory(rootPath);
        }
    }

    /// <summary>
    /// 构建测试服务容器。
    /// </summary>
    /// <param name="rootPath">测试根目录。</param>
    /// <param name="providerName">Provider 名称。</param>
    /// <param name="isEnabled">是否启用。</param>
    /// <param name="dryRun">是否 dry-run。</param>
    /// <param name="includeConnectionStrings">是否包含连接字符串。</param>
    /// <returns>服务容器。</returns>
    private static ServiceProvider BuildServiceProvider(string rootPath, string providerName, bool isEnabled, bool dryRun, bool includeConnectionStrings = true) {
        var settings = new Dictionary<string, string?> {
            ["Persistence:Provider"] = providerName,
            ["Persistence:Backup:IsEnabled"] = isEnabled.ToString(),
            ["Persistence:Backup:DryRun"] = dryRun.ToString(),
            ["Persistence:Backup:PollIntervalMinutes"] = "60",
            ["Persistence:Backup:MaxAllowedBackupAgeHours"] = "24",
            ["Persistence:Backup:BackupDirectory"] = "backup-artifacts",
            ["Persistence:Backup:BackupFilePrefix"] = "sorting-hub",
            ["Persistence:Backup:RestoreRunbookDirectory"] = "backup-runbooks",
            ["Persistence:Backup:DrillRecordDirectory"] = "drill-records"
        };
        if (includeConnectionStrings) {
            settings["ConnectionStrings:MySql"] = "Server=localhost;Database=SortingHubDb;User Id=tester;Password=<test-password>;";
            settings["ConnectionStrings:SqlServer"] = "Server=localhost;Initial Catalog=SortingHubDb;User Id=tester;Password=<test-password>;";
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IOptions<BackupOptions>>(Microsoft.Extensions.Options.Options.Create(new BackupOptions {
            IsEnabled = isEnabled,
            DryRun = dryRun,
            PollIntervalMinutes = 60,
            MaxAllowedBackupAgeHours = 24,
            BackupDirectory = "backup-artifacts",
            BackupFilePrefix = "sorting-hub",
            RestoreRunbookDirectory = "backup-runbooks",
            DrillRecordDirectory = "drill-records"
        }));
        services.AddSingleton<IHostEnvironment>(_ => new TestHostEnvironment("Development") {
            ContentRootPath = rootPath
        });
        services.AddSingleton<RestoreDrillPlanner>();
        services.AddSingleton<IBackupProvider>(providerName == SqlServerProvider
            ? new SqlServerBackupProvider()
            : new MySqlBackupProvider());
        services.AddSingleton<BackupVerificationService>();
        services.AddSingleton<BackupHealthCheck>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建测试目录。
    /// </summary>
    /// <returns>目录路径。</returns>
    private static string CreateTempDirectory() {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"zeye-sorting-hub-backup-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    /// <summary>
    /// 删除测试目录。
    /// </summary>
    /// <param name="directoryPath">目录路径。</param>
    private static void DeleteTempDirectory(string directoryPath) {
        if (Directory.Exists(directoryPath)) {
            Directory.Delete(directoryPath, recursive: true);
        }
    }
}
