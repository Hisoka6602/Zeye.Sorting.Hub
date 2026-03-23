using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    /// 验证场景：IParcelRepository_ShouldResolveFromDependencyInjection。
    /// </summary>
    [Fact]
    public void IParcelRepository_ShouldResolveFromDependencyInjection() {
        var databaseName = $"parcel-repo-di-test-{Guid.NewGuid():N}";
        var options = BuildOptions(databaseName);
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<SortingHubDbContext>>(new TestDbContextFactory(options));
        services.AddScoped<IParcelRepository, ParcelRepository>();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        _ = scope.ServiceProvider.GetRequiredService<IParcelRepository>();
    }

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
    /// 验证当 actualChuteId 与 targetChuteId 同时为 null 时，仓储边界应拒绝调用并抛出包含“至少提供一个格口 Id”的参数异常。
    /// </summary>
    [Fact]
    public async Task GetByChuteAsync_WhenActualAndTargetBothNull_ShouldThrowArgumentException() {
        var databaseName = $"parcel-repo-test-{Guid.NewGuid():N}";
        var baseTime = DateTime.Now;
        try {
            var repository = CreateRepository(databaseName);

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => repository.GetByChuteAsync(
                null,
                null,
                baseTime.AddHours(-1),
                baseTime.AddHours(1),
                new PageRequest(),
                CancellationToken.None));

            Assert.Contains("至少提供一个格口 Id", exception.Message);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证当仅提供 actualChuteId 时，查询仍可执行且只返回实际格口匹配的数据。
    /// </summary>
    [Fact]
    public async Task GetByChuteAsync_WhenOnlyActualChuteIdProvided_ShouldFilterByActualChuteId() {
        var databaseName = $"parcel-repo-test-{Guid.NewGuid():N}";
        var baseTime = DateTime.Now;
        try {
            var repository = CreateRepository(databaseName);
            await SeedParcelsAsync(databaseName, [
                CreateParcel("BC-AC-1", "BAG-AC", "WS-AC", ParcelStatus.Pending, baseTime.AddMinutes(-20), 700, 800),
                CreateParcel("BC-AC-2", "BAG-AC", "WS-AC", ParcelStatus.Pending, baseTime.AddMinutes(-10), 701, 801)
            ]);

            var result = await repository.GetByChuteAsync(
                actualChuteId: 800,
                targetChuteId: null,
                scannedTimeStart: baseTime.AddHours(-1),
                scannedTimeEnd: baseTime.AddHours(1),
                pageRequest: new PageRequest { PageNumber = 1, PageSize = 10 },
                cancellationToken: CancellationToken.None);

            Assert.Equal(1, result.TotalCount);
            Assert.Single(result.Items);
            Assert.Equal(800, result.Items[0].ActualChuteId);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证当仅提供 targetChuteId 时，查询仍可执行且只返回目标格口匹配的数据。
    /// </summary>
    [Fact]
    public async Task GetByChuteAsync_WhenOnlyTargetChuteIdProvided_ShouldFilterByTargetChuteId() {
        var databaseName = $"parcel-repo-test-{Guid.NewGuid():N}";
        var baseTime = DateTime.Now;
        try {
            var repository = CreateRepository(databaseName);
            await SeedParcelsAsync(databaseName, [
                CreateParcel("BC-TC-1", "BAG-TC", "WS-TC", ParcelStatus.Pending, baseTime.AddMinutes(-20), 700, 810),
                CreateParcel("BC-TC-2", "BAG-TC", "WS-TC", ParcelStatus.Pending, baseTime.AddMinutes(-10), 701, 811)
            ]);

            var result = await repository.GetByChuteAsync(
                actualChuteId: null,
                targetChuteId: 700,
                scannedTimeStart: baseTime.AddHours(-1),
                scannedTimeEnd: baseTime.AddHours(1),
                pageRequest: new PageRequest { PageNumber = 1, PageSize = 10 },
                cancellationToken: CancellationToken.None);

            Assert.Equal(1, result.TotalCount);
            Assert.Single(result.Items);
            Assert.Equal(700, result.Items[0].TargetChuteId);
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
    /// 验证场景：GetPagedAsync_BarCodeKeyword_ShouldMatchViaFallbackContains。
    /// InMemory Provider（非 MySQL）走 Contains() 回退路径（子串匹配）。
    /// 覆盖含 - 的条码关键词（BC-001）在回退路径下的正确匹配行为。
    /// MySQL FULLTEXT phrase 搜索分支（EF.Functions.IsMatch）需真实 MySQL 环境，
    /// 超出当前 InMemory 测试基础设施范围，由集成测试覆盖。
    /// </summary>
    [Fact]
    public async Task GetPagedAsync_BarCodeKeyword_ShouldMatchViaFallbackContains() {
        var databaseName = $"parcel-repo-barcode-kw-{Guid.NewGuid():N}";
        var baseTime = DateTime.Now;
        try {
            var repository = CreateRepository(databaseName);
            await SeedParcelsAsync(databaseName, [
                // 步骤 1：种入含 - 分隔符条码的包裹（覆盖 - 字符在非 BOOLEAN MODE 路径下不被当作排除操作符）。
                CreateParcel("BC-001-XYZ", "BAG-KW", "WS-KW", ParcelStatus.Pending, baseTime.AddMinutes(-5), 900, 901),
                CreateParcel("BC-001-ABC", "BAG-KW", "WS-KW", ParcelStatus.Pending, baseTime.AddMinutes(-4), 900, 901),
                CreateParcel("UNRELATED-999", "BAG-KW", "WS-KW", ParcelStatus.Pending, baseTime.AddMinutes(-3), 900, 901)
            ]);

            // 步骤 2：用前缀关键词搜索，应命中两条含 BC-001 的记录，第三条不匹配。
            var result = await repository.GetPagedAsync(
                new ParcelQueryFilter {
                    BarCodeKeyword = "BC-001",
                    ScannedTimeStart = baseTime.AddHours(-1),
                    ScannedTimeEnd = baseTime.AddHours(1)
                },
                new PageRequest { PageNumber = 1, PageSize = 10 },
                CancellationToken.None);

            Assert.Equal(2, result.TotalCount);
            Assert.All(result.Items, item => Assert.Contains("BC-001", item.BarCodes));

            // 步骤 3：空格修剪 — 两端空白不影响匹配结果。
            var resultTrimmed = await repository.GetPagedAsync(
                new ParcelQueryFilter {
                    BarCodeKeyword = "  BC-001  ",
                    ScannedTimeStart = baseTime.AddHours(-1),
                    ScannedTimeEnd = baseTime.AddHours(1)
                },
                new PageRequest { PageNumber = 1, PageSize = 10 },
                CancellationToken.None);

            Assert.Equal(2, resultTrimmed.TotalCount);

            // 步骤 4：不存在的关键词 — 应返回空结果集。
            var resultNone = await repository.GetPagedAsync(
                new ParcelQueryFilter {
                    BarCodeKeyword = "NOTEXIST",
                    ScannedTimeStart = baseTime.AddHours(-1),
                    ScannedTimeEnd = baseTime.AddHours(1)
                },
                new PageRequest { PageNumber = 1, PageSize = 10 },
                CancellationToken.None);

            Assert.Equal(0, resultNone.TotalCount);
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
            var addResult = await contractRepository.AddAsync(parcel, CancellationToken.None);
            Assert.True(addResult.IsSuccess, addResult.ErrorMessage);

            var saved = await repository.GetByIdAsync(parcel.Id, CancellationToken.None);
            Assert.NotNull(saved);

            saved!.UpdateRequestStatus(ApiRequestStatus.Failed);
            var updateResult = await contractRepository.UpdateAsync(saved, CancellationToken.None);
            Assert.True(updateResult.IsSuccess, updateResult.ErrorMessage);

            var updated = await repository.GetByIdAsync(saved.Id, CancellationToken.None);
            Assert.NotNull(updated);
            Assert.Equal(ApiRequestStatus.Failed, updated!.RequestStatus);

            var oldParcel = CreateParcel("BC-W-2", "BAG-W", "WS-W", ParcelStatus.Completed, baseTime.AddMinutes(-20), 502, 602);
            var addRangeResult = await contractRepository.AddRangeAsync([oldParcel], CancellationToken.None);
            Assert.True(addRangeResult.IsSuccess, addRangeResult.ErrorMessage);

            // 步骤 1：默认配置下危险清理动作应被守卫阻断（安全默认值）。
            var removeExpiredBlockedResult = await contractRepository.RemoveExpiredAsync(baseTime.AddMinutes(1), CancellationToken.None);
            Assert.True(removeExpiredBlockedResult.IsSuccess, removeExpiredBlockedResult.ErrorMessage);
            var blockedAction = removeExpiredBlockedResult.Value;
            Assert.True(blockedAction.IsBlockedByGuard);
            Assert.False(blockedAction.IsDryRun);
            Assert.Equal(ActionIsolationDecision.BlockedByGuard, blockedAction.Decision);
            Assert.Equal(0, blockedAction.ExecutedCount);

            // 步骤 2：关闭守卫并开启 dry-run 时，应仅审计不执行。
            var dryRunRepository = CreateRepository(
                databaseName,
                BuildRemoveExpiredIsolationConfiguration(enableGuard: false, allowDangerousActionExecution: false, dryRun: true));
            var removeExpiredDryRunResult = await dryRunRepository.RemoveExpiredAsync(baseTime.AddMinutes(1), CancellationToken.None);
            Assert.True(removeExpiredDryRunResult.IsSuccess, removeExpiredDryRunResult.ErrorMessage);
            var dryRunAction = removeExpiredDryRunResult.Value;
            Assert.True(dryRunAction.IsDryRun);
            Assert.False(dryRunAction.IsBlockedByGuard);
            Assert.Equal(ActionIsolationDecision.DryRunOnly, dryRunAction.Decision);
            Assert.Equal(0, dryRunAction.ExecutedCount);
            Assert.True(dryRunAction.PlannedCount >= 2);

            // 步骤 3：显式放开危险动作且关闭 dry-run 后，才允许真实删除。
            var executeRepository = CreateRepository(
                databaseName,
                BuildRemoveExpiredIsolationConfiguration(enableGuard: true, allowDangerousActionExecution: true, dryRun: false));
            var removeExpiredExecutedResult = await executeRepository.RemoveExpiredAsync(baseTime.AddMinutes(1), CancellationToken.None);
            Assert.True(removeExpiredExecutedResult.IsSuccess, removeExpiredExecutedResult.ErrorMessage);
            var executedAction = removeExpiredExecutedResult.Value;
            Assert.Equal(ActionIsolationDecision.Execute, executedAction.Decision);
            Assert.False(executedAction.IsBlockedByGuard);
            Assert.False(executedAction.IsDryRun);
            Assert.True(executedAction.ExecutedCount >= 2);
            Assert.False(string.IsNullOrWhiteSpace(executedAction.CompensationBoundary));
            Assert.Contains("回滚", executedAction.CompensationBoundary);

            var deleteTarget = CreateParcel("BC-W-3", "BAG-W", "WS-W", ParcelStatus.Pending, baseTime.AddMinutes(-5), 503, 603);
            var addDeleteTargetResult = await contractRepository.AddAsync(deleteTarget, CancellationToken.None);
            Assert.True(addDeleteTargetResult.IsSuccess, addDeleteTargetResult.ErrorMessage);
            var removeResult = await contractRepository.RemoveAsync(deleteTarget, CancellationToken.None);
            Assert.True(removeResult.IsSuccess, removeResult.ErrorMessage);

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
    private static ParcelRepository CreateRepository(string databaseName, IConfiguration? configuration = null) {
        var options = BuildOptions(databaseName);
        var factory = new TestDbContextFactory(options);
        return new ParcelRepository(factory, configuration);
    }

    /// <summary>
    /// 构建过期清理危险动作隔离配置。
    /// </summary>
    private static IConfiguration BuildRemoveExpiredIsolationConfiguration(
        bool enableGuard,
        bool allowDangerousActionExecution,
        bool dryRun) {
        var values = new Dictionary<string, string?> {
            [ParcelRepository.RemoveExpiredEnableGuardConfigKey] = enableGuard.ToString(),
            [ParcelRepository.RemoveExpiredAllowExecutionConfigKey] = allowDangerousActionExecution.ToString(),
            [ParcelRepository.RemoveExpiredDryRunConfigKey] = dryRun.ToString()
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
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
