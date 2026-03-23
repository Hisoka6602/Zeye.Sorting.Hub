using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Parcel 查询应用服务回归测试。
/// </summary>
public sealed class ParcelQueryServicesTests {
    /// <summary>
    /// 测试包裹 Id 自增序列。
    /// </summary>
    private static long _testParcelIdSequence = 1000;
    /// <summary>
    /// 验证场景：GetParcelPagedQueryService_ShouldMapAndValidate。
    /// </summary>
    [Fact]
    public async Task GetParcelPagedQueryService_ShouldMapAndValidate() {
        var databaseName = $"parcel-query-service-test-{Guid.NewGuid():N}";
        var baseTime = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Local);
        try {
            var repository = CreateRepository(databaseName);
            await SeedParcelsAsync(databaseName, [
                CreateParcel("BC-QS-1", "BAG-QS", "WS-QS", ParcelStatus.Pending, baseTime.AddMinutes(-5), 910, 911),
                CreateParcel("BC-QS-2", "BAG-QS", "WS-QS", ParcelStatus.Completed, baseTime.AddMinutes(-3), 910, 912),
                CreateParcel("BC-QS-3", "BAG-OTHER", "WS-OTHER", ParcelStatus.Pending, baseTime.AddMinutes(-1), 920, 921)
            ]);

            var service = new GetParcelPagedQueryService(repository);
            var response = await service.ExecuteAsync(
                new ParcelListRequest {
                    PageNumber = 1,
                    PageSize = 10,
                    BagCode = "BAG-QS",
                    ScannedTimeStart = baseTime.AddHours(-1),
                    ScannedTimeEnd = baseTime
                },
                CancellationToken.None);

            Assert.Equal(2, response.TotalCount);
            Assert.Equal(2, response.Items.Count);
            Assert.All(response.Items, item => Assert.Equal("BAG-QS", item.BagCode));

            await Assert.ThrowsAsync<ArgumentException>(() => service.ExecuteAsync(
                new ParcelListRequest {
                    PageNumber = 1,
                    PageSize = 10,
                    ScannedTimeStart = baseTime,
                    ScannedTimeEnd = baseTime.AddMinutes(-1)
                },
                CancellationToken.None));
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：GetParcelByIdQueryService_ShouldReturnDetailOrNull。
    /// </summary>
    [Fact]
    public async Task GetParcelByIdQueryService_ShouldReturnDetailOrNull() {
        var databaseName = $"parcel-query-service-test-{Guid.NewGuid():N}";
        var baseTime = new DateTime(2026, 3, 20, 11, 0, 0, DateTimeKind.Local);
        try {
            var parcel = CreateParcel("BC-DETAIL-1", "BAG-DETAIL", "WS-DETAIL", ParcelStatus.Pending, baseTime.AddMinutes(-2), 930, 931);
            await SeedParcelsAsync(databaseName, [parcel]);
            var repository = CreateRepository(databaseName);
            var service = new GetParcelByIdQueryService(repository);

            var detail = await service.ExecuteAsync(parcel.Id, CancellationToken.None);
            Assert.NotNull(detail);
            Assert.Equal(parcel.Id, detail.Id);
            Assert.Equal("BC-DETAIL-1", detail.BarCodes);

            var none = await service.ExecuteAsync(long.MaxValue, CancellationToken.None);
            Assert.Null(none);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：GetAdjacentParcelsQueryService_ShouldMapAndNormalize。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcelsQueryService_ShouldMapAndNormalize() {
        var databaseName = $"parcel-query-service-test-{Guid.NewGuid():N}";
        var baseTime = new DateTime(2026, 3, 20, 9, 0, 0, DateTimeKind.Local);
        try {
            var parcel1 = CreateParcel("BC-ADJ-1", "BAG-ADJ", "WS-ADJ", ParcelStatus.Pending, baseTime.AddMinutes(1), 940, 941);
            var parcel2 = CreateParcel("BC-ADJ-2", "BAG-ADJ", "WS-ADJ", ParcelStatus.Pending, baseTime.AddMinutes(2), 940, 942);
            var parcel3 = CreateParcel("BC-ADJ-3", "BAG-ADJ", "WS-ADJ", ParcelStatus.Pending, baseTime.AddMinutes(3), 940, 943);
            await SeedParcelsAsync(databaseName, [parcel1, parcel2, parcel3]);
            var repository = CreateRepository(databaseName);
            var service = new GetAdjacentParcelsQueryService(repository);

            var response = await service.ExecuteAsync(
                new ParcelAdjacentRequest {
                    Id = parcel2.Id,
                    BeforeCount = 200,
                    AfterCount = 200
                },
                CancellationToken.None);
            Assert.Equal(200, response.BeforeCount);
            Assert.Equal(200, response.AfterCount);
            Assert.Equal(2, response.Items.Count);
            Assert.Equal("BC-ADJ-1", response.Items[0].BarCodes);
            Assert.Equal("BC-ADJ-3", response.Items[1].BarCodes);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync(
                new ParcelAdjacentRequest {
                    Id = parcel2.Id,
                    BeforeCount = -1,
                    AfterCount = 1
                },
                CancellationToken.None));
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：邻近查询锚点不存在时抛出 KeyNotFoundException。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcelsQueryService_WhenAnchorNotFound_ShouldThrowKeyNotFoundException() {
        var databaseName = $"parcel-query-service-test-{Guid.NewGuid():N}";
        try {
            var repository = CreateRepository(databaseName);
            var service = new GetAdjacentParcelsQueryService(repository);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ExecuteAsync(
                new ParcelAdjacentRequest {
                    Id = 999999,
                    BeforeCount = 1,
                    AfterCount = 1
                },
                CancellationToken.None));
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：多重过滤条件联合使用时，GetParcelPagedQueryService 仅返回同时满足所有条件的记录。
    /// 覆盖 bagCode、workstationName、actualChuteId、status 多参数组合路径。
    /// </summary>
    [Fact]
    public async Task GetParcelPagedQueryService_WithMultipleFilters_ShouldReturnOnlyMatchingParcels() {
        var databaseName = $"parcel-multifilter-test-{Guid.NewGuid():N}";
        var baseTime = LocalTimeTestConstraintHelper.CreateLocalTime(2026, 3, 20, 12, 0, 0);
        try {
            await SeedParcelsAsync(databaseName, [
                // 完全匹配：BagCode + WorkstationName + ActualChuteId + Status 全部一致
                CreateParcel("BC-MULTI-1", "BAG-MULTI", "WS-M1", ParcelStatus.Completed, baseTime.AddMinutes(-10), 900, 901),
                // ActualChuteId 不匹配
                CreateParcel("BC-MULTI-2", "BAG-MULTI", "WS-M1", ParcelStatus.Completed, baseTime.AddMinutes(-8), 900, 999),
                // WorkstationName 不匹配
                CreateParcel("BC-MULTI-3", "BAG-MULTI", "WS-OTHER", ParcelStatus.Completed, baseTime.AddMinutes(-6), 900, 901),
                // Status 不匹配（仍为 Pending）
                CreateParcel("BC-MULTI-4", "BAG-MULTI", "WS-M1", ParcelStatus.Pending, baseTime.AddMinutes(-4), 900, 901),
                // BagCode 不匹配
                CreateParcel("BC-MULTI-5", "BAG-OTHER", "WS-M1", ParcelStatus.Completed, baseTime.AddMinutes(-2), 900, 901)
            ]);

            var service = new GetParcelPagedQueryService(CreateRepository(databaseName));
            var response = await service.ExecuteAsync(
                new ParcelListRequest {
                    PageNumber = 1,
                    PageSize = 10,
                    BagCode = "BAG-MULTI",
                    WorkstationName = "WS-M1",
                    ActualChuteId = 901L,
                    Status = (int)ParcelStatus.Completed,
                    ScannedTimeStart = baseTime.AddHours(-1),
                    ScannedTimeEnd = baseTime
                },
                CancellationToken.None);

            // 步骤 1：只有 BC-MULTI-1 满足全部四个过滤条件。
            Assert.Equal(1, response.TotalCount);
            Assert.Single(response.Items);
            Assert.Equal("BC-MULTI-1", response.Items[0].BarCodes);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：ExceptionType 过滤条件可以单独筛选出对应异常类型的包裹。
    /// </summary>
    [Fact]
    public async Task GetParcelPagedQueryService_WithExceptionTypeFilter_ShouldReturnOnlyMatchingParcels() {
        var databaseName = $"parcel-exceptiontype-test-{Guid.NewGuid():N}";
        var baseTime = LocalTimeTestConstraintHelper.CreateLocalTime(2026, 3, 20, 13, 0, 0);
        try {
            var parcelLost = CreateParcelWithException("BC-EX-1", "BAG-EX", "WS-EX", ParcelExceptionType.ParcelLost, baseTime.AddMinutes(-10), 910, 911);
            var mechanical = CreateParcelWithException("BC-EX-2", "BAG-EX", "WS-EX", ParcelExceptionType.MechanicalFailure, baseTime.AddMinutes(-8), 910, 911);
            var pending = CreateParcel("BC-EX-3", "BAG-EX", "WS-EX", ParcelStatus.Pending, baseTime.AddMinutes(-6), 910, 912);
            await SeedParcelsAsync(databaseName, [parcelLost, mechanical, pending]);

            var service = new GetParcelPagedQueryService(CreateRepository(databaseName));
            var response = await service.ExecuteAsync(
                new ParcelListRequest {
                    PageNumber = 1,
                    PageSize = 10,
                    Status = (int)ParcelStatus.SortingException,
                    ExceptionType = (int)ParcelExceptionType.ParcelLost,
                    ScannedTimeStart = baseTime.AddHours(-1),
                    ScannedTimeEnd = baseTime
                },
                CancellationToken.None);

            // 步骤 1：只有 BC-EX-1（ParcelLost）满足条件，BC-EX-2 是 MechanicalFailure，BC-EX-3 是 Pending。
            Assert.Equal(1, response.TotalCount);
            Assert.Single(response.Items);
            Assert.Equal("BC-EX-1", response.Items[0].BarCodes);
            Assert.Equal((int)ParcelExceptionType.ParcelLost, response.Items[0].ExceptionType);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：传入非法 ExceptionType 时，GetParcelPagedQueryService 应抛出 ArgumentOutOfRangeException。
    /// </summary>
    [Fact]
    public async Task GetParcelPagedQueryService_WithInvalidExceptionType_ShouldThrow() {
        var databaseName = $"parcel-exceptiontype-invalid-{Guid.NewGuid():N}";
        try {
            var service = new GetParcelPagedQueryService(CreateRepository(databaseName));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync(
                new ParcelListRequest {
                    PageNumber = 1,
                    PageSize = 10,
                    ExceptionType = 9999 // 非法枚举值
                },
                CancellationToken.None));
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 创建已标记异常类型的测试包裹。
    /// </summary>
    /// <param name="barCode">条码。</param>
    /// <param name="bagCode">集包号。</param>
    /// <param name="workstation">工作台。</param>
    /// <param name="exceptionType">分拣异常类型。</param>
    /// <param name="scannedTime">扫码时间。</param>
    /// <param name="targetChuteId">目标格口。</param>
    /// <param name="actualChuteId">实际格口。</param>
    /// <returns>已标记异常状态的包裹聚合。</returns>
    private static Parcel CreateParcelWithException(
        string barCode,
        string bagCode,
        string workstation,
        ParcelExceptionType exceptionType,
        DateTime scannedTime,
        long targetChuteId,
        long actualChuteId) {
        var parcel = CreateParcel(barCode, bagCode, workstation, ParcelStatus.Pending, scannedTime, targetChuteId, actualChuteId);
        parcel.MarkSortingException(exceptionType);
        return parcel;
    }

    /// <summary>
    /// 创建仓储实例。
    /// </summary>
    /// <param name="databaseName">内存数据库名称。</param>
    /// <returns>Parcel 仓储。</returns>
    private static ParcelRepository CreateRepository(string databaseName) {
        var options = BuildOptions(databaseName);
        return new ParcelRepository(
            new TestDbContextFactory(options),
            BuildConfiguration());
    }

    /// <summary>
    /// 构建测试数据库选项。
    /// </summary>
    /// <param name="databaseName">内存数据库名称。</param>
    /// <returns>数据库选项。</returns>
    private static DbContextOptions<SortingHubDbContext> BuildOptions(string databaseName) {
        return new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    /// <summary>
    /// 构建测试配置。
    /// </summary>
    /// <returns>配置对象。</returns>
    private static IConfiguration BuildConfiguration() {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:EnableGuard"] = "false",
                ["Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:AllowDangerousActionExecution"] = "true",
                ["Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:DryRun"] = "false"
            })
            .Build();
    }

    /// <summary>
    /// 种子写入 Parcel 数据。
    /// </summary>
    /// <param name="databaseName">内存数据库名称。</param>
    /// <param name="parcels">待写入包裹集合。</param>
    private static async Task SeedParcelsAsync(string databaseName, IReadOnlyCollection<Parcel> parcels) {
        var repository = CreateRepository(databaseName);
        var result = await ((IParcelRepository)repository).AddRangeAsync(parcels, CancellationToken.None);
        Assert.True(result.IsSuccess, result.ErrorMessage);
    }

    /// <summary>
    /// 清理测试数据库。
    /// </summary>
    /// <param name="databaseName">内存数据库名称。</param>
    private static async Task CleanupDatabaseAsync(string databaseName) {
        var options = BuildOptions(databaseName);
        await using var db = new SortingHubDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }

    /// <summary>
    /// 创建测试包裹。
    /// </summary>
    /// <param name="barCode">条码。</param>
    /// <param name="bagCode">集包号。</param>
    /// <param name="workstation">工作台。</param>
    /// <param name="status">包裹状态。</param>
    /// <param name="scannedTime">扫码时间。</param>
    /// <param name="targetChuteId">目标格口。</param>
    /// <param name="actualChuteId">实际格口。</param>
    /// <returns>包裹聚合。</returns>
    private static Parcel CreateParcel(
        string barCode,
        string bagCode,
        string workstation,
        ParcelStatus status,
        DateTime scannedTime,
        long targetChuteId,
        long actualChuteId) {
        var parcel = Parcel.Create(
            id: Interlocked.Increment(ref _testParcelIdSequence),
            parcelTimestamp: Math.Abs(scannedTime.Ticks),
            type: ParcelType.Normal,
            barCodes: barCode,
            weight: 1.25m,
            workstationName: workstation,
            scannedTime: scannedTime,
            dischargeTime: scannedTime.AddSeconds(10),
            targetChuteId: targetChuteId,
            actualChuteId: actualChuteId,
            requestStatus: ApiRequestStatus.Success,
            bagCode: bagCode,
            isSticking: false,
            length: 10,
            width: 20,
            height: 30,
            volume: 6000,
            hasImages: true,
            hasVideos: false,
            coordinate: "x:10,y:20");

        if (status == ParcelStatus.Completed) {
            parcel.MarkCompleted(scannedTime.AddMinutes(1));
        }
        return parcel;
    }

    /// <summary>
    /// 测试用 DbContextFactory。
    /// </summary>
    private sealed class TestDbContextFactory : IDbContextFactory<SortingHubDbContext> {
        /// <summary>
        /// 用于创建 InMemory 测试数据库上下文的配置选项。
        /// </summary>
        private readonly DbContextOptions<SortingHubDbContext> _options;

        /// <summary>
        /// 创建测试 DbContextFactory。
        /// </summary>
        /// <param name="options">数据库选项。</param>
        public TestDbContextFactory(DbContextOptions<SortingHubDbContext> options) {
            _options = options;
        }

        /// <summary>
        /// 创建 DbContext。
        /// </summary>
        /// <returns>数据库上下文。</returns>
        public SortingHubDbContext CreateDbContext() {
            return new SortingHubDbContext(_options);
        }

        /// <summary>
        /// 异步创建 DbContext。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>数据库上下文。</returns>
        public Task<SortingHubDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            return Task.FromResult(new SortingHubDbContext(_options));
        }
    }
}
