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
            var service = new LogCleanupService(new SafeExecutor(), settingsMonitor, new NullAutoTuningObservability());
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
