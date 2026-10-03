using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Host.Options;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>读取真实进程资源和持久化目录剩余空间，阈值超限进入深度诊断降级状态。</summary>
public sealed class RuntimeResourceHealthCheck(IOptions<ResourceThresholdsOptions> thresholds, IHostEnvironment environment, IOptions<BackupOptions> backups, IOptionsMonitor<LogCleanupSettings> logs) : IHealthCheck {
    /// <summary>资源采样异常日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        try {
            cancellationToken.ThrowIfCancellationRequested();
            using var process = Process.GetCurrentProcess();
            var options = thresholds.Value;
            var workingSet = process.WorkingSet64 / (1024m * 1024);
            var data = new Dictionary<string, object> {
                ["workingSetMB"] = decimal.Round(workingSet, 2),
                ["managedHeapMB"] = decimal.Round(GC.GetTotalMemory(false) / (1024m * 1024), 2),
                ["handleCount"] = process.HandleCount,
                ["memoryWarningThresholdMB"] = options.MemoryWarningThresholdMB,
                ["handleWarningThreshold"] = options.HandleWarningThreshold,
                ["minimumDiskFreeMB"] = options.MinimumDiskFreeMB
            };
            var warnings = new List<string>();
            if (options.MemoryWarningThresholdMB > 0 && workingSet >= options.MemoryWarningThresholdMB) warnings.Add("进程内存超过告警阈值");
            if (options.HandleWarningThreshold > 0 && process.HandleCount >= options.HandleWarningThreshold) warnings.Add("进程句柄超过告警阈值");
            if (options.MinimumDiskFreeMB > 0) {
                // 按最长挂载路径匹配实际文件系统，Linux 独立日志/备份挂载不能只检查根目录。
                var volumes = DriveInfo.GetDrives();
                var paths = new[] { environment.ContentRootPath, ResolveDirectory(logs.CurrentValue.LogDirectory), ResolveDirectory(backups.Value.BackupDirectory) };
                var checkedVolumes = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
                foreach (var path in paths) {
                    cancellationToken.ThrowIfCancellationRequested();
                    var volume = volumes.Where(drive => IsWithinVolume(path, drive.Name)).OrderByDescending(drive => drive.Name.Length).FirstOrDefault();
                    if (volume is null || !checkedVolumes.Add(volume.Name)) continue;
                    if (!volume.IsReady) { warnings.Add("持久化文件系统不可用：" + volume.Name); continue; }
                    var free = volume.AvailableFreeSpace / (1024m * 1024);
                    data["diskFreeMB:" + volume.Name] = decimal.Round(free, 2);
                    if (free < options.MinimumDiskFreeMB) warnings.Add("持久化文件系统剩余空间不足：" + volume.Name);
                }
            }
            return Task.FromResult(warnings.Count == 0 ? HealthCheckResult.Healthy("进程与持久化目录资源正常。", data) : HealthCheckResult.Degraded(string.Join("；", warnings), data: data));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { Logger.Info("运行资源采样已取消。"); throw; }
        catch (Exception exception) { Logger.Error(exception, "运行资源采样失败。"); return Task.FromResult(HealthCheckResult.Degraded("无法完成运行资源采样。", exception)); }
    }

    /// <summary>解析部署目录，不依赖启动时的当前工作目录。</summary>
    private string ResolveDirectory(string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path));

    /// <summary>检查路径段边界，避免把同名路径前缀误识别为文件系统。</summary>
    private static bool IsWithinVolume(string path, string volume) {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var normalized = Path.GetFullPath(path);
        return normalized.Equals(Path.TrimEndingDirectorySeparator(volume), comparison) || normalized.StartsWith(Path.EndsInDirectorySeparator(volume) ? volume : volume + Path.DirectorySeparatorChar, comparison);
    }
}
