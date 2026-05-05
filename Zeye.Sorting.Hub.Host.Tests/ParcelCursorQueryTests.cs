using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Repositories;
using Zeye.Sorting.Hub.Host.Routing;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Parcel 游标分页查询回归测试。
/// </summary>
public sealed class ParcelCursorQueryTests {
    /// <summary>
    /// 验证场景：游标分页首页返回第一页数据与下一页游标。
    /// </summary>
    [Fact]
    public async Task GetParcelCursorList_ShouldReturnFirstPageAndNextCursor() {
        var repository = new FakeParcelRepository();
        await using var app = await BuildCursorTestAppAsync(repository);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/parcels/cursor?pageSize=2&bagCode=BAG-CURSOR");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ParcelCursorListResponse>();
        Assert.NotNull(payload);
        Assert.Equal(2, payload.PageSize);
        Assert.True(payload.HasMore);
        Assert.False(string.IsNullOrWhiteSpace(payload.NextCursor));
        Assert.Equal([4L, 3L], payload.Items.Select(item => item.Id));
    }

    /// <summary>
    /// 验证场景：带下一页游标可继续查询第二页。
    /// </summary>
    [Fact]
    public async Task GetParcelCursorList_WithNextCursor_ShouldReturnSecondPage() {
        var repository = new FakeParcelRepository();
        await using var app = await BuildCursorTestAppAsync(repository);
        using var client = app.GetTestClient();

        var firstPageResponse = await client.GetFromJsonAsync<ParcelCursorListResponse>("/api/parcels/cursor?pageSize=2&bagCode=BAG-CURSOR");
        Assert.NotNull(firstPageResponse);

        var secondPageResponse = await client.GetAsync($"/api/parcels/cursor?pageSize=2&cursor={Uri.EscapeDataString(firstPageResponse.NextCursor!)}&bagCode=BAG-CURSOR");

        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
        var payload = await secondPageResponse.Content.ReadFromJsonAsync<ParcelCursorListResponse>();
        Assert.NotNull(payload);
        Assert.False(payload.HasMore);
        Assert.Null(payload.NextCursor);
        Assert.Equal([2L, 1L], payload.Items.Select(item => item.Id));
    }

    /// <summary>
    /// 验证场景：非法游标返回 400。
    /// </summary>
    [Fact]
    public async Task GetParcelCursorList_WithInvalidCursor_ShouldReturnBadRequest() {
        var repository = new FakeParcelRepository();
        await using var app = await BuildCursorTestAppAsync(repository);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/parcels/cursor?cursor=invalid-cursor");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：游标分页页大小超过上限时会归一化到 200。
    /// </summary>
    [Fact]
    public async Task GetParcelCursorList_WithOversizedPageSize_ShouldNormalizeToMaxPageSize() {
        var repository = new FakeParcelRepository();
        await using var app = await BuildCursorTestAppAsync(repository);
        using var client = app.GetTestClient();

        var response = await client.GetFromJsonAsync<ParcelCursorListResponse>("/api/parcels/cursor?pageSize=999");

        Assert.NotNull(response);
        Assert.Equal(CursorPageRequest.MaxPageSize, response.PageSize);
    }

    /// <summary>
    /// 验证场景：游标查询未指定时间范围时默认仅查询最近 24 小时。
    /// </summary>
    [Fact]
    public async Task GetParcelCursorPagedQueryService_WithoutTimeRange_ShouldDefaultRecentTwentyFourHours() {
        var repository = new FakeParcelRepository();
        var service = new GetParcelCursorPagedQueryService(repository);
        var startBoundary = DateTime.Now;

        await service.ExecuteAsync(new ParcelCursorListRequest {
            PageSize = 10
        }, CancellationToken.None);

        var endBoundary = DateTime.Now;
        Assert.NotNull(repository.LastCursorFilter);
        Assert.NotNull(repository.LastCursorFilter!.ScannedTimeStart);
        Assert.NotNull(repository.LastCursorFilter.ScannedTimeEnd);
        Assert.InRange(repository.LastCursorFilter.ScannedTimeEnd.Value, startBoundary.AddSeconds(-1), endBoundary.AddSeconds(1));
        var range = repository.LastCursorFilter.ScannedTimeEnd.Value - repository.LastCursorFilter.ScannedTimeStart.Value;
        Assert.InRange(range.TotalHours, 23.99, 24.01);
    }

    /// <summary>
    /// 验证场景：普通分页页码超过 10000 时拒绝执行。
    /// </summary>
    [Fact]
    public async Task GetParcelPagedQueryService_WithTooLargePageNumber_ShouldThrowArgumentOutOfRangeException() {
        var repository = new FakeParcelRepository();
        var service = new GetParcelPagedQueryService(repository);

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.ExecuteAsync(new ParcelListRequest {
            PageNumber = 10001,
            PageSize = 20
        }, CancellationToken.None));

        Assert.Contains("页码不能超过 10000", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：仓储游标分页保持 ScannedTime DESC, Id DESC 的稳定排序。
    /// </summary>
    [Fact]
    public async Task ParcelRepository_GetCursorPagedAsync_ShouldKeepStableSortAcrossPages() {
        var databaseName = $"parcel-cursor-repository-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        var factory = new SortingHubTestDbContextFactory(options);
        var repository = new ParcelRepository(factory);
        var baseTime = LocalTimeTestConstraint.CreateLocalTime(2026, 3, 21, 10, 0, 0);

        try {
            var seedResult = await repository.AddRangeAsync([
                CreateParcel(101, "BC-CURSOR-101", baseTime, "BAG-CURSOR"),
                CreateParcel(102, "BC-CURSOR-102", baseTime, "BAG-CURSOR"),
                CreateParcel(103, "BC-CURSOR-103", baseTime.AddMinutes(-1), "BAG-CURSOR")
            ], CancellationToken.None);
            Assert.True(seedResult.IsSuccess, seedResult.ErrorMessage);

            var filter = new ParcelQueryFilter {
                BagCode = "BAG-CURSOR",
                ScannedTimeStart = baseTime.AddHours(-1),
                ScannedTimeEnd = baseTime.AddHours(1)
            };
            var firstPage = await repository.GetCursorPagedAsync(filter, new CursorPageRequest {
                PageSize = 2
            }, CancellationToken.None);

            Assert.Equal([102L, 101L], firstPage.Items.Select(item => item.Id));
            Assert.True(firstPage.HasMore);
            Assert.Equal(101L, firstPage.NextId);
            LocalTimeTestConstraint.AssertIsLocalTime(firstPage.NextScannedTimeLocal!.Value);

            var secondPage = await repository.GetCursorPagedAsync(filter, new CursorPageRequest {
                PageSize = 2,
                LastScannedTimeLocal = firstPage.NextScannedTimeLocal,
                LastId = firstPage.NextId
            }, CancellationToken.None);

            Assert.Equal([103L], secondPage.Items.Select(item => item.Id));
            Assert.False(secondPage.HasMore);
        }
        finally {
            await using var dbContext = new SortingHubDbContext(options);
            await dbContext.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>
    /// 构建游标分页测试应用。
    /// </summary>
    /// <param name="repository">测试仓储。</param>
    /// <returns>已启动应用。</returns>
    private static async Task<WebApplication> BuildCursorTestAppAsync(FakeParcelRepository repository) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddSingleton(repository);
        builder.Services.AddSingleton<Zeye.Sorting.Hub.Domain.Repositories.IParcelRepository>(repository);
        builder.Services.AddScoped<GetParcelPagedQueryService>();
        builder.Services.AddScoped<GetParcelCursorPagedQueryService>();
        builder.Services.AddScoped<GetParcelByIdQueryService>();
        builder.Services.AddScoped<GetAdjacentParcelsQueryService>();

        var app = builder.Build();
        app.MapParcelReadOnlyApis();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 创建测试包裹。
    /// </summary>
    /// <param name="id">包裹 Id。</param>
    /// <param name="barCode">条码。</param>
    /// <param name="scannedTime">扫码时间。</param>
    /// <param name="bagCode">集包号。</param>
    /// <returns>测试包裹。</returns>
    private static Parcel CreateParcel(long id, string barCode, DateTime scannedTime, string bagCode) {
        return Parcel.Create(
            id: id,
            parcelTimestamp: Math.Abs(scannedTime.Ticks),
            type: ParcelType.Normal,
            barCodes: barCode,
            weight: 1.0m,
            workstationName: "WS-CURSOR",
            scannedTime: scannedTime,
            dischargeTime: scannedTime.AddSeconds(3),
            targetChuteId: 900,
            actualChuteId: 901,
            requestStatus: ApiRequestStatus.Success,
            bagCode: bagCode,
            isSticking: false,
            length: 10m,
            width: 20m,
            height: 30m,
            volume: 6000m,
            hasImages: true,
            hasVideos: false,
            coordinate: "x:10,y:10");
    }
}
