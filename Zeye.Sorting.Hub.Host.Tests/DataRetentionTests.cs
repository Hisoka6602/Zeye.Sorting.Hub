using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
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
    /// 验证场景：计划器应统计多类保留候选并遵循批次上限。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task DataRetentionPlanner_WhenCandidatesExist_ShouldCountCandidatesByPolicy() {
        var databaseName = $"data-retention-planner-{Guid.NewGuid():N}";
        try {
            var options = BuildDbContextOptions(databaseName);
            await SeedRetentionCandidatesAsync(options);
            var planner = CreatePlanner(options, out _, out _);
            var retentionOptions = CreateRetentionOptions(batchSize: 2);

            var result = await planner.BuildCandidateCountsAsync(retentionOptions, CancellationToken.None);

            Assert.Equal(2, result[DataRetentionPolicy.WebRequestAuditLog]);
            Assert.Equal(1, result[DataRetentionPolicy.OutboxMessage]);
            Assert.Equal(1, result[DataRetentionPolicy.InboxMessage]);
            Assert.Equal(1, result[DataRetentionPolicy.IdempotencyRecord]);
            Assert.Equal(1, result[DataRetentionPolicy.ArchiveTask]);
            Assert.Equal(1, result[DataRetentionPolicy.DeadLetterWriteEntry]);
            Assert.Equal(1, result[DataRetentionPolicy.SlowQueryProfile]);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：执行器在 dry-run 模式下应生成成功记录与候选摘要。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task DataRetentionExecutor_WhenDryRunEnabled_ShouldStoreSucceededRecord() {
        var databaseName = $"data-retention-executor-{Guid.NewGuid():N}";
        try {
            var options = BuildDbContextOptions(databaseName);
            await SeedRetentionCandidatesAsync(options);
            var planner = CreatePlanner(options, out _, out _);
            var retentionOptions = CreateRetentionOptions(batchSize: 10);
            var executor = new DataRetentionExecutor(planner, new TestOptionsMonitor<DataRetentionOptions>(retentionOptions));

            var record = await executor.ExecuteAsync(CancellationToken.None);

            Assert.Equal(DataRetentionAuditRecord.SucceededStatus, record.Status);
            Assert.True(record.IsDryRun);
            Assert.True(record.TotalCandidateCount >= 7);
            Assert.Equal(record, executor.GetLatestRecord());
            LocalTimeTestConstraint.AssertIsLocalTime(record.RecordedAtLocal);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：存在候选记录时健康检查应返回 Degraded。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task DataRetentionHealthCheck_WhenCandidatesExist_ShouldReturnDegraded() {
        var databaseName = $"data-retention-health-{Guid.NewGuid():N}";
        try {
            var options = BuildDbContextOptions(databaseName);
            await SeedRetentionCandidatesAsync(options);
            var planner = CreatePlanner(options, out _, out _);
            var executor = new DataRetentionExecutor(planner, new TestOptionsMonitor<DataRetentionOptions>(CreateRetentionOptions(batchSize: 10)));
            await executor.ExecuteAsync(CancellationToken.None);
            var healthCheck = new DataRetentionHealthCheck(executor);

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Degraded, result.Status);
            Assert.True((int)result.Data["totalCandidateCount"] >= 7);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：未启用时健康检查应返回 Healthy。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task DataRetentionHealthCheck_WhenDisabled_ShouldReturnHealthy() {
        var databaseName = $"data-retention-disabled-{Guid.NewGuid():N}";
        try {
            var options = BuildDbContextOptions(databaseName);
            var planner = CreatePlanner(options, out _, out _);
            var retentionOptions = CreateRetentionOptions(batchSize: 10);
            retentionOptions.IsEnabled = false;
            var executor = new DataRetentionExecutor(planner, new TestOptionsMonitor<DataRetentionOptions>(retentionOptions));
            await executor.ExecuteAsync(CancellationToken.None);
            var healthCheck = new DataRetentionHealthCheck(executor);

            var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

            Assert.Equal(HealthStatus.Healthy, result.Status);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 种入保留候选测试数据。
    /// </summary>
    /// <param name="options">数据库配置。</param>
    /// <returns>异步任务。</returns>
    private static async Task SeedRetentionCandidatesAsync(DbContextOptions<SortingHubDbContext> options) {
        var oldTime = LocalTimeTestConstraint.CreateLocalTime(2026, 1, 1, 8, 0, 0);
        var oldCompletedTime = LocalTimeTestConstraint.CreateLocalTime(2026, 1, 15, 8, 0, 0);
        var pendingInboxExpiresAt = LocalTimeTestConstraint.CreateLocalTime(2099, 1, 1, 0, 0, 0);
        await using var dbContext = new SortingHubDbContext(options);

        dbContext.Set<WebRequestAuditLog>().AddRange(
            new WebRequestAuditLog {
                TraceId = "trace-1",
                CorrelationId = "corr-1",
                SpanId = "span-1",
                OperationName = "GET /api/parcels/1",
                RequestMethod = "GET",
                RequestScheme = "http",
                RequestHost = "localhost",
                RequestPath = "/api/parcels/1",
                RequestRouteTemplate = "/api/parcels/{id}",
                StartedAt = oldTime,
                EndedAt = oldTime.AddSeconds(1),
                DurationMs = 100,
                CreatedAt = oldTime
            },
            new WebRequestAuditLog {
                TraceId = "trace-2",
                CorrelationId = "corr-2",
                SpanId = "span-2",
                OperationName = "GET /api/parcels/2",
                RequestMethod = "GET",
                RequestScheme = "http",
                RequestHost = "localhost",
                RequestPath = "/api/parcels/2",
                RequestRouteTemplate = "/api/parcels/{id}",
                StartedAt = oldTime,
                EndedAt = oldTime.AddSeconds(1),
                DurationMs = 100,
                CreatedAt = oldTime
            });

        var outboxMessage = OutboxMessage.CreatePending("ParcelCreated", "{\"parcelId\":1}");
        outboxMessage.MarkProcessing();
        outboxMessage.MarkDispatchSucceeded();
        SetPrivateDateTime(outboxMessage, "UpdatedAt", oldCompletedTime);
        SetPrivateNullableDateTime(outboxMessage, "CompletedAt", oldCompletedTime);
        dbContext.Set<OutboxMessage>().Add(outboxMessage);

        var inboxMessage = InboxMessage.CreatePending("WCS", "MSG-1", "ParcelCreated", pendingInboxExpiresAt);
        inboxMessage.MarkProcessing();
        inboxMessage.MarkSucceeded();
        SetPrivateDateTime(inboxMessage, "UpdatedAt", oldCompletedTime);
        SetPrivateDateTime(inboxMessage, "ExpiresAt", oldCompletedTime);
        dbContext.Set<InboxMessage>().Add(inboxMessage);

        var idempotencyRecord = IdempotencyRecord.CreatePending("API", "CreateParcel", "BK-1", new string('A', IdempotencyRecord.PayloadHashLength));
        idempotencyRecord.MarkCompleted();
        SetPrivateDateTime(idempotencyRecord, "UpdatedAt", oldCompletedTime);
        SetPrivateNullableDateTime(idempotencyRecord, "CompletedAt", oldCompletedTime);
        dbContext.Set<IdempotencyRecord>().Add(idempotencyRecord);

        var archiveTask = ArchiveTask.CreateDryRun(Domain.Enums.DataGovernance.ArchiveTaskType.WebRequestAuditLogHistory, 30, "copilot", "retention-test");
        archiveTask.MarkRunning();
        archiveTask.MarkCompleted(12, "summary", "{}");
        SetPrivateDateTime(archiveTask, "UpdatedAt", oldCompletedTime);
        SetPrivateNullableDateTime(archiveTask, "CompletedAt", oldCompletedTime);
        dbContext.Set<ArchiveTask>().Add(archiveTask);

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// 创建测试计划器。
    /// </summary>
    /// <param name="options">数据库配置。</param>
    /// <param name="deadLetterWriteStore">死信存储。</param>
    /// <param name="slowQueryProfileStore">慢查询画像存储。</param>
    /// <returns>计划器。</returns>
    private static DataRetentionPlanner CreatePlanner(
        DbContextOptions<SortingHubDbContext> options,
        out DeadLetterWriteStore deadLetterWriteStore,
        out SlowQueryProfileStore slowQueryProfileStore) {
        deadLetterWriteStore = new DeadLetterWriteStore(capacity: 16);
        deadLetterWriteStore.Add(new DeadLetterWriteEntry(
            Parcel: CreateDeadLetterParcel(),
            FailedAtLocal: LocalTimeTestConstraint.CreateLocalTime(2026, 1, 10, 8, 0, 0),
            RetryCount: 3,
            ErrorMessage: "dead-letter",
            LastRetryAtLocal: LocalTimeTestConstraint.CreateLocalTime(2026, 1, 11, 8, 0, 0)));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:AutoTuning:SlowQueryThresholdMilliseconds"] = "500",
                ["Persistence:AutoTuning:SlowQueryProfile:IsEnabled"] = "true",
                ["Persistence:AutoTuning:SlowQueryProfile:WindowMinutes"] = "43200",
                ["Persistence:AutoTuning:SlowQueryProfile:TopN"] = "50",
                ["Persistence:AutoTuning:SlowQueryProfile:MaxFingerprintCount"] = "1000",
                ["Persistence:AutoTuning:SlowQueryProfile:MaxSampleCountPerFingerprint"] = "256"
            })
            .Build();
        slowQueryProfileStore = new SlowQueryProfileStore(configuration);
        slowQueryProfileStore.Record("SELECT * FROM Parcels WHERE Id = 1", TimeSpan.FromMilliseconds(1200));
        ForceSlowQueryLastSeenAtLocal(slowQueryProfileStore, LocalTimeTestConstraint.CreateLocalTime(2026, 1, 20, 8, 0, 0));
        return new DataRetentionPlanner(new SortingHubTestDbContextFactory(options), deadLetterWriteStore, slowQueryProfileStore);
    }

    /// <summary>
    /// 创建数据保留配置。
    /// </summary>
    /// <param name="batchSize">批次上限。</param>
    /// <returns>数据保留配置。</returns>
    private static DataRetentionOptions CreateRetentionOptions(int batchSize) {
        return new DataRetentionOptions {
            IsEnabled = true,
            DryRun = true,
            BatchSize = batchSize,
            ExecutionIntervalMinutes = 60,
            Policies = DataRetentionPolicy.CreateDefaultPolicies().Select(policy => new DataRetentionPolicy {
                Name = policy.Name,
                RetentionDays = policy.RetentionDays
            }).ToList()
        };
    }

    /// <summary>
    /// 创建测试数据库配置。
    /// </summary>
    /// <param name="databaseName">数据库名称。</param>
    /// <returns>数据库配置。</returns>
    private static DbContextOptions<SortingHubDbContext> BuildDbContextOptions(string databaseName) {
        return new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    /// <summary>
    /// 清理测试数据库。
    /// </summary>
    /// <param name="databaseName">数据库名称。</param>
    /// <returns>异步任务。</returns>
    private static async Task CleanupDatabaseAsync(string databaseName) {
        var cleanupOptions = BuildDbContextOptions(databaseName);
        await using var cleanupContext = new SortingHubDbContext(cleanupOptions);
        await cleanupContext.Database.EnsureDeletedAsync();
    }

    /// <summary>
    /// 强制设置慢查询最后命中时间。
    /// </summary>
    /// <param name="store">慢查询画像存储。</param>
    /// <param name="lastSeenAtLocal">目标时间。</param>
    private static void ForceSlowQueryLastSeenAtLocal(SlowQueryProfileStore store, DateTime lastSeenAtLocal) {
        var field = typeof(SlowQueryProfileStore).GetField("_lastSeenAtLocalByFingerprint", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var map = Assert.IsType<Dictionary<string, DateTime>>(field!.GetValue(store));
        var keys = map.Keys.ToArray();
        Assert.Single(keys);
        map[keys[0]] = lastSeenAtLocal;
    }

    /// <summary>
    /// 通过反射设置私有时间字段。
    /// </summary>
    /// <param name="instance">目标实例。</param>
    /// <param name="propertyName">属性名。</param>
    /// <param name="value">属性值。</param>
    private static void SetPrivateDateTime(object instance, string propertyName, DateTime value) {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(instance, DateTime.SpecifyKind(value, DateTimeKind.Local));
    }

    /// <summary>
    /// 创建死信包裹测试数据。
    /// </summary>
    /// <returns>包裹聚合。</returns>
    private static Parcel CreateDeadLetterParcel() {
        var scannedTime = LocalTimeTestConstraint.CreateLocalTime(2026, 5, 1, 8, 0, 0);
        return Parcel.Create(
            id: 1,
            parcelTimestamp: 20260501080000,
            type: ParcelType.Normal,
            barCodes: "DL-001",
            weight: 1.25m,
            workstationName: "WS-RETENTION",
            scannedTime: scannedTime,
            dischargeTime: scannedTime.AddMinutes(1),
            targetChuteId: 1,
            actualChuteId: 1,
            requestStatus: ApiRequestStatus.Success,
            bagCode: "BAG-RETENTION",
            isSticking: false,
            length: 10,
            width: 10,
            height: 10,
            volume: 1000,
            hasImages: false,
            hasVideos: false,
            coordinate: "0,0");
    }

    /// <summary>
    /// 通过反射设置私有可空时间字段。
    /// </summary>
    /// <param name="instance">目标实例。</param>
    /// <param name="propertyName">属性名。</param>
    /// <param name="value">属性值。</param>
    private static void SetPrivateNullableDateTime(object instance, string propertyName, DateTime value) {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property!.SetValue(instance, DateTime.SpecifyKind(value, DateTimeKind.Local));
    }
}
