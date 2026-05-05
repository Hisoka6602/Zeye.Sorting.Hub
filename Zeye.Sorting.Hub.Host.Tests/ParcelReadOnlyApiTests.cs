using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Host.Routing;
using ParcelListResponse = Zeye.Sorting.Hub.Contracts.Models.Parcels.ParcelListResponse;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Parcel 只读 API 端点回归测试。
/// </summary>
public sealed class ParcelReadOnlyApiTests {
    /// <summary>
    /// 邻近查询稳定排序测试锚点 Id。
    /// </summary>
    private const long StableOrderAnchorId = 11;

    /// <summary>
    /// 验证场景：正常获取列表。
    /// </summary>
    [Fact]
    public async Task GetParcels_ShouldReturnPagedList() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels?pageNumber=1&pageSize=10&bagCode=BAG-LIST");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ParcelListResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.PageNumber);
        Assert.Equal(10, payload.PageSize);
        Assert.Equal(1, payload.TotalCount);
        Assert.Single(payload.Items);
        Assert.Equal("BAG-LIST", payload.Items[0].BagCode);
    }

    /// <summary>
    /// 验证场景：正常获取详情。
    /// </summary>
    [Fact]
    public async Task GetParcelById_ShouldReturnDetail() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.RootElement.GetProperty("id").GetInt64());
        Assert.Equal("BC-DETAIL-1", payload.RootElement.GetProperty("barCodes").GetString());
    }

    /// <summary>
    /// 验证场景：详情不存在返回 404。
    /// </summary>
    [Fact]
    public async Task GetParcelById_WhenNotFound_ShouldReturnNotFound() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/404");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：邻近查询 id 缺失返回 400。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WithMissingId_ShouldReturnBadRequest() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/adjacent?beforeCount=1&afterCount=1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("id 为必填参数", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：列表查询拒绝 UTC/offset 时间参数。
    /// </summary>
    [Fact]
    public async Task GetParcels_WithUtcOrOffsetTime_ShouldReturnBadRequest() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var utcResponse = await client.GetAsync("/api/parcels?scannedTimeStart=2026-03-20T10:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, utcResponse.StatusCode);

        var offsetResponse = await client.GetAsync("/api/parcels?scannedTimeEnd=2026-03-20T10:00:00+08:00");
        Assert.Equal(HttpStatusCode.BadRequest, offsetResponse.StatusCode);
    }

    /// <summary>
    /// 验证场景：邻近查询 id 非法返回 400。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WithInvalidId_ShouldReturnBadRequest() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/adjacent?id=0&beforeCount=1&afterCount=1");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("包裹 Id 必须大于 0", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：邻近查询锚点不存在返回 404。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WhenAnchorNotFound_ShouldReturnNotFound() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync("/api/parcels/adjacent?id=999&beforeCount=2&afterCount=2");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：邻近查询按 id 正常返回前后记录。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WithValidId_ShouldReturnAdjacentItems() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/parcels/adjacent?id=2&beforeCount=1&afterCount=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.RootElement.GetProperty("beforeCount").GetInt32());
        Assert.Equal(1, payload.RootElement.GetProperty("afterCount").GetInt32());
        var items = payload.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].GetProperty("id").GetInt64());
        Assert.Equal(3, items[1].GetProperty("id").GetInt64());
    }

    /// <summary>
    /// 验证场景：同一 ScannedTime 下按 Id 保持稳定排序。
    /// </summary>
    [Fact]
    public async Task GetAdjacentParcels_WithSameScannedTime_ShouldKeepStableOrder() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        var response = await client.GetAsync($"/api/parcels/adjacent?id={StableOrderAnchorId}&beforeCount=1&afterCount=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        var ids = payload.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64())
            .ToArray();
        Assert.Equal([10L, 12L, 13L], ids);
    }

    /// <summary>
    /// 构建测试用 WebApplication。
    /// </summary>
    /// <returns>已启动的测试应用。</returns>
    private static async Task<WebApplication> BuildTestAppAsync() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddScoped<IParcelRepository, FakeParcelRepository>();
        builder.Services.AddScoped<GetParcelPagedQueryService>();
        builder.Services.AddScoped<GetParcelCursorPagedQueryService>();
        builder.Services.AddScoped<GetParcelByIdQueryService>();
        builder.Services.AddScoped<GetAdjacentParcelsQueryService>();

        var app = builder.Build();
        app.MapParcelReadOnlyApis();
        await app.StartAsync();
        return app;
    }
}
