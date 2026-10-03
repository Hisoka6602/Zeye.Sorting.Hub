using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Host.Options;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>无页面访问时也持续采样实际资源，按状态变化及十五分钟提醒窗口记录压力与恢复。</summary>
public sealed class RuntimeResourceMonitorHostedService(HealthCheckService healthChecks, IOptions<ResourceThresholdsOptions> options) : BackgroundService {
    /// <summary>资源压力与恢复日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        HealthStatus? lastStatus = null;
        var lastWarningTimestamp = Stopwatch.GetTimestamp();
        while (!stoppingToken.IsCancellationRequested) {
            try {
                var report = await healthChecks.CheckHealthAsync(static registration => registration.Name == "runtime-resources", stoppingToken);
                if (report.Status != lastStatus || report.Status != HealthStatus.Healthy && Stopwatch.GetElapsedTime(lastWarningTimestamp) >= TimeSpan.FromMinutes(15)) {
                    var detail = string.Join("；", report.Entries.Values.Select(static entry => entry.Description));
                    if (report.Status == HealthStatus.Healthy) Logger.Info("运行资源监测正常或已恢复。{Detail}", detail);
                    else { Logger.Warn("运行资源压力告警。{Detail}", detail); lastWarningTimestamp = Stopwatch.GetTimestamp(); }
                    lastStatus = report.Status;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { Logger.Info("运行资源监测已停止。"); break; }
            catch (Exception exception) { Logger.Error(exception, "运行资源监测周期失败，下个周期重试。"); }
            try { await Task.Delay(TimeSpan.FromSeconds(options.Value.SampleIntervalSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { Logger.Info("运行资源监测等待已取消。"); break; }
        }
    }
}
