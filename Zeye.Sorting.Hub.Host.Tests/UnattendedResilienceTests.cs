using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OptionValues = Microsoft.Extensions.Options.Options;
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Host.Options;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>验证无人值守中的故障恢复、独立容量字典上限和真实资源告警。</summary>
public sealed class UnattendedResilienceTests {
    /// <summary>一次观测提交故障只结束当前周期，下一周期继续运行并允许正常停止。</summary>
    [Fact]
    public async Task AutoTuningCycleRecoversAfterAnUnexpectedFailure() {
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        var observability = new TestObservability {
            BeforeEmitMetric = name => {
                if (name != "autotuning.analysis.window_size") return;
                if (Interlocked.Increment(ref attempts) == 1) throw new IOException("一次性观测故障");
                recovered.TrySetResult();
            }
        };
        using var service = CreateAutoTuningService(observability);
        await service.StartAsync(default);
        try { await recovered.Task.WaitAsync(TimeSpan.FromSeconds(8)); }
        finally { using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(3)); await service.StopAsync(stop.Token); }
        Assert.True(attempts >= 2);
        Assert.True(service.ExecuteTask!.IsCompletedSuccessfully);
    }

    /// <summary>热度字典为空时，容量预测字典仍裁剪到五百张表并清除过期记录。</summary>
    [Fact]
    public void IndependentCapacityTrackingRemainsBoundedWithoutHotTableSignals() {
        using var service = CreateAutoTuningService(new TestObservability());
        var field = typeof(DatabaseAutoTuningHostedService).GetField("_capacitySnapshotsByTable", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var dictionary = (IDictionary)field.GetValue(service)!;
        var queueType = dictionary.GetType().GetGenericArguments()[1];
        var snapshotType = queueType.GetGenericArguments()[0];
        for (var index = 0; index < 700; index++) {
            var queue = Activator.CreateInstance(queueType)!;
            var snapshot = Activator.CreateInstance(snapshotType, DateTime.Now.AddMinutes(-index), 100L, 1)!;
            queueType.GetMethod("Enqueue")!.Invoke(queue, [snapshot]);
            dictionary.Add("Parcel_" + index, queue);
        }
        var expiredQueue = Activator.CreateInstance(queueType)!;
        queueType.GetMethod("Enqueue")!.Invoke(expiredQueue, [Activator.CreateInstance(snapshotType, DateTime.Now.AddDays(-2), 10L, 1)!]);
        dictionary.Add("expired", expiredQueue);
        typeof(DatabaseAutoTuningHostedService).GetMethod("PruneTrackingState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(service, null);
        Assert.Equal(500, dictionary.Count);
        Assert.False(dictionary.Contains("expired"));
        Assert.True(dictionary.Contains("Parcel_0"));
        Assert.False(dictionary.Contains("Parcel_699"));
    }

    /// <summary>资源检查使用实际进程采样，阈值超限时降级，关闭阈值时仍返回真实指标。</summary>
    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public async Task RuntimeResourceHealthUsesActualProcessMeasurements(int memoryThreshold, bool shouldDegrade) {
        var check = new RuntimeResourceHealthCheck(
            OptionValues.Create(new ResourceThresholdsOptions { MemoryWarningThresholdMB = memoryThreshold, HandleWarningThreshold = 0, MinimumDiskFreeMB = 0 }),
            new TestHostEnvironment("Development") { ContentRootPath = Path.GetTempPath() }, OptionValues.Create(new BackupOptions()),
            new TestOptionsMonitor<LogCleanupSettings>(new LogCleanupSettings()));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(shouldDegrade ? HealthStatus.Degraded : HealthStatus.Healthy, result.Status);
        Assert.True(Assert.IsType<decimal>(result.Data["workingSetMB"]) > 1);
        Assert.True(Assert.IsType<int>(result.Data["handleCount"]) > 0);
    }

    /// <summary>构建一秒周期的只读自动调优实例，所有危险动作保持默认阻断。</summary>
    private static DatabaseAutoTuningHostedService CreateAutoTuningService(TestObservability observability) {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Persistence:AutoTuning:AnalyzeIntervalSeconds"] = "1"
        }).Build();
        return new DatabaseAutoTuningHostedService(observability, new FixedPlanProbe(), new EmptyServiceScopeFactory(), new TestDialect(),
            new SlowQueryAutoTuningPipeline(configuration, observability), configuration, OptionValues.Create(new ResourceThresholdsOptions()));
    }
}
