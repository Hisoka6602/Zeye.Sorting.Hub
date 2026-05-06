using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Application.Services.Idempotency;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Application.Services.WriteBuffers;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Idempotency;
using Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Parcel 批量缓冲写入回归测试。
/// </summary>
public sealed class ParcelBufferedWriteTests {
    /// <summary>
    /// 验证场景：批量缓冲写入服务在低水位时成功入队。
    /// </summary>
    [Fact]
    public async Task ParcelBufferedWriteService_WhenQueueBelowThreshold_ShouldAcceptAll() {
        var options = CreateBufferedWriteOptions();
        var writeChannel = new BoundedWriteChannel<BufferedParcelWriteItem>(options.ChannelCapacity);
        var service = new ParcelBufferedWriteService(writeChannel, Microsoft.Extensions.Options.Options.Create(options));
        var parcels = new[] {
            CreateParcel(1001, LocalTimeTestConstraint.CreateLocalTime(2026, 4, 1, 10, 0, 0)),
            CreateParcel(1002, LocalTimeTestConstraint.CreateLocalTime(2026, 4, 1, 10, 0, 1))
        };

        var result = await service.EnqueueAsync(parcels, CancellationToken.None);

        Assert.Equal(2, result.AcceptedCount);
        Assert.Equal(0, result.RejectedCount);
        Assert.False(result.IsBackpressureTriggered);
        Assert.Equal(2, result.QueueDepth);
    }

    /// <summary>
    /// 验证场景：队列达到背压阈值后拒绝剩余请求。
    /// </summary>
    [Fact]
    public async Task ParcelBufferedWriteService_WhenQueueReachesThreshold_ShouldRejectRemaining() {
        var options = CreateBufferedWriteOptions(channelCapacity: 4, backpressureRejectThreshold: 2);
        var writeChannel = new BoundedWriteChannel<BufferedParcelWriteItem>(options.ChannelCapacity);
        var service = new ParcelBufferedWriteService(writeChannel, Microsoft.Extensions.Options.Options.Create(options));
        await service.EnqueueAsync([
            CreateParcel(1101, LocalTimeTestConstraint.CreateLocalTime(2026, 4, 1, 10, 0, 0)),
            CreateParcel(1102, LocalTimeTestConstraint.CreateLocalTime(2026, 4, 1, 10, 0, 1))
        ], CancellationToken.None);

        var result = await service.EnqueueAsync([
            CreateParcel(1103, LocalTimeTestConstraint.CreateLocalTime(2026, 4, 1, 10, 0, 2)),
            CreateParcel(1104, LocalTimeTestConstraint.CreateLocalTime(2026, 4, 1, 10, 0, 3))
        ], CancellationToken.None);

        Assert.Equal(0, result.AcceptedCount);
        Assert.Equal(2, result.RejectedCount);
        Assert.True(result.IsBackpressureTriggered);
        Assert.Equal(2, result.QueueDepth);
    }

    /// <summary>
    /// 验证场景：后台刷新按单批调用一次批量新增。
    /// </summary>
    [Fact]
    public async Task ParcelBatchWriteFlushService_FlushOnce_ShouldPersistBatchWithSingleCall() {
        var options = CreateBufferedWriteOptions(batchSize: 3);
        var writeChannel = new BoundedWriteChannel<BufferedParcelWriteItem>(options.ChannelCapacity);
        var deadLetterStore = new DeadLetterWriteStore(options.DeadLetterCapacity);
        var fakeRepository = new FakeParcelRepository();
        var serviceProvider = BuildRepositoryServiceProvider(fakeRepository);
        var flushService = new ParcelBatchWriteFlushService(
            writeChannel,
            deadLetterStore,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(options));
        for (var index = 0; index < 3; index++) {
            writeChannel.TryEnqueue(new BufferedParcelWriteItem(
                Parcel: CreateParcel(1200 + index, LocalTimeTestConstraint.CreateLocalTime(2026, 4, 2, 9, 0, index)),
                EnqueuedAt: LocalTimeTestConstraint.CreateLocalTime(2026, 4, 2, 9, 1, index),
                RetryCount: 0,
                LastErrorMessage: null,
                LastRetryAtLocal: null));
        }

        var hasFlushed = await flushService.FlushOnceAsync(CancellationToken.None);

        Assert.True(hasFlushed);
        Assert.Equal(1, fakeRepository.AddRangeCallCount);
        Assert.Equal(3, fakeRepository.LastAddRangeCount);
        Assert.Equal(3, fakeRepository.GetStoredParcelCount());
        var metricsSnapshot = flushService.GetMetricsSnapshot();
        Assert.Equal(3, metricsSnapshot.TotalFlushedCount);
        Assert.Equal(0, metricsSnapshot.DeadLetterCount);
        Assert.NotNull(metricsSnapshot.LastSuccessfulFlushAtLocal);
        LocalTimeTestConstraint.AssertIsLocalTime(metricsSnapshot.LastSuccessfulFlushAtLocal!.Value);
    }

    /// <summary>
    /// 验证场景：重试次数耗尽后写入死信。
    /// </summary>
    [Fact]
    public async Task ParcelBatchWriteFlushService_WhenRetryExhausted_ShouldMoveToDeadLetter() {
        var options = CreateBufferedWriteOptions(maxRetryCount: 1, deadLetterCapacity: 10);
        var writeChannel = new BoundedWriteChannel<BufferedParcelWriteItem>(options.ChannelCapacity);
        var deadLetterStore = new DeadLetterWriteStore(options.DeadLetterCapacity);
        var fakeRepository = new FakeParcelRepository { ShouldFailOnAddRange = true };
        var serviceProvider = BuildRepositoryServiceProvider(fakeRepository);
        var flushService = new ParcelBatchWriteFlushService(
            writeChannel,
            deadLetterStore,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(options));
        writeChannel.TryEnqueue(new BufferedParcelWriteItem(
            Parcel: CreateParcel(1301, LocalTimeTestConstraint.CreateLocalTime(2026, 4, 3, 9, 0, 0)),
            EnqueuedAt: LocalTimeTestConstraint.CreateLocalTime(2026, 4, 3, 9, 0, 1),
            RetryCount: 0,
            LastErrorMessage: null,
            LastRetryAtLocal: null));

        await flushService.FlushOnceAsync(CancellationToken.None);
        await flushService.FlushOnceAsync(CancellationToken.None);

        Assert.Equal(2, fakeRepository.AddRangeCallCount);
        Assert.Equal(1, deadLetterStore.Count);
        var deadLetter = Assert.Single(deadLetterStore.GetSnapshot());
        Assert.Equal(1301, deadLetter.Parcel.Id);
        Assert.Contains("模拟批量写入失败", deadLetter.ErrorMessage, StringComparison.Ordinal);
        LocalTimeTestConstraint.AssertIsLocalTime(deadLetter.FailedAtLocal);
        var metricsSnapshot = flushService.GetMetricsSnapshot();
        Assert.Equal(1, metricsSnapshot.DeadLetterCount);
        Assert.NotNull(metricsSnapshot.LastFailedFlushAtLocal);
    }

    /// <summary>
    /// 验证场景：批量落库返回失败且取消信号已触发时，已出队批次进入死信避免丢失。
    /// </summary>
    [Fact]
    public async Task ParcelBatchWriteFlushService_WhenRepositoryFailsAndTokenCanceled_ShouldMoveDequeuedBatchToDeadLetter() {
        using var cancellationTokenSource = new CancellationTokenSource();
        var options = CreateBufferedWriteOptions(maxRetryCount: 3, deadLetterCapacity: 10);
        var writeChannel = new BoundedWriteChannel<BufferedParcelWriteItem>(options.ChannelCapacity);
        var deadLetterStore = new DeadLetterWriteStore(options.DeadLetterCapacity);
        var fakeRepository = new FakeParcelRepository {
            ShouldFailOnAddRange = true,
            BeforeAddRangeResult = () => cancellationTokenSource.Cancel()
        };
        var serviceProvider = BuildRepositoryServiceProvider(fakeRepository);
        var flushService = new ParcelBatchWriteFlushService(
            writeChannel,
            deadLetterStore,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(options));
        writeChannel.TryEnqueue(new BufferedParcelWriteItem(
            Parcel: CreateParcel(1351, LocalTimeTestConstraint.CreateLocalTime(2026, 4, 3, 10, 0, 0)),
            EnqueuedAt: LocalTimeTestConstraint.CreateLocalTime(2026, 4, 3, 10, 0, 1),
            RetryCount: 0,
            LastErrorMessage: null,
            LastRetryAtLocal: null));

        var exception = await Record.ExceptionAsync(() => flushService.FlushOnceAsync(cancellationTokenSource.Token));

        Assert.IsType<OperationCanceledException>(exception);
        Assert.Equal(0, writeChannel.Depth);
        Assert.Equal(1, deadLetterStore.Count);
        var deadLetter = Assert.Single(deadLetterStore.GetSnapshot());
        Assert.Equal(1351, deadLetter.Parcel.Id);
        Assert.Contains("取消期间进入死信隔离", deadLetter.ErrorMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：有界写入通道达到容量后拒绝写入且深度不超过容量。
    /// </summary>
    [Fact]
    public void BoundedWriteChannel_WhenCapacityReached_ShouldRejectAndKeepDepthBounded() {
        var writeChannel = new BoundedWriteChannel<int>(capacity: 1);

        var firstAccepted = writeChannel.TryEnqueue(1);
        var secondAccepted = writeChannel.TryEnqueue(2);

        Assert.True(firstAccepted);
        Assert.False(secondAccepted);
        Assert.Equal(1, writeChannel.Depth);
        Assert.Equal(1, writeChannel.DroppedCount);
        Assert.True(writeChannel.TryDequeue(out var item));
        Assert.Equal(1, item);
        Assert.Equal(0, writeChannel.Depth);
    }

    /// <summary>
    /// 验证场景：死信存在时健康检查返回 Degraded。
    /// </summary>
    [Fact]
    public async Task BufferedWriteQueueHealthCheck_WhenDeadLetterExists_ShouldReturnDegraded() {
        var options = CreateBufferedWriteOptions(maxRetryCount: 0, deadLetterCapacity: 10);
        var writeChannel = new BoundedWriteChannel<BufferedParcelWriteItem>(options.ChannelCapacity);
        var deadLetterStore = new DeadLetterWriteStore(options.DeadLetterCapacity);
        var fakeRepository = new FakeParcelRepository { ShouldFailOnAddRange = true };
        var serviceProvider = BuildRepositoryServiceProvider(fakeRepository);
        var flushService = new ParcelBatchWriteFlushService(
            writeChannel,
            deadLetterStore,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(options));
        var healthCheck = new BufferedWriteQueueHealthCheck(flushService, Microsoft.Extensions.Options.Options.Create(options));
        writeChannel.TryEnqueue(new BufferedParcelWriteItem(
            Parcel: CreateParcel(1401, LocalTimeTestConstraint.CreateLocalTime(2026, 4, 4, 9, 0, 0)),
            EnqueuedAt: LocalTimeTestConstraint.CreateLocalTime(2026, 4, 4, 9, 0, 1),
            RetryCount: 0,
            LastErrorMessage: null,
            LastRetryAtLocal: null));

        await flushService.FlushOnceAsync(CancellationToken.None);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(1, Assert.IsType<int>(result.Data["deadLetterCount"]));
    }

    /// <summary>
    /// 验证场景：批量缓冲写入接口返回 accepted/rejected/queueDepth 等字段。
    /// </summary>
    [Fact]
    public async Task CreateBufferedParcelBatchApi_ShouldReturnAcceptedResult() {
        await using var app = await BuildBatchBufferTestAppAsync(CreateBufferedWriteOptions());
        using var client = app.GetTestClient();
        using var response = await client.PostAsync("/api/admin/parcels/batch-buffer", BuildBatchCreateRequestJson(2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ParcelBatchBufferedCreateResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.AcceptedCount);
        Assert.Equal(0, payload.RejectedCount);
        Assert.False(payload.IsBackpressureTriggered);
        Assert.Equal(2, payload.QueueDepth);
    }

    /// <summary>
    /// 验证场景：批量缓冲写入接口拒绝 UTC 时间字符串。
    /// </summary>
    [Fact]
    public async Task CreateBufferedParcelBatchApi_WithUtcTime_ShouldReturnBadRequest() {
        await using var app = await BuildBatchBufferTestAppAsync(CreateBufferedWriteOptions());
        using var client = app.GetTestClient();
        using var response = await client.PostAsync(
            "/api/admin/parcels/batch-buffer",
            BuildBatchCreateRequestJson(1, scannedTime: "2026-03-20T10:00:00Z"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("本地时间格式", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// 构建缓冲写入配置。
    /// </summary>
    /// <param name="channelCapacity">通道容量。</param>
    /// <param name="batchSize">批次大小。</param>
    /// <param name="flushIntervalMilliseconds">刷新间隔。</param>
    /// <param name="maxRetryCount">最大重试次数。</param>
    /// <param name="backpressureRejectThreshold">背压阈值。</param>
    /// <param name="deadLetterCapacity">死信容量。</param>
    /// <returns>缓冲写入配置。</returns>
    private static BufferedWriteOptions CreateBufferedWriteOptions(
        int channelCapacity = 100,
        int batchSize = 10,
        int flushIntervalMilliseconds = 10,
        int maxRetryCount = 3,
        int backpressureRejectThreshold = 90,
        int deadLetterCapacity = 100) {
        return new BufferedWriteOptions {
            IsEnabled = true,
            ChannelCapacity = channelCapacity,
            BatchSize = batchSize,
            FlushIntervalMilliseconds = flushIntervalMilliseconds,
            MaxRetryCount = maxRetryCount,
            BackpressureRejectThreshold = backpressureRejectThreshold,
            DeadLetterCapacity = deadLetterCapacity
        };
    }

    /// <summary>
    /// 构建仓储作用域服务提供器。
    /// </summary>
    /// <param name="repository">测试仓储。</param>
    /// <returns>服务提供器。</returns>
    private static ServiceProvider BuildRepositoryServiceProvider(IParcelRepository repository) {
        var services = new ServiceCollection();
        services.AddScoped<IParcelRepository>(_ => repository);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 构建批量缓冲写入测试应用。
    /// </summary>
    /// <param name="options">缓冲写入配置。</param>
    /// <returns>测试应用。</returns>
    private static async Task<WebApplication> BuildBatchBufferTestAppAsync(BufferedWriteOptions options) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        var databaseOptions = new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase($"parcel-buffer-idempotency-{Guid.NewGuid():N}")
            .Options;
        builder.Services.AddScoped<IParcelRepository>(_ => new FakeParcelRepository());
        builder.Services.AddSingleton<IDbContextFactory<SortingHubDbContext>>(new SortingHubTestDbContextFactory(databaseOptions));
        builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        builder.Services.AddScoped<IdempotencyGuardService>();
        builder.Services.AddSingleton<IdempotencyKeyHasher>();
        builder.Services.AddScoped<CreateParcelCommandService>();
        builder.Services.AddScoped<UpdateParcelStatusCommandService>();
        builder.Services.AddScoped<DeleteParcelCommandService>();
        builder.Services.AddScoped<CleanupExpiredParcelsCommandService>();
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(options));
        builder.Services.AddSingleton(new BoundedWriteChannel<BufferedParcelWriteItem>(options.ChannelCapacity));
        builder.Services.AddSingleton(new DeadLetterWriteStore(options.DeadLetterCapacity));
        builder.Services.AddSingleton<ParcelBufferedWriteService>();
        builder.Services.AddSingleton<IBufferedWriteService>(sp => sp.GetRequiredService<ParcelBufferedWriteService>());
        var app = builder.Build();
        app.MapParcelAdminApis();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 创建测试包裹。
    /// </summary>
    /// <param name="id">包裹 Id。</param>
    /// <param name="scannedTime">扫码时间。</param>
    /// <returns>包裹聚合。</returns>
    private static Parcel CreateParcel(long id, DateTime scannedTime) {
        return Parcel.Create(
            id: id,
            parcelTimestamp: Math.Abs(scannedTime.Ticks),
            type: ParcelType.Normal,
            barCodes: $"BC-{id}",
            weight: 1.2m,
            workstationName: "WS-BUFFER",
            scannedTime: scannedTime,
            dischargeTime: scannedTime.AddSeconds(2),
            targetChuteId: 201,
            actualChuteId: 202,
            requestStatus: ApiRequestStatus.Success,
            bagCode: "BAG-BUFFER",
            isSticking: false,
            length: 10m,
            width: 11m,
            height: 12m,
            volume: 1320m,
            hasImages: false,
            hasVideos: false,
            coordinate: "x:2,y:3");
    }

    /// <summary>
    /// 构建批量缓冲写入请求体。
    /// </summary>
    /// <param name="count">记录条数。</param>
    /// <param name="scannedTime">扫码时间字符串。</param>
    /// <returns>请求体。</returns>
    private static StringContent BuildBatchCreateRequestJson(int count, string scannedTime = "2026-03-20T10:00:00") {
        var parcels = Enumerable.Range(0, count)
            .Select(index => $$"""
                {
                  "id": {{9000 + index}},
                  "parcelTimestamp": 638789040000000000,
                  "type": 0,
                  "barCodes": "BC-BATCH-{{index}}",
                  "weight": 1.5,
                  "workstationName": "WS-ADMIN",
                  "scannedTime": "{{scannedTime}}",
                  "dischargeTime": "2026-03-20T10:00:03",
                  "targetChuteId": 101,
                  "actualChuteId": 102,
                  "requestStatus": 1,
                  "bagCode": "BAG-ADMIN",
                  "isSticking": false,
                  "length": 20,
                  "width": 15,
                  "height": 10,
                  "volume": 3000,
                  "hasImages": false,
                  "hasVideos": false,
                  "coordinate": "x:5,y:3"
                }
                """)
            .ToArray();
        var json = $$"""
            {
              "parcels": [{{string.Join(',', parcels)}}]
            }
            """;
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
