using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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
            await SeedParcelsAsync(databaseName, [
                CreateParcel("BC-ADJ-1", "BAG-ADJ", "WS-ADJ", ParcelStatus.Pending, baseTime.AddMinutes(1), 940, 941),
                CreateParcel("BC-ADJ-2", "BAG-ADJ", "WS-ADJ", ParcelStatus.Pending, baseTime.AddMinutes(2), 940, 942),
                CreateParcel("BC-ADJ-3", "BAG-ADJ", "WS-ADJ", ParcelStatus.Pending, baseTime.AddMinutes(3), 940, 943)
            ]);
            var repository = CreateRepository(databaseName);
            var service = new GetAdjacentParcelsQueryService(repository);

            var response = await service.ExecuteAsync(
                new ParcelAdjacentRequest {
                    ScannedTime = baseTime.AddMinutes(2),
                    BeforeCount = 200,
                    AfterCount = 200
                },
                CancellationToken.None);
            Assert.Equal(100, response.BeforeCount);
            Assert.Equal(100, response.AfterCount);
            Assert.Equal(2, response.Items.Count);
            Assert.Equal("BC-ADJ-1", response.Items[0].BarCodes);
            Assert.Equal("BC-ADJ-3", response.Items[1].BarCodes);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync(
                new ParcelAdjacentRequest {
                    ScannedTime = baseTime,
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
    /// 创建仓储实例。
    /// </summary>
    /// <param name="databaseName">内存数据库名称。</param>
    /// <returns>Parcel 仓储。</returns>
    private static ParcelRepository CreateRepository(string databaseName) {
        var options = BuildOptions(databaseName);
        return new ParcelRepository(
            new TestDbContextFactory(options),
            NullLogger<ParcelRepository>.Instance,
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
