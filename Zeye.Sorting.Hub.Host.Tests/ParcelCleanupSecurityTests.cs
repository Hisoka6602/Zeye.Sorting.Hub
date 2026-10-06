using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zeye.Sorting.Hub.Application.Services.Idempotency;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Host.Authentication;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Idempotency;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Management;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>对真实密码、权限、数据库清理与永久查询使用完整会话管线验证。</summary>
public sealed class ParcelCleanupSecurityTests {
    /// <summary>关闭通用鉴权也不能绕过超级管理员身份和密码；普通审计权限不能读取删除记录。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CleanupRequiresCurrentUserPasswordAndGovernancePermission(bool enforceAuthorization) {
        await using var db = new RelationalParcelTestDatabase("PerDay"); await db.InitializeAsync();
        await db.Processing.AppendAsync(ParcelCleanupAuditTests.Fact(1, new(2026, 9, 28)), default);
        await using var app = await CreateAsync(db, enforceAuthorization);
        using var anonymous = app.GetTestClient(); using var admin = app.GetTestClient(); using var writer = app.GetTestClient();
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); writer.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        var body = new { createdBefore = "2026-10-01T00:00:00", password = "test-admin-password" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/parcels/cleanup-history")).StatusCode);
        Cookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "清理管理员", password = body.password, bootstrapKey = "test-bootstrap-key" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", new { body.createdBefore })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", body with { password = "wrong-password" })).StatusCode);
        admin.DefaultRequestHeaders.Remove("X-Zeye-Client");
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", body)).StatusCode);
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "业务员", permissions = new[] { "parcels.write", "audit.read" } });
        await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "writer", name = "业务员", password = "test-writer-password", roleId = 2 });
        Cookie(writer, await writer.PostAsJsonAsync("/api/access/login", new { username = "writer", password = "test-writer-password" }));
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", body)).StatusCode);
        Assert.Equal(1, await db.CountPhysicalAsync("Parcels_20260928"));
        var result = await admin.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", body);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var response = await result.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("execute", response.GetProperty("decision").GetString()); Assert.Equal(1, response.GetProperty("executedCount").GetInt32());
        var id = response.GetProperty("cleanupRecordId").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.GetAsync("/api/admin/parcels/cleanup-history/" + id)).StatusCode);
        var detail = await admin.GetFromJsonAsync<JsonElement>("/api/admin/parcels/cleanup-history/" + id);
        Assert.Equal("清理管理员", detail.GetProperty("record").GetProperty("operator").GetProperty("name").GetString());
        Assert.Equal("admin", detail.GetProperty("record").GetProperty("operator").GetProperty("account").GetString());
        Assert.False(detail.TryGetProperty("items", out _));
        Assert.Equal(1, detail.GetProperty("record").GetProperty("executedCount").GetInt32());
        Assert.DoesNotContain(body.password, detail.GetRawText());
        Assert.Equal(0, await db.CountPhysicalAsync("Parcels_20260928"));
        await using var context = await db.Factory.CreateDbContextAsync();
        var permanent = await context.Set<ManagedDocument>().Where(x => x.Key.StartsWith(ParcelCleanupAudit.Prefix) || x.Key.StartsWith("parcel-cleanup-batch:")).ToListAsync();
        Assert.Equal(2, permanent.Count); Assert.All(permanent, x => Assert.DoesNotContain(body.password, x.Json));
        Assert.Equal(HttpStatusCode.Forbidden, (await writer.GetAsync("/api/admin/parcels/cleanup-history?pageSize=999999")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/admin/parcels/cleanup-history?pageSize=999999")).StatusCode);
    }
    /// <summary>密码确认的尝试也受限流保护。</summary>
    [Fact]
    public async Task CleanupPasswordAttemptsAreRateLimited() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db, true, 1);
        using var client = app.GetTestClient();
        await client.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", new { createdBefore = "2026-10-01", password = "incorrect" });
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", new { createdBefore = "2026-10-01", password = "incorrect" })).StatusCode);
    }
    /// <summary>内置超级用户使用固定口令确认清理，永久记录绑定它的独立程序身份。</summary>
    [Fact]
    public async Task BuiltInSuperUserCanConfirmCleanupWithItsOwnPassword() {
        await using var db = new RelationalParcelTestDatabase("PerDay"); await db.InitializeAsync();
        await db.Processing.AppendAsync(ParcelCleanupAuditTests.Fact(1, new(2026, 9, 28)), default);
        await using var app = await CreateAsync(db, true); using var admin = app.GetTestClient(); using var builtIn = app.GetTestClient();
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); builtIn.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        Cookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        const string builtInPassword = "15876396602";
        Cookie(builtIn, await builtIn.PostAsJsonAsync("/api/access/login", new { username = "hisoka", password = builtInPassword }));
        var request = new { createdBefore = "2026-10-01T00:00:00", password = "test-admin-password" };
        Assert.Equal(HttpStatusCode.BadRequest, (await builtIn.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", request)).StatusCode);
        Assert.Equal(1, await db.CountPhysicalAsync("Parcels_20260928"));
        var response = await builtIn.PostAsJsonAsync("/api/admin/parcels/cleanup-expired", request with { password = builtInPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("executedCount").GetInt32());
        var detail = await builtIn.GetFromJsonAsync<JsonElement>("/api/admin/parcels/cleanup-history/" + result.GetProperty("cleanupRecordId").GetString());
        var actor = detail.GetProperty("record").GetProperty("operator");
        Assert.Equal(BuiltInSuperUser.Id, actor.GetProperty("userId").GetString()); Assert.Equal("hisoka", actor.GetProperty("account").GetString());
        Assert.DoesNotContain(builtInPassword, detail.GetRawText()); Assert.Equal(0, await db.CountPhysicalAsync("Parcels_20260928"));
    }
    /// <summary>测试会话 Cookie 只在隔离测试中传递。</summary>
    private static void Cookie(HttpClient client, HttpResponseMessage response) {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
    }
    /// <summary>建立包含生产认证与仓储的隔离关系数据库宿主。</summary>
    private static async Task<WebApplication> CreateAsync(RelationalParcelTestDatabase db, bool enforceAuthorization, int limit = 100) {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Access:EnforceAuthorization"] = enforceAuthorization.ToString(), ["Access:BootstrapKey"] = "test-bootstrap-key" });
        builder.Services.AddSingleton(db.Factory); builder.Services.AddSingleton<IParcelRepository>(db.Parcels);
        builder.Services.AddScoped<ManagedDocumentService>(); builder.Services.AddScoped<ParcelCleanupHistoryService>();
        builder.Services.AddSortingHubAccess(Path.Combine(Path.GetTempPath(), "zeye-cleanup-tests")); builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthorization(); builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddFixedWindowLimiter("account-login", x => { x.PermitLimit = limit; x.Window = TimeSpan.FromMinutes(1); }); });
        builder.Services.AddScoped<IIdempotencyRepository, IdempotencyRepository>(); builder.Services.AddScoped<IdempotencyGuardService>(); builder.Services.AddSingleton<IdempotencyKeyHasher>();
        builder.Services.AddScoped<CreateParcelCommandService>(); builder.Services.AddScoped<UpdateParcelStatusCommandService>(); builder.Services.AddScoped<DeleteParcelCommandService>(); builder.Services.AddScoped<CleanupExpiredParcelsCommandService>();
        var app = builder.Build(); app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization(); app.UseSortingHubAccess();
        app.MapAccessApis(); app.MapParcelAdminApis(); await app.StartAsync(); return app;
    }
}
