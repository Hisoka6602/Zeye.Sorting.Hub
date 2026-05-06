using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Application.Services.Idempotency;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Idempotency;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 幂等能力测试。
/// </summary>
public sealed class IdempotencyTests {
    /// <summary>
    /// 管理端新增包裹幂等来源系统。
    /// </summary>
    private const string ParcelCreateIdempotencySourceSystem = "Host.ParcelAdminApi";

    /// <summary>
    /// 管理端新增包裹幂等操作名称。
    /// </summary>
    private const string ParcelCreateIdempotencyOperationName = "ParcelCreate";

    /// <summary>
    /// 相同载荷应生成稳定的 SHA256 哈希。
    /// </summary>
    [Fact]
    public void IdempotencyKeyHasher_WhenPayloadIsSame_ShouldReturnStableHash() {
        var hasher = new IdempotencyKeyHasher();
        var payload = new {
            SourceSystem = ParcelCreateIdempotencySourceSystem,
            OperationName = ParcelCreateIdempotencyOperationName,
            BusinessKey = "1001",
            Payload = "demo"
        };

        var firstHash = hasher.ComputeHash(payload);
        var secondHash = hasher.ComputeHash(payload);

        Assert.Equal(64, firstHash.Length);
        Assert.Equal(firstHash, secondHash);
    }

    /// <summary>
    /// 相同幂等请求重复提交时应返回已有结果而不是再次写入。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task CreateParcelCommandService_WhenSameRequestRepeated_ShouldReplayExistingResponse() {
        var databaseName = $"idempotency-replay-{Guid.NewGuid():N}";
        try {
            var options = BuildOptions(databaseName);
            var factory = new SortingHubTestDbContextFactory(options);
            var service = CreateCommandService(factory);
            var request = CreateRequest(31001);
            var scannedTime = DateTime.Now.AddMinutes(-5);
            var dischargeTime = scannedTime.AddMinutes(1);
            var hasher = new IdempotencyKeyHasher();
            var payloadHash = BuildParcelCreatePayloadHash(hasher, request, scannedTime, dischargeTime);

            var firstResult = await service.ExecuteAsync(
                request,
                scannedTime,
                dischargeTime,
                ParcelCreateIdempotencySourceSystem,
                ParcelCreateIdempotencyOperationName,
                payloadHash,
                CancellationToken.None);
            var secondResult = await service.ExecuteAsync(
                request,
                scannedTime,
                dischargeTime,
                ParcelCreateIdempotencySourceSystem,
                ParcelCreateIdempotencyOperationName,
                payloadHash,
                CancellationToken.None);

            Assert.False(firstResult.IsReplay);
            Assert.True(secondResult.IsReplay);
            Assert.Equal(request.Id, firstResult.Response.Id);
            Assert.Equal(request.Id, secondResult.Response.Id);

            await using var dbContext = new SortingHubDbContext(options);
            Assert.Equal(1, await dbContext.Set<Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Parcel>().CountAsync());
            var record = await dbContext.Set<IdempotencyRecord>().SingleAsync();
            Assert.Equal(request.Id.ToString(), record.BusinessKey);
            Assert.Equal(Zeye.Sorting.Hub.Domain.Enums.Idempotency.IdempotencyRecordStatus.Completed, record.Status);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 相同幂等键仍处于处理中时应明确拒绝重复请求。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task CreateParcelCommandService_WhenIdempotencyRecordIsPending_ShouldReject() {
        var databaseName = $"idempotency-pending-{Guid.NewGuid():N}";
        try {
            var options = BuildOptions(databaseName);
            var factory = new SortingHubTestDbContextFactory(options);
            var request = CreateRequest(32001);
            var scannedTime = DateTime.Now.AddMinutes(-10);
            var dischargeTime = scannedTime.AddMinutes(1);
            var hasher = new IdempotencyKeyHasher();
            var payloadHash = BuildParcelCreatePayloadHash(hasher, request, scannedTime, dischargeTime);

            await using (var dbContext = new SortingHubDbContext(options)) {
                await dbContext.Set<IdempotencyRecord>().AddAsync(IdempotencyRecord.CreatePending(
                    ParcelCreateIdempotencySourceSystem,
                    ParcelCreateIdempotencyOperationName,
                    request.Id.ToString(),
                    payloadHash));
                await dbContext.SaveChangesAsync();
            }

            var service = CreateCommandService(factory);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(
                request,
                scannedTime,
                dischargeTime,
                ParcelCreateIdempotencySourceSystem,
                ParcelCreateIdempotencyOperationName,
                payloadHash,
                CancellationToken.None));

            Assert.Equal(IdempotencyGuardService.RequestInProgressMessage, exception.Message);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 首次请求取消后应保留可重试语义，并允许后续相同请求成功重试。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task CreateParcelCommandService_WhenFirstRequestIsCanceled_ShouldAllowLaterRetry() {
        var databaseName = $"idempotency-cancel-{Guid.NewGuid():N}";
        try {
            var options = BuildOptions(databaseName);
            var factory = new SortingHubTestDbContextFactory(options);
            var idempotencyRepository = new IdempotencyRepository(factory);
            var guardService = new IdempotencyGuardService(idempotencyRepository);
            var request = CreateRequest(33001);
            var scannedTime = DateTime.Now.AddMinutes(-8);
            var dischargeTime = scannedTime.AddMinutes(1);
            var hasher = new IdempotencyKeyHasher();
            var payloadHash = BuildParcelCreatePayloadHash(hasher, request, scannedTime, dischargeTime);
            using var cancellationTokenSource = new CancellationTokenSource();
            await Assert.ThrowsAsync<OperationCanceledException>(() => guardService.ExecuteAsync(
                ParcelCreateIdempotencySourceSystem,
                ParcelCreateIdempotencyOperationName,
                request.Id.ToString(),
                payloadHash,
                innerCancellationToken => {
                    cancellationTokenSource.Cancel();
                    throw new OperationCanceledException(innerCancellationToken);
                },
                static _ => Task.FromResult<string?>(null),
                cancellationTokenSource.Token));

            await using (var verificationContext = new SortingHubDbContext(options)) {
                var canceledRecord = await verificationContext.Set<IdempotencyRecord>().SingleAsync();
                Assert.Equal(Zeye.Sorting.Hub.Domain.Enums.Idempotency.IdempotencyRecordStatus.Rejected, canceledRecord.Status);
                Assert.Equal(IdempotencyGuardService.RequestCanceledMessage, canceledRecord.FailureMessage);
            }

            var retryResult = await guardService.ExecuteAsync(
                ParcelCreateIdempotencySourceSystem,
                ParcelCreateIdempotencyOperationName,
                request.Id.ToString(),
                payloadHash,
                static _ => Task.FromResult("retry-success"),
                static _ => Task.FromResult<string?>(null),
                CancellationToken.None);

            Assert.False(retryResult.IsReplay);
            Assert.Equal("retry-success", retryResult.Response);

            await using var finalContext = new SortingHubDbContext(options);
            var finalRecord = await finalContext.Set<IdempotencyRecord>().SingleAsync();
            Assert.Equal(Zeye.Sorting.Hub.Domain.Enums.Idempotency.IdempotencyRecordStatus.Completed, finalRecord.Status);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// Pending 记录已存在真实结果时应按重放语义返回，避免长期卡死在处理中。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task CreateParcelCommandService_WhenPendingRecordAlreadyHasResult_ShouldReplayExistingResponse() {
        var databaseName = $"idempotency-recover-pending-{Guid.NewGuid():N}";
        try {
            var options = BuildOptions(databaseName);
            var factory = new SortingHubTestDbContextFactory(options);
            var service = CreateCommandService(factory);
            var request = CreateRequest(34001);
            var scannedTime = DateTime.Now.AddMinutes(-6);
            var dischargeTime = scannedTime.AddMinutes(1);
            var hasher = new IdempotencyKeyHasher();
            var payloadHash = BuildParcelCreatePayloadHash(hasher, request, scannedTime, dischargeTime);
            var parcel = Zeye.Sorting.Hub.Application.Mappers.Parcels.ParcelCreateRequestMapper.MapToParcel(request, scannedTime, dischargeTime);

            await using (var dbContext = new SortingHubDbContext(options)) {
                await dbContext.Set<Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Parcel>().AddAsync(parcel);
                await dbContext.Set<IdempotencyRecord>().AddAsync(IdempotencyRecord.CreatePending(
                    ParcelCreateIdempotencySourceSystem,
                    ParcelCreateIdempotencyOperationName,
                    request.Id.ToString(),
                    payloadHash));
                await dbContext.SaveChangesAsync();
            }

            var replayResult = await service.ExecuteAsync(
                request,
                scannedTime,
                dischargeTime,
                ParcelCreateIdempotencySourceSystem,
                ParcelCreateIdempotencyOperationName,
                payloadHash,
                CancellationToken.None);

            Assert.True(replayResult.IsReplay);
            Assert.Equal(request.Id, replayResult.Response.Id);

            await using var verificationContext = new SortingHubDbContext(options);
            var repairedRecord = await verificationContext.Set<IdempotencyRecord>().SingleAsync();
            Assert.Equal(Zeye.Sorting.Hub.Domain.Enums.Idempotency.IdempotencyRecordStatus.Completed, repairedRecord.Status);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 创建命令服务。
    /// </summary>
    /// <param name="factory">测试数据库工厂。</param>
    /// <returns>命令服务实例。</returns>
    private static CreateParcelCommandService CreateCommandService(IDbContextFactory<SortingHubDbContext> factory) {
        var parcelRepository = new ParcelRepository(factory);
        var idempotencyRepository = new IdempotencyRepository(factory);
        var idempotencyGuardService = new IdempotencyGuardService(idempotencyRepository);
        return new CreateParcelCommandService(parcelRepository, idempotencyGuardService);
    }

    /// <summary>
    /// 构建 InMemory DbContext 选项。
    /// </summary>
    /// <param name="databaseName">数据库名。</param>
    /// <returns>上下文选项。</returns>
    private static DbContextOptions<SortingHubDbContext> BuildOptions(string databaseName) {
        return new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    /// <summary>
    /// 删除测试数据库。
    /// </summary>
    /// <param name="databaseName">数据库名。</param>
    /// <returns>异步任务。</returns>
    private static async Task CleanupDatabaseAsync(string databaseName) {
        var options = BuildOptions(databaseName);
        await using var dbContext = new SortingHubDbContext(options);
        await dbContext.Database.EnsureDeletedAsync();
    }

    /// <summary>
    /// 创建测试请求。
    /// </summary>
    /// <param name="id">包裹 Id。</param>
    /// <returns>测试请求。</returns>
    private static ParcelCreateRequest CreateRequest(long id) {
        return new ParcelCreateRequest {
            Id = id,
            ParcelTimestamp = DateTime.Now.Ticks,
            Type = 1,
            BarCodes = $"BC-{id}",
            Weight = 2.5m,
            WorkstationName = "WS-IDEMPOTENCY",
            ScannedTime = "2026-05-06 08:00:00",
            DischargeTime = "2026-05-06 08:01:00",
            TargetChuteId = 1001,
            ActualChuteId = 1002,
            RequestStatus = 1,
            BagCode = $"BAG-{id}",
            IsSticking = false,
            Length = 10,
            Width = 8,
            Height = 6,
            Volume = 480,
            HasImages = false,
            HasVideos = false,
            Coordinate = "10,20",
            NoReadType = 0,
            SorterCarrierId = 1,
            SegmentCodes = "SEG-A",
            LifecycleMilliseconds = 1000
        };
    }

    /// <summary>
    /// 构建管理端新增包裹规范化载荷哈希。
    /// </summary>
    /// <param name="hasher">哈希计算器。</param>
    /// <param name="request">请求合同。</param>
    /// <param name="scannedTime">扫码时间。</param>
    /// <param name="dischargeTime">落格时间。</param>
    /// <returns>载荷哈希。</returns>
    private static string BuildParcelCreatePayloadHash(
        IdempotencyKeyHasher hasher,
        ParcelCreateRequest request,
        DateTime scannedTime,
        DateTime dischargeTime) {
        return hasher.ComputeHash(new {
            request.Id,
            request.ParcelTimestamp,
            request.Type,
            request.BarCodes,
            request.Weight,
            request.WorkstationName,
            ScannedTime = scannedTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff"),
            DischargeTime = dischargeTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff"),
            request.TargetChuteId,
            request.ActualChuteId,
            request.RequestStatus,
            request.BagCode,
            request.IsSticking,
            request.Length,
            request.Width,
            request.Height,
            request.Volume,
            request.HasImages,
            request.HasVideos,
            request.Coordinate,
            request.NoReadType,
            request.SorterCarrierId,
            request.SegmentCodes,
            request.LifecycleMilliseconds
        });
    }
}
