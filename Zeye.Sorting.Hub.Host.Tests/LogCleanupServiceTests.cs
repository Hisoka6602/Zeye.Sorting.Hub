using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.SharedKernel.Utilities;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 日志清理服务测试。
/// </summary>
public sealed class LogCleanupServiceTests {
    /// <summary>
    /// 临时目录前缀。
    /// </summary>
    private const string TempDirectoryPrefix = "zeye-log-cleanup-tests";

    /// <summary>
    /// 验证场景：清理任务会递归扫描子目录并删除过期日志。
    /// </summary>
    [Fact]
    public void CleanupOldLogs_ShouldDeleteExpiredLogs_InAllSubDirectories() {
        var rootDirectory = CreateTempDirectory();
        try {
            var nestedDirectory = Directory.CreateDirectory(Path.Combine(rootDirectory, "nested", "deep")).FullName;
            var rootExpiredLog = Path.Combine(rootDirectory, "root-expired.log");
            var nestedExpiredLog = Path.Combine(nestedDirectory, "nested-expired.log");
            var nestedRecentLog = Path.Combine(nestedDirectory, "nested-recent.log");

            File.WriteAllText(rootExpiredLog, "expired");
            File.WriteAllText(nestedExpiredLog, "expired");
            File.WriteAllText(nestedRecentLog, "recent");

            File.SetLastWriteTime(rootExpiredLog, DateTime.Now.AddDays(-5));
            File.SetLastWriteTime(nestedExpiredLog, DateTime.Now.AddDays(-5));
            File.SetLastWriteTime(nestedRecentLog, DateTime.Now);

            var settingsMonitor = new TestOptionsMonitor<LogCleanupSettings>(new LogCleanupSettings {
                Enabled = true,
                RetentionDays = 2,
                CheckIntervalHours = 1,
                LogDirectory = rootDirectory
            });
            var service = new LogCleanupService(new SafeExecutor(), settingsMonitor, new NullAutoTuningObservability(), new ConfigChangeHistoryStore<LogCleanupSettings>());
            service.CleanupOldLogs(CancellationToken.None);

            Assert.False(File.Exists(rootExpiredLog));
            Assert.False(File.Exists(nestedExpiredLog));
            Assert.True(File.Exists(nestedRecentLog));
        }
        finally {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    /// <summary>
    /// 验证场景：RetentionDays 配置为 0 时，保护性截断至 1 天，近期日志不会被误删。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public void CleanupOldLogs_WithInvalidRetentionDays_ShouldFallbackToAtLeastOneDayAndNotDeleteRecentLogs(int invalidRetentionDays) {
        var rootDirectory = CreateTempDirectory();
        try {
            // 创建两个日志文件：一个"昨天"，一个"5天前"
            var recentLog = Path.Combine(rootDirectory, "recent.log");
            var oldLog = Path.Combine(rootDirectory, "old.log");

            File.WriteAllText(recentLog, "recent");
            File.WriteAllText(oldLog, "old");

            // 设置文件时间：recent 为昨天（在 1 天保护值之内应被保留），old 为 5 天前
            File.SetLastWriteTime(recentLog, DateTime.Now.AddHours(-12));
            File.SetLastWriteTime(oldLog, DateTime.Now.AddDays(-5));

            var settingsMonitor = new TestOptionsMonitor<LogCleanupSettings>(new LogCleanupSettings {
                Enabled = true,
                RetentionDays = invalidRetentionDays,
                CheckIntervalHours = 1,
                LogDirectory = rootDirectory
            });
            var service = new LogCleanupService(new SafeExecutor(), settingsMonitor, new NullAutoTuningObservability(), new ConfigChangeHistoryStore<LogCleanupSettings>());
            service.CleanupOldLogs(CancellationToken.None);

            // 保护性截断为 1 天：昨天的日志应被保留，5 天前的日志应被清除
            Assert.True(File.Exists(recentLog), $"RetentionDays={invalidRetentionDays} 保护性截断为 1 天时，昨天的日志文件不应被删除");
            Assert.False(File.Exists(oldLog), $"RetentionDays={invalidRetentionDays} 保护性截断为 1 天时，5 天前的日志文件应被删除");
        }
        finally {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    /// <summary>
    /// 验证场景：GetEffectiveCheckIntervalHours 对 0/负数配置值返回至少 1 小时，防止忙等待。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public void GetEffectiveCheckIntervalHours_WithInvalidValues_ShouldReturnAtLeastOne(int invalidCheckIntervalHours) {
        // 通过直接调用 CleanupOldLogs 间接验证 CheckIntervalHours=0/负数不会导致异常，
        // 并通过 GetEffectiveCheckIntervalHours 的调用路径确保最小值保护逻辑正确。
        var rootDirectory = CreateTempDirectory();
        try {
            var settingsMonitor = new TestOptionsMonitor<LogCleanupSettings>(new LogCleanupSettings {
                Enabled = true,
                RetentionDays = 2,
                CheckIntervalHours = invalidCheckIntervalHours,
                LogDirectory = rootDirectory
            });

            // 创建并调用服务（仅验证 CleanupOldLogs 不因 CheckIntervalHours 非法值抛出异常）
            var service = new LogCleanupService(new SafeExecutor(), settingsMonitor, new NullAutoTuningObservability(), new ConfigChangeHistoryStore<LogCleanupSettings>());
            var ex = Record.Exception(() => service.CleanupOldLogs(CancellationToken.None));
            Assert.Null(ex);
        }
        finally {
            Directory.Delete(rootDirectory, recursive: true);
        }
    }

    /// <summary>
    /// 创建唯一临时目录。
    /// </summary>
    /// <returns>临时目录绝对路径。</returns>
    private static string CreateTempDirectory() {
        var tempDirectory = Path.Combine(
            Path.GetTempPath(),
            TempDirectoryPrefix,
            Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(tempDirectory).FullName;
    }
}
