using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Parcel 管理端写接口回归测试。
/// 覆盖：普通写接口成功路径、参数非法校验、cleanup-expired 三态（blocked/dry-run/execute）。
/// </summary>
public sealed class ParcelAdminApiTests {

    // ═══════════════════════════════════════════════════════════════════════
    // POST /api/admin/parcels
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 验证场景：新增 Parcel 成功，返回 201 Created 及详情。
    /// </summary>
    [Fact]
    public async Task CreateParcel_WithValidRequest_ShouldReturn201() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var body = BuildCreateRequestJson(
            scannedTime: "2026-03-20T10:00:00",
            dischargeTime: "2026-03-20T10:00:03");

        using var response = await client.PostAsync("/api/admin/parcels", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        // 新增后 Id 由 FakeParcelRepository 分配（>=100），条码应与请求一致
        Assert.Equal("BC-ADMIN-TEST", payload.RootElement.GetProperty("barCodes").GetString());
    }

    /// <summary>
    /// 验证场景：新增时传入 UTC 时间（Z 后缀），返回 400 Bad Request。
    /// </summary>
    [Fact]
    public async Task CreateParcel_WithUtcScannedTime_ShouldReturn400() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var body = BuildCreateRequestJson(
            scannedTime: "2026-03-20T10:00:00Z",
            dischargeTime: "2026-03-20T10:00:03");

        using var response = await client.PostAsync("/api/admin/parcels", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("本地时间格式", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：新增时传入带 offset 的时间（+08:00），返回 400 Bad Request（字符串解析可严格拒绝 offset）。
    /// </summary>
    [Fact]
    public async Task CreateParcel_WithOffsetScannedTime_ShouldReturn400() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var body = BuildCreateRequestJson(
            scannedTime: "2026-03-20T10:00:00+08:00",
            dischargeTime: "2026-03-20T10:00:03");

        using var response = await client.PostAsync("/api/admin/parcels", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("本地时间格式", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：更新时传入带 offset 的 completedTime（+08:00），返回 400 Bad Request（字符串解析可严格拒绝 offset）。
    /// </summary>
    [Fact]
    public async Task UpdateParcelStatus_MarkCompleted_WithOffsetCompletedTime_ShouldReturn400() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var body = new StringContent(
            """{"operation":1,"completedTime":"2026-03-20T10:10:00+08:00"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PutAsync("/api/admin/parcels/1", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("本地时间格式", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：新增时 BarCodes 为空字符串，域层抛 ArgumentException，返回 400 Bad Request。
    /// </summary>
    [Fact]
    public async Task CreateParcel_WithEmptyBarCodes_ShouldReturn400() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var body = BuildCreateRequestJson(
            scannedTime: "2026-03-20T10:00:00",
            dischargeTime: "2026-03-20T10:00:03",
            barCodes: "");

        using var response = await client.PostAsync("/api/admin/parcels", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUT /api/admin/parcels/{id}
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 验证场景：UpdateRequestStatus 操作成功，返回 200 OK 及更新后详情。
    /// </summary>
    [Fact]
    public async Task UpdateParcelStatus_UpdateRequestStatus_ShouldReturn200() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        // Operation=3 = UpdateRequestStatus，RequestStatus=2 = Failed
        var body = new StringContent(
            """{"operation":3,"requestStatus":2}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PutAsync("/api/admin/parcels/1", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.RootElement.GetProperty("id").GetInt64());
    }

    /// <summary>
    /// 验证场景：MarkCompleted 操作成功，返回 200 OK。
    /// </summary>
    [Fact]
    public async Task UpdateParcelStatus_MarkCompleted_ShouldReturn200() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        // Operation=1 = MarkCompleted，completedTime 为本地时间
        var body = new StringContent(
            """{"operation":1,"completedTime":"2026-03-20T10:10:00"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PutAsync("/api/admin/parcels/1", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：目标包裹不存在，返回 404 Not Found。
    /// </summary>
    [Fact]
    public async Task UpdateParcelStatus_WhenNotFound_ShouldReturn404() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var body = new StringContent(
            """{"operation":3,"requestStatus":1}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PutAsync("/api/admin/parcels/999", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：Operation 无效值，返回 400 Bad Request。
    /// </summary>
    [Fact]
    public async Task UpdateParcelStatus_WithInvalidOperation_ShouldReturn400() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var body = new StringContent(
            """{"operation":99}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PutAsync("/api/admin/parcels/1", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("操作类型无效", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：MarkCompleted 操作但未提供 completedTime，返回 400 Bad Request。
    /// </summary>
    [Fact]
    public async Task UpdateParcelStatus_MarkCompleted_WithoutCompletedTime_ShouldReturn400() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var body = new StringContent(
            """{"operation":1}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PutAsync("/api/admin/parcels/1", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("completedTime", content, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // DELETE /api/admin/parcels/{id}
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 验证场景：删除存在的包裹成功，返回 204 No Content。
    /// </summary>
    [Fact]
    public async Task DeleteParcel_WhenExists_ShouldReturn204() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.DeleteAsync("/api/admin/parcels/1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：删除不存在的包裹，返回 404 Not Found。
    /// </summary>
    [Fact]
    public async Task DeleteParcel_WhenNotFound_ShouldReturn404() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.DeleteAsync("/api/admin/parcels/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // POST /api/admin/parcels/cleanup-expired（治理型接口三态测试）
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 验证场景：cleanup-expired - 守卫阻断（blocked），返回 200 + decision=blocked + executedCount=0。
    /// </summary>
    [Fact]
    public async Task CleanupExpired_WhenBlockedByGuard_ShouldReturnBlockedDecision() {
        var fakeRepo = new FakeParcelRepository {
            CleanupDecision = ActionIsolationDecision.BlockedByGuard,
            CleanupPlannedCount = 10,
            CleanupExecutedCount = 10
        };
        await using var app = await BuildTestAppAsync(fakeRepo);
        using var client = app.GetTestClient();

        var body = new StringContent(
            """{"createdBefore":"2026-01-01 00:00:00"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/admin/parcels/cleanup-expired", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ParcelCleanupExpiredResponse>();
        Assert.NotNull(payload);
        Assert.Equal("blocked", payload.Decision);
        Assert.True(payload.IsBlockedByGuard);
        Assert.False(payload.IsDryRun);
        Assert.Equal(0, payload.ExecutedCount);
        Assert.Equal(10, payload.PlannedCount);
    }

    /// <summary>
    /// 验证场景：cleanup-expired - 演练模式（dry-run），返回 200 + decision=dry-run + executedCount=0。
    /// </summary>
    [Fact]
    public async Task CleanupExpired_WhenDryRun_ShouldReturnDryRunDecision() {
        var fakeRepo = new FakeParcelRepository {
            CleanupDecision = ActionIsolationDecision.DryRunOnly,
            CleanupPlannedCount = 8
        };
        await using var app = await BuildTestAppAsync(fakeRepo);
        using var client = app.GetTestClient();

        var body = new StringContent(
            """{"createdBefore":"2026-01-01 00:00:00"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/admin/parcels/cleanup-expired", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ParcelCleanupExpiredResponse>();
        Assert.NotNull(payload);
        Assert.Equal("dry-run", payload.Decision);
        Assert.True(payload.IsDryRun);
        Assert.False(payload.IsBlockedByGuard);
        Assert.Equal(0, payload.ExecutedCount);
        Assert.Equal(8, payload.PlannedCount);
    }

    /// <summary>
    /// 验证场景：cleanup-expired - 正常执行（execute），返回 200 + decision=execute + executedCount 有值。
    /// </summary>
    [Fact]
    public async Task CleanupExpired_WhenExecute_ShouldReturnExecuteDecision() {
        var fakeRepo = new FakeParcelRepository {
            CleanupDecision = ActionIsolationDecision.Execute,
            CleanupPlannedCount = 6,
            CleanupExecutedCount = 6
        };
        await using var app = await BuildTestAppAsync(fakeRepo);
        using var client = app.GetTestClient();

        var body = new StringContent(
            """{"createdBefore":"2026-01-01 00:00:00"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/admin/parcels/cleanup-expired", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ParcelCleanupExpiredResponse>();
        Assert.NotNull(payload);
        Assert.Equal("execute", payload.Decision);
        Assert.False(payload.IsDryRun);
        Assert.False(payload.IsBlockedByGuard);
        Assert.Equal(6, payload.ExecutedCount);
        Assert.Equal(6, payload.PlannedCount);
        Assert.NotEmpty(payload.CompensationBoundary);
    }

    /// <summary>
    /// 验证场景：cleanup-expired 传入 UTC 时间字符串（含 Z），返回 400 Bad Request。
    /// </summary>
    [Fact]
    public async Task CleanupExpired_WithUtcCreatedBefore_ShouldReturn400() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var body = new StringContent(
            """{"createdBefore":"2026-01-01T00:00:00Z"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/admin/parcels/cleanup-expired", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("本地时间格式", content, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：cleanup-expired 传入非法时间字符串，返回 400 Bad Request。
    /// </summary>
    [Fact]
    public async Task CleanupExpired_WithInvalidCreatedBefore_ShouldReturn400() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        var body = new StringContent(
            """{"createdBefore":"not-a-date"}""",
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync("/api/admin/parcels/cleanup-expired", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 测试基础设施
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 构建管理端测试用 WebApplication（使用默认 FakeParcelRepository）。
    /// </summary>
    /// <returns>已启动的测试应用。</returns>
    private static Task<WebApplication> BuildTestAppAsync() {
        return BuildTestAppAsync(new FakeParcelRepository());
    }

    /// <summary>
    /// 构建管理端测试用 WebApplication（使用指定 FakeParcelRepository 以控制行为）。
    /// </summary>
    /// <param name="fakeRepo">预配置的测试替身（可控制 CleanupDecision 等属性）。</param>
    /// <returns>已启动的测试应用。</returns>
    private static async Task<WebApplication> BuildTestAppAsync(FakeParcelRepository fakeRepo) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        // 注册预配置的测试替身（单例工厂，确保同一请求使用同一实例）
        builder.Services.AddScoped<IParcelRepository>(_ => fakeRepo);
        builder.Services.AddScoped<CreateParcelCommandService>();
        builder.Services.AddScoped<UpdateParcelStatusCommandService>();
        builder.Services.AddScoped<DeleteParcelCommandService>();
        builder.Services.AddScoped<CleanupExpiredParcelsCommandService>();

        var app = builder.Build();
        app.MapParcelAdminApis();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 构建新增 Parcel 的 JSON 请求体。
    /// </summary>
    /// <param name="scannedTime">扫码时间字符串（本地时间或 UTC）。</param>
    /// <param name="dischargeTime">落格时间字符串（本地时间）。</param>
    /// <param name="barCodes">条码（默认 BC-ADMIN-TEST）。</param>
    /// <returns>HTTP JSON StringContent。</returns>
    private static StringContent BuildCreateRequestJson(
        string scannedTime,
        string dischargeTime,
        string barCodes = "BC-ADMIN-TEST") {
        var json = $$"""
            {
                "parcelTimestamp": 638789040000000000,
                "type": 0,
                "barCodes": "{{barCodes}}",
                "weight": 1.5,
                "workstationName": "WS-ADMIN",
                "scannedTime": "{{scannedTime}}",
                "dischargeTime": "{{dischargeTime}}",
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
            """;
        return new StringContent(json, Encoding.UTF8, "application/json");
    }
}
