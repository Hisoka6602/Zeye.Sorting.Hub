using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// ParcelRepository 第一阶段能力回归测试。
/// </summary>
public sealed class ParcelRepositoryTests {
    /// <summary>
    /// 验证场景：GetPagedAsync_ShouldReturnSummaryWithExpectedFilterAndPaging。
    /// </summary>
    [Fact]
    public async Task GetPagedAsync_ShouldReturnSummaryWithExpectedFilterAndPaging() {
        var databaseName = $"parcel-repo-test-{Guid.NewGuid():N}";
        var baseTime = DateTime.Now;
        try {
            var repository = CreateRepository(databaseName);
            await SeedParcelsAsync(databaseName, [
                CreateParcel("BC-001", "BAG-A", "WS-1", ParcelStatus.Pending, baseTime.AddMinutes(-3), 100, 101),
                CreateParcel("BC-002", "BAG-A", "WS-1", ParcelStatus.Pending, baseTime.AddMinutes(-2), 100, 101),
                CreateParcel("BC-003", "BAG-B", "WS-2", ParcelStatus.Completed, baseTime.AddMinutes(-1), 200, 201)
            ]);

            var result = await repository.GetPagedAsync(
                new ParcelQueryFilter {
                    BagCode = "BAG-A",
                    WorkstationName = "WS-1",
                    Status = ParcelStatus.Pending,
                    ScannedTimeStart = baseTime.AddHours(-2),
                    ScannedTimeEnd = baseTime
                },
                new PageRequest { PageNumber = 1, PageSize = 1 },
                CancellationToken.None);

            Assert.Equal(1, result.PageNumber);
            Assert.Equal(1, result.PageSize);
            Assert.Equal(2, result.TotalCount);
            Assert.Single(result.Items);
            Assert.Equal("BAG-A", result.Items[0].BagCode);
            Assert.Equal("WS-1", result.Items[0].WorkstationName);
            Assert.Equal(ParcelStatus.Pending, result.Items[0].Status);
            Assert.Equal(ApiRequestStatus.Success, result.Items[0].RequestStatus);
            Assert.NotNull(result.Items[0].ModifyIp);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：GetByIdAsync_AndAdjacent_ShouldReturnAggregateAndNeighbors。
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_AndAdjacent_ShouldReturnAggregateAndNeighbors() {
        var databaseName = $"parcel-repo-test-{Guid.NewGuid():N}";
        var baseTime = DateTime.Now.AddHours(-1);
        try {
            var parcel1 = CreateParcel("BC-101", "BAG-X", "WS-X", ParcelStatus.Pending, baseTime.AddMinutes(1), 301, 401);
            parcel1.AddBarCodeInfo(new BarCodeInfo {
                BarCode = "SUB-101",
                BarCodeType = BarCodeType.ExpressSheet,
                CapturedTime = baseTime.AddMinutes(1)
            });

            var parcel2 = CreateParcel("BC-102", "BAG-X", "WS-X", ParcelStatus.Pending, baseTime.AddMinutes(2), 301, 401);
            var parcel3 = CreateParcel("BC-103", "BAG-Y", "WS-Y", ParcelStatus.Completed, baseTime.AddMinutes(3), 302, 402);

            await SeedParcelsAsync(databaseName, [parcel1, parcel2, parcel3]);

            var repository = CreateRepository(databaseName);
            var detail = await repository.GetByIdAsync(parcel2.Id, CancellationToken.None);

            Assert.NotNull(detail);
            Assert.Equal("BC-102", detail.BarCodes);

            var adjacent = await repository.GetAdjacentByScannedTimeAsync(baseTime.AddMinutes(2), 1, 1, CancellationToken.None);
            Assert.Equal(2, adjacent.Count);
            Assert.Equal("BC-101", adjacent[0].BarCodes);
            Assert.Equal("BC-103", adjacent[1].BarCodes);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：GetBySpecificFilters_ShouldRequireTimeRangeAndWork。
    /// </summary>
    [Fact]
    public async Task GetBySpecificFilters_ShouldRequireTimeRangeAndWork() {
        var databaseName = $"parcel-repo-test-{Guid.NewGuid():N}";
        var baseTime = DateTime.Now;
        try {
            var repository = CreateRepository(databaseName);
            var startTime = baseTime.AddHours(-2);
            var endTime = baseTime;

            await SeedParcelsAsync(databaseName, [
                CreateParcel("BC-F-1", "BAG-F", "WS-F", ParcelStatus.Pending, baseTime.AddMinutes(-30), 700, 800),
                CreateParcel("BC-F-2", "BAG-F", "WS-F", ParcelStatus.Completed, baseTime.AddMinutes(-20), 700, 801)
            ]);

            var byBag = await repository.GetByBagCodeAsync("BAG-F", startTime, endTime, new PageRequest(), CancellationToken.None);
            Assert.Equal(2, byBag.TotalCount);

            var byWorkstation = await repository.GetByWorkstationNameAsync("WS-F", startTime, endTime, new PageRequest(), CancellationToken.None);
            Assert.Equal(2, byWorkstation.TotalCount);

            var byStatus = await repository.GetByStatusAsync(ParcelStatus.Pending, startTime, endTime, new PageRequest(), CancellationToken.None);
            Assert.Equal(1, byStatus.TotalCount);

            var byChute = await repository.GetByChuteAsync(800, 700, startTime, endTime, new PageRequest(), CancellationToken.None);
            Assert.Equal(1, byChute.TotalCount);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：GetPagedAsync_ShouldRejectTimeRangeExceedingThreeMonths。
    /// </summary>
    [Fact]
    public async Task GetPagedAsync_ShouldRejectTimeRangeExceedingThreeMonths() {
        var databaseName = $"parcel-repo-test-{Guid.NewGuid():N}";
        var baseTime = DateTime.Now;
        try {
            var repository = CreateRepository(databaseName);
            var exception = await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => repository.GetPagedAsync(
                new ParcelQueryFilter {
                    ScannedTimeStart = baseTime.AddMonths(-4),
                    ScannedTimeEnd = baseTime
                },
                new PageRequest(),
                CancellationToken.None));

            Assert.Contains("3 个月", exception.Message);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：WriteOperations_ShouldAddUpdateRemoveAndCleanupExpired。
    /// </summary>
    [Fact]
    public async Task WriteOperations_ShouldAddUpdateRemoveAndCleanupExpired() {
        var databaseName = $"parcel-repo-test-{Guid.NewGuid():N}";
        var baseTime = DateTime.Now;
        try {
            var repository = CreateRepository(databaseName);
            var contractRepository = (IParcelRepository)repository;

            var parcel = CreateParcel("BC-W-1", "BAG-W", "WS-W", ParcelStatus.Pending, baseTime.AddMinutes(-10), 501, 601);
            await contractRepository.AddAsync(parcel, CancellationToken.None);

            var saved = await repository.GetByIdAsync(parcel.Id, CancellationToken.None);
            Assert.NotNull(saved);

            saved!.UpdateRequestStatus(ApiRequestStatus.Failed);
            await contractRepository.UpdateAsync(saved, CancellationToken.None);

            var updated = await repository.GetByIdAsync(saved.Id, CancellationToken.None);
            Assert.NotNull(updated);
            Assert.Equal(ApiRequestStatus.Failed, updated!.RequestStatus);

            var oldParcel = CreateParcel("BC-W-2", "BAG-W", "WS-W", ParcelStatus.Completed, baseTime.AddMinutes(-20), 502, 602);
            await contractRepository.AddRangeAsync([oldParcel], CancellationToken.None);

            var removedExpiredCount = await repository.RemoveExpiredAsync(baseTime.AddMinutes(1), CancellationToken.None);
            Assert.True(removedExpiredCount >= 2);

            var deleteTarget = CreateParcel("BC-W-3", "BAG-W", "WS-W", ParcelStatus.Pending, baseTime.AddMinutes(-5), 503, 603);
            await contractRepository.AddAsync(deleteTarget, CancellationToken.None);
            await contractRepository.RemoveAsync(deleteTarget, CancellationToken.None);

            var deleted = await repository.GetByIdAsync(deleteTarget.Id, CancellationToken.None);
            Assert.Null(deleted);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 创建 ParcelRepository 测试实例。
    /// </summary>
    private static ParcelRepository CreateRepository(string databaseName) {
        var options = BuildOptions(databaseName);
        var factory = new TestDbContextFactory(options);
        return new ParcelRepository(factory, NullLogger<ParcelRepository>.Instance);
    }

    /// <summary>
    /// 批量写入测试数据。
    /// </summary>
    private static async Task SeedParcelsAsync(string databaseName, IReadOnlyCollection<Parcel> parcels) {
        var options = BuildOptions(databaseName);
        await using var db = new SortingHubDbContext(options);

        foreach (var parcel in parcels) {
            if (parcel.Status == ParcelStatus.Completed) {
                parcel.MarkCompleted(parcel.ScannedTime.AddMinutes(5));
            }
        }

        await db.Set<Parcel>().AddRangeAsync(parcels);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// 构建 InMemory DbContext 选项。
    /// </summary>
    private static DbContextOptions<SortingHubDbContext> BuildOptions(string databaseName) {
        return new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    /// <summary>
    /// 创建测试 Parcel。
    /// </summary>
    private static Parcel CreateParcel(
        string barCodes,
        string bagCode,
        string workstationName,
        ParcelStatus status,
        DateTime scannedTime,
        long targetChuteId,
        long actualChuteId) {
        var parcel = Parcel.Create(
            parcelTimestamp: Math.Abs(scannedTime.Ticks),
            type: ParcelType.Normal,
            barCodes: barCodes,
            weight: 1.1m,
            workstationName: workstationName,
            scannedTime: scannedTime,
            dischargeTime: scannedTime.AddMinutes(1),
            targetChuteId: targetChuteId,
            actualChuteId: actualChuteId,
            requestStatus: ApiRequestStatus.Success,
            bagCode: bagCode,
            isSticking: false,
            length: 1,
            width: 1,
            height: 1,
            volume: 1,
            hasImages: false,
            hasVideos: false,
            coordinate: "0,0");

        if (status == ParcelStatus.Completed) {
            parcel.MarkCompleted(scannedTime.AddMinutes(3));
        }

        return parcel;
    }

    /// <summary>
    /// 删除测试数据库，确保用例资源及时释放。
    /// </summary>
    private static async Task CleanupDatabaseAsync(string databaseName) {
        var options = BuildOptions(databaseName);
        await using var db = new SortingHubDbContext(options);
        await db.Database.EnsureDeletedAsync();
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
        public TestDbContextFactory(DbContextOptions<SortingHubDbContext> options) {
            _options = options;
        }

        /// <summary>
        /// 创建 DbContext。
        /// </summary>
        public SortingHubDbContext CreateDbContext() {
            return new SortingHubDbContext(_options);
        }

        /// <summary>
        /// 异步创建 DbContext。
        /// </summary>
        public Task<SortingHubDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            return Task.FromResult(new SortingHubDbContext(_options));
        }
    }
}
