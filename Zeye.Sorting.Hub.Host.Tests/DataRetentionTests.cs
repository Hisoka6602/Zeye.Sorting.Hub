using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.AuditLogs;
using Zeye.Sorting.Hub.Domain.Enums.DataGovernance;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;
using Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 数据保留治理测试。
/// </summary>
public sealed class DataRetentionTests {
    /// <summary>
    /// dry-run 模式下应仅输出计划，不删除任何数据。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task DataRetentionExecutor_WhenDryRunEnabled_ShouldOnlyPlanWithoutDeletingData() {
        var serviceProvider = BuildServiceProvider(enableGuard: false, dryRun: true, allowDangerousActionExecution: false);
        await SeedRetentionDataAsync(serviceProvider, includeFreshRecords: false);
        var executor = serviceProvider.GetRequiredService<DataRetentionExecutor>();
        var healthCheck = serviceProvider.GetRequiredService<DataRetentionHealthCheck>();

        var record = await executor.ExecuteAsync(CancellationToken.None);
        var healthResult = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(ActionIsolationDecision.DryRunOnly, record.Decision);
        Assert.Equal(6, record.PlannedCount);
        Assert.Equal(0, record.ExecutedCount);
        Assert.Equal(HealthStatus.Degraded, healthResult.Status);

        await using var dbContext = await serviceProvider.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await dbContext.Set<WebRequestAuditLog>().CountAsync());
        Assert.Equal(1, await dbContext.Set<OutboxMessage>().CountAsync());
        Assert.Equal(1, await dbContext.Set<InboxMessage>().CountAsync());
        Assert.Equal(1, await dbContext.Set<IdempotencyRecord>().CountAsync());
        Assert.Equal(1, await dbContext.Set<ArchiveTask>().CountAsync());
        Assert.Equal(1, serviceProvider.GetRequiredService<DeadLetterWriteStore>().Count);
        Assert.Equal(0, serviceProvider.GetRequiredService<SlowQueryProfileStore>().GetTopProfiles().TotalFingerprintCount);
    }

    /// <summary>
    /// 允许真实执行时应按策略删除过期数据并保留新数据。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task DataRetentionExecutor_WhenExecutionAllowed_ShouldDeleteExpiredDataOnly() {
        var serviceProvider = BuildServiceProvider(dryRun: false, allowDangerousActionExecution: true);
        await SeedRetentionDataAsync(serviceProvider, includeFreshRecords: true);
        var executor = serviceProvider.GetRequiredService<DataRetentionExecutor>();
        var healthCheck = serviceProvider.GetRequiredService<DataRetentionHealthCheck>();

        var record = await executor.ExecuteAsync(CancellationToken.None);
        var healthResult = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(ActionIsolationDecision.Execute, record.Decision);
        Assert.Equal(6, record.ExecutedCount);
        Assert.Equal(0, record.FailedPolicyCount);
        Assert.Equal(HealthStatus.Healthy, healthResult.Status);

        await using var dbContext = await serviceProvider.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContextAsync();
        Assert.Equal(1, await dbContext.Set<WebRequestAuditLog>().CountAsync());
        Assert.Equal("trace-fresh", (await dbContext.Set<WebRequestAuditLog>().SingleAsync()).TraceId);
        Assert.Equal(1, await dbContext.Set<OutboxMessage>().CountAsync());
        Assert.Equal(1, await dbContext.Set<InboxMessage>().CountAsync());
        Assert.Equal(1, await dbContext.Set<IdempotencyRecord>().CountAsync());
        Assert.Equal(1, await dbContext.Set<ArchiveTask>().CountAsync());
        Assert.Equal(1, serviceProvider.GetRequiredService<DeadLetterWriteStore>().Count);
        Assert.Equal(0, serviceProvider.GetRequiredService<SlowQueryProfileStore>().GetTopProfiles().TotalFingerprintCount);
    }

    /// <summary>
    /// 关闭治理时健康检查应返回 Healthy。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task DataRetentionHealthCheck_WhenRetentionDisabled_ShouldReturnHealthy() {
        var serviceProvider = BuildServiceProvider(isEnabled: false, dryRun: true, allowDangerousActionExecution: false);
        var executor = serviceProvider.GetRequiredService<DataRetentionExecutor>();
        var healthCheck = serviceProvider.GetRequiredService<DataRetentionHealthCheck>();

        var record = await executor.ExecuteAsync(CancellationToken.None);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.False(record.IsEnabled);
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    /// <summary>
    /// 构建测试服务容器。
    /// </summary>
    /// <param name="isEnabled">是否启用。</param>
    /// <param name="dryRun">是否 dry-run。</param>
    /// <param name="allowDangerousActionExecution">是否允许真实执行。</param>
    /// <returns>服务提供器。</returns>
    private static ServiceProvider BuildServiceProvider(
        bool isEnabled = true,
        bool enableGuard = true,
        bool dryRun = true,
        bool allowDangerousActionExecution = false) {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Retention:IsEnabled"] = isEnabled.ToString(),
                ["Persistence:Retention:EnableGuard"] = enableGuard.ToString(),
                ["Persistence:Retention:AllowDangerousActionExecution"] = allowDangerousActionExecution.ToString(),
                ["Persistence:Retention:DryRun"] = dryRun.ToString(),
                ["Persistence:Retention:BatchSize"] = "10",
                ["Persistence:Retention:PollIntervalMinutes"] = "60",
                ["Persistence:Retention:Policies:0:Name"] = DataRetentionPolicy.WebRequestAuditLogName,
                ["Persistence:Retention:Policies:0:RetentionDays"] = "30",
                ["Persistence:Retention:Policies:1:Name"] = DataRetentionPolicy.OutboxMessageName,
                ["Persistence:Retention:Policies:1:RetentionDays"] = "30",
                ["Persistence:Retention:Policies:2:Name"] = DataRetentionPolicy.InboxMessageName,
                ["Persistence:Retention:Policies:2:RetentionDays"] = "30",
                ["Persistence:Retention:Policies:3:Name"] = DataRetentionPolicy.IdempotencyRecordName,
                ["Persistence:Retention:Policies:3:RetentionDays"] = "30",
                ["Persistence:Retention:Policies:4:Name"] = DataRetentionPolicy.ArchiveTaskName,
                ["Persistence:Retention:Policies:4:RetentionDays"] = "30",
                ["Persistence:Retention:Policies:5:Name"] = DataRetentionPolicy.DeadLetterWriteEntryName,
                ["Persistence:Retention:Policies:5:RetentionDays"] = "30",
                ["Persistence:Retention:Policies:6:Name"] = DataRetentionPolicy.SlowQueryProfileName,
                ["Persistence:Retention:Policies:6:RetentionDays"] = "30",
                ["Persistence:AutoTuning:SlowQueryProfile:IsEnabled"] = "true",
                ["Persistence:AutoTuning:SlowQueryThresholdMilliseconds"] = "1",
                ["Persistence:AutoTuning:SlowQueryProfile:WindowMinutes"] = "1000000",
                ["Persistence:AutoTuning:SlowQueryProfile:TopN"] = "10",
                ["Persistence:AutoTuning:SlowQueryProfile:MaxFingerprintCount"] = "10",
                ["Persistence:AutoTuning:SlowQueryProfile:MaxSampleCountPerFingerprint"] = "10"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContextFactory<SortingHubDbContext>(options =>
            options.UseInMemoryDatabase($"data-retention-tests-{Guid.NewGuid():N}"));
        services.AddOptions<DataRetentionOptions>()
            .Configure(options => {
                options.IsEnabled = configuration.GetValue<bool>("Persistence:Retention:IsEnabled");
                options.EnableGuard = configuration.GetValue<bool>("Persistence:Retention:EnableGuard");
                options.AllowDangerousActionExecution = configuration.GetValue<bool>("Persistence:Retention:AllowDangerousActionExecution");
                options.DryRun = configuration.GetValue<bool>("Persistence:Retention:DryRun");
                options.BatchSize = configuration.GetValue<int>("Persistence:Retention:BatchSize");
                options.PollIntervalMinutes = configuration.GetValue<int>("Persistence:Retention:PollIntervalMinutes");
                options.Policies = Enumerable.Range(0, 7)
                    .Select(index => new DataRetentionPolicy {
                        Name = configuration[$"Persistence:Retention:Policies:{index}:Name"]!,
                        RetentionDays = configuration.GetValue<int>($"Persistence:Retention:Policies:{index}:RetentionDays")
                    })
                    .ToArray();
            });
        services.AddSingleton<DeadLetterWriteStore>(_ => new DeadLetterWriteStore(16));
        services.AddSingleton<SlowQueryProfileStore>();
        services.AddSingleton<DataRetentionPlanner>();
        services.AddSingleton<DataRetentionExecutor>();
        services.AddSingleton<DataRetentionHealthCheck>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 写入测试数据。
    /// </summary>
    /// <param name="serviceProvider">服务容器。</param>
    /// <param name="includeFreshRecords">是否补充未过期数据。</param>
    /// <returns>异步任务。</returns>
    private static async Task SeedRetentionDataAsync(ServiceProvider serviceProvider, bool includeFreshRecords) {
        var expiredAt = LocalTimeTestConstraint.CreateLocalTime(2026, 3, 1, 8, 0, 0);
        var freshAt = LocalTimeTestConstraint.CreateLocalTime(2026, 5, 1, 8, 0, 0);
        await using var dbContext = await serviceProvider.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContextAsync();

        var expiredAuditLog = CreateAuditLog(1, expiredAt, "trace-expired");
        var expiredOutboxMessage = CreateOutboxMessage();
        var expiredInboxMessage = CreateInboxMessage();
        var expiredIdempotencyRecord = CreateIdempotencyRecord();
        var expiredArchiveTask = CreateArchiveTask();
        dbContext.Set<WebRequestAuditLog>().Add(expiredAuditLog);
        dbContext.Set<OutboxMessage>().Add(expiredOutboxMessage);
        dbContext.Set<InboxMessage>().Add(expiredInboxMessage);
        dbContext.Set<IdempotencyRecord>().Add(expiredIdempotencyRecord);
        dbContext.Set<ArchiveTask>().Add(expiredArchiveTask);
        if (includeFreshRecords) {
            dbContext.Set<WebRequestAuditLog>().Add(CreateAuditLog(2, freshAt, "trace-fresh"));
            dbContext.Set<OutboxMessage>().Add(CreateOutboxMessage());
            dbContext.Set<InboxMessage>().Add(CreateInboxMessage());
            dbContext.Set<IdempotencyRecord>().Add(CreateIdempotencyRecord());
            dbContext.Set<ArchiveTask>().Add(CreateArchiveTask());
        }

        await dbContext.SaveChangesAsync();
        AdjustOutboxMessageTimes(dbContext, expiredOutboxMessage, expiredAt);
        AdjustInboxMessageTimes(dbContext, expiredInboxMessage, expiredAt);
        AdjustIdempotencyRecordTimes(dbContext, expiredIdempotencyRecord, expiredAt);
        AdjustArchiveTaskTimes(dbContext, expiredArchiveTask, expiredAt);
        if (includeFreshRecords) {
            var freshInboxMessage = await dbContext.Set<InboxMessage>().OrderByDescending(x => x.Id).FirstAsync();
            var freshOutboxMessage = await dbContext.Set<OutboxMessage>().OrderByDescending(x => x.Id).FirstAsync();
            var freshIdempotencyRecord = await dbContext.Set<IdempotencyRecord>().OrderByDescending(x => x.Id).FirstAsync();
            var freshArchiveTask = await dbContext.Set<ArchiveTask>().OrderByDescending(x => x.Id).FirstAsync();
            AdjustInboxMessageTimes(dbContext, freshInboxMessage, freshAt);
            AdjustOutboxMessageTimes(dbContext, freshOutboxMessage, freshAt);
            AdjustIdempotencyRecordTimes(dbContext, freshIdempotencyRecord, freshAt);
            AdjustArchiveTaskTimes(dbContext, freshArchiveTask, freshAt);
        }

        await dbContext.SaveChangesAsync();

        var deadLetterStore = serviceProvider.GetRequiredService<DeadLetterWriteStore>();
        deadLetterStore.Add(new DeadLetterWriteEntry(CreateParcel(1001), expiredAt, 3, "expired-deadletter", expiredAt));
        if (includeFreshRecords) {
            deadLetterStore.Add(new DeadLetterWriteEntry(CreateParcel(1002), freshAt, 1, "fresh-deadletter", freshAt));
        }

        var slowQueryStore = serviceProvider.GetRequiredService<SlowQueryProfileStore>();
        slowQueryStore.Record(new SlowQuerySample(
            commandText: "select * from parcels where id = 1",
            sqlFingerprint: "fingerprint-expired",
            elapsedMilliseconds: 120,
            affectedRows: 1,
            isError: false,
            isTimeout: false,
            isDeadlock: false,
            occurredTime: expiredAt));
        if (includeFreshRecords) {
            slowQueryStore.Record(new SlowQuerySample(
                commandText: "select * from parcels where id = 2",
                sqlFingerprint: "fingerprint-fresh",
                elapsedMilliseconds: 160,
                affectedRows: 1,
                isError: false,
                isTimeout: false,
                isDeadlock: false,
                occurredTime: freshAt));
        }
    }

    /// <summary>
    /// 创建测试审计日志。
    /// </summary>
    /// <param name="id">主键。</param>
    /// <param name="createdAt">创建时间。</param>
    /// <param name="traceId">TraceId。</param>
    /// <returns>审计日志。</returns>
    private static WebRequestAuditLog CreateAuditLog(long id, DateTime createdAt, string traceId) {
        return new WebRequestAuditLog {
            Id = id,
            TraceId = traceId,
            CorrelationId = $"corr-{id}",
            SpanId = $"span-{id}",
            OperationName = "Retention.Test",
            RequestMethod = "GET",
            RequestScheme = "http",
            RequestHost = "localhost",
            RequestPath = "/api/retention",
            RequestRouteTemplate = "/api/retention",
            UserName = "tester",
            RequestPayloadType = WebRequestPayloadType.Json,
            ResponsePayloadType = WebResponsePayloadType.Json,
            ResourceId = id.ToString(),
            AuditResourceType = AuditResourceType.Api,
            StartedAt = createdAt,
            EndedAt = createdAt.AddSeconds(1),
            CreatedAt = createdAt,
            DurationMs = 100,
            StatusCode = 200,
            IsSuccess = true,
            Detail = new WebRequestAuditLogDetail {
                StartedAt = createdAt,
                RequestUrl = "http://localhost/api/retention",
                RequestHeadersJson = "{}",
                ResponseHeadersJson = "{}"
            }
        };
    }

    /// <summary>
    /// 创建测试 Outbox 消息。
    /// </summary>
    /// <param name="completedAt">完成时间。</param>
    /// <returns>Outbox 消息。</returns>
    private static OutboxMessage CreateOutboxMessage() {
        var message = OutboxMessage.CreatePending("Retention.Test", "{}");
        message.MarkProcessing();
        message.MarkDispatchSucceeded();
        return message;
    }

    /// <summary>
    /// 创建测试 Inbox 消息。
    /// </summary>
    /// <returns>Inbox 消息。</returns>
    private static InboxMessage CreateInboxMessage() {
        var message = InboxMessage.CreatePending("WCS", Guid.NewGuid().ToString("N"), "Retention.Test", DateTime.Now.AddDays(60));
        message.MarkProcessing();
        message.MarkSucceeded();
        return message;
    }

    /// <summary>
    /// 创建测试幂等记录。
    /// </summary>
    /// <param name="completedAt">完成时间。</param>
    /// <returns>幂等记录。</returns>
    private static IdempotencyRecord CreateIdempotencyRecord() {
        var record = IdempotencyRecord.CreatePending("WCS", "Retention", Guid.NewGuid().ToString("N"), new string('A', 64));
        record.MarkCompleted();
        return record;
    }

    /// <summary>
    /// 创建测试归档任务。
    /// </summary>
    /// <param name="completedAt">完成时间。</param>
    /// <returns>归档任务。</returns>
    private static ArchiveTask CreateArchiveTask() {
        var task = ArchiveTask.CreateDryRun(ArchiveTaskType.WebRequestAuditLogHistory, 7, "retention-test", null);
        task.MarkRunning();
        task.MarkCompleted(1, "done", "{}");
        return task;
    }

    /// <summary>
    /// 调整 Outbox 消息时间字段。
    /// </summary>
    /// <param name="message">Outbox 消息。</param>
    /// <param name="time">目标时间。</param>
    /// <returns>Outbox 消息。</returns>
    private static void AdjustOutboxMessageTimes(SortingHubDbContext dbContext, OutboxMessage message, DateTime time) {
        dbContext.Entry(message).Property(x => x.CreatedAt).CurrentValue = time;
        dbContext.Entry(message).Property(x => x.UpdatedAt).CurrentValue = time;
        dbContext.Entry(message).Property(x => x.CompletedAt).CurrentValue = time;
        dbContext.Entry(message).Property(x => x.LastAttemptedAt).CurrentValue = time;
    }

    /// <summary>
    /// 调整 Inbox 消息时间字段。
    /// </summary>
    /// <param name="dbContext">数据库上下文。</param>
    /// <param name="message">Inbox 消息。</param>
    /// <param name="time">目标时间。</param>
    private static void AdjustInboxMessageTimes(SortingHubDbContext dbContext, InboxMessage message, DateTime time) {
        dbContext.Entry(message).Property(x => x.CreatedAt).CurrentValue = time;
        dbContext.Entry(message).Property(x => x.UpdatedAt).CurrentValue = time;
        dbContext.Entry(message).Property(x => x.ProcessedAt).CurrentValue = time;
        dbContext.Entry(message).Property(x => x.LastAttemptedAt).CurrentValue = time;
        dbContext.Entry(message).Property(x => x.ExpiresAt).CurrentValue = time;
    }

    /// <summary>
    /// 调整幂等记录时间字段。
    /// </summary>
    /// <param name="record">幂等记录。</param>
    /// <param name="time">目标时间。</param>
    /// <returns>幂等记录。</returns>
    private static void AdjustIdempotencyRecordTimes(SortingHubDbContext dbContext, IdempotencyRecord record, DateTime time) {
        dbContext.Entry(record).Property(x => x.CreatedAt).CurrentValue = time;
        dbContext.Entry(record).Property(x => x.UpdatedAt).CurrentValue = time;
        dbContext.Entry(record).Property(x => x.CompletedAt).CurrentValue = time;
    }

    /// <summary>
    /// 调整归档任务时间字段。
    /// </summary>
    /// <param name="task">归档任务。</param>
    /// <param name="time">目标时间。</param>
    /// <returns>归档任务。</returns>
    private static void AdjustArchiveTaskTimes(SortingHubDbContext dbContext, ArchiveTask task, DateTime time) {
        dbContext.Entry(task).Property(x => x.CreatedAt).CurrentValue = time;
        dbContext.Entry(task).Property(x => x.UpdatedAt).CurrentValue = time;
        dbContext.Entry(task).Property(x => x.CompletedAt).CurrentValue = time;
        dbContext.Entry(task).Property(x => x.LastAttemptedAt).CurrentValue = time;
    }

    /// <summary>
    /// 创建测试 Parcel。
    /// </summary>
    /// <param name="id">主键。</param>
    /// <returns>Parcel。</returns>
    private static Parcel CreateParcel(long id) {
        var scannedTime = LocalTimeTestConstraint.CreateLocalTime(2026, 3, 1, 8, 0, 1);
        return Parcel.Create(
            id: id,
            parcelTimestamp: Math.Abs(scannedTime.Ticks),
            type: ParcelType.Normal,
            barCodes: id.ToString(),
            weight: 1.2m,
            workstationName: "WS-01",
            scannedTime: scannedTime,
            dischargeTime: scannedTime.AddSeconds(2),
            targetChuteId: 201,
            actualChuteId: 202,
            requestStatus: ApiRequestStatus.Success,
            bagCode: $"BAG-{id}",
            isSticking: false,
            length: 10m,
            width: 11m,
            height: 12m,
            volume: 1320m,
            hasImages: false,
            hasVideos: false,
            coordinate: "x:1,y:1");
    }
}
