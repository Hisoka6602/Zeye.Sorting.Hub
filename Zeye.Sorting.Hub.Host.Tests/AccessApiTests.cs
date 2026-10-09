using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zeye.Sorting.Hub.Host.Authentication;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Host.Routing;
namespace Zeye.Sorting.Hub.Host.Tests;
/// <summary>真实密码认证、权限保护、会话失效及账号目录持久化的回归测试。</summary>
public sealed class AccessApiTests {
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedParcelsReadParcelsWriteValues = new[] { "parcels.read", "parcels.write" };
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedParcelsReadValues = new[] { "parcels.read" };
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedInvalidValues = new[] { "invalid" };

    /// <summary>普通角色即使拥有全部权限或同名角色，也不能访问三个敏感版块。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SensitiveSectionsRequireTheFixedSuperAdministratorRole(bool enforceAuthorization) {
        string[] readPaths = ["/api/audit/web-requests", "/api/audit/web-requests/123", "/api/diagnostics/slow-queries",
            "/api/diagnostics/slow-queries/test", "/api/diagnostics/fusion/facts", "/api/data-governance/archive-tasks", "/api/operations/partitions",
            "/api/admin/parcels/cleanup-history", "/api/admin/parcels/cleanup-history/test", "/health/deep"];
        string[] writePaths = ["/api/data-governance/archive-tasks", "/api/data-governance/archive-tasks/123/retry",
            "/api/operations/partitions/prebuild", "/api/admin/parcels/cleanup-expired"];
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db, enforceAuthorization, configureRoutes: routes => {
            foreach (var path in readPaths) routes.MapGet(path, () => Results.Ok());
            foreach (var path in writePaths) routes.MapPost(path, () => Results.Ok());
            routes.MapGet("/health/live", () => Results.Ok());
        });
        using var admin = app.GetTestClient(); using var ordinary = app.GetTestClient(); using var anonymous = app.GetTestClient(); using var builtIn = app.GetTestClient();
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); ordinary.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); builtIn.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        UseCookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "超级管理员", permissions = AccessDirectoryService.PermissionCodes })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "ordinary", name = "普通角色", password = "test-ordinary-password", roleId = 2 })).StatusCode);
        UseCookie(ordinary, await ordinary.PostAsJsonAsync("/api/access/login", new { username = "ordinary", password = "test-ordinary-password" }));
        UseCookie(builtIn, await builtIn.PostAsJsonAsync("/api/access/login", new { username = "hisoka", password = "15876396602" }));
        Assert.True((await admin.GetFromJsonAsync<JsonElement>("/api/access/session")).GetProperty("isSuperAdministrator").GetBoolean());
        Assert.True((await builtIn.GetFromJsonAsync<JsonElement>("/api/access/session")).GetProperty("isSuperAdministrator").GetBoolean());
        Assert.False((await ordinary.GetFromJsonAsync<JsonElement>("/api/access/session")).GetProperty("isSuperAdministrator").GetBoolean());
        Assert.False((await anonymous.GetFromJsonAsync<JsonElement>("/api/access/session")).GetProperty("isSuperAdministrator").GetBoolean());
        foreach (var path in readPaths) {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(path)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await builtIn.GetAsync(path)).StatusCode);
        }
        foreach (var path in writePaths.Concat(["/api/admin/parcels", "/api/admin/parcels/batch-buffer", "/api/admin/parcels/processing-records"])) {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(path, new { })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync(path, new { })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(path, new { })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await builtIn.PostAsJsonAsync(path, new { })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync("/API/AUDIT/WEB-REQUESTS/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ordinary.GetAsync("/api/parcels/test")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/ready")).StatusCode);
    }
    /// <summary>账号管理权限不能自行晋升或接管超级管理员，但仍可维护普通成员。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DelegatedAccountManagerCannotPromoteOrTakeOverSuperAdministrators(bool enforceAuthorization) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db, enforceAuthorization); using var admin = app.GetTestClient(); using var manager = app.GetTestClient();
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); manager.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        UseCookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "账号维护", permissions = AccessDirectoryService.PermissionCodes });
        await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "manager", name = "账号维护员", password = "test-manager-password", roleId = 2 });
        UseCookie(manager, await manager.PostAsJsonAsync("/api/access/login", new { username = "manager", password = "test-manager-password" }));
        var directory = await manager.GetFromJsonAsync<JsonElement>("/api/access");
        var revision = directory.GetProperty("revision").GetInt32();
        var managerId = directory.GetProperty("users").EnumerateArray().Single(x => x.GetProperty("account").GetString() == "manager").GetProperty("id").GetString();
        var adminId = directory.GetProperty("users").EnumerateArray().Single(x => x.GetProperty("account").GetString() == "admin").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/api/access/users", new { id = managerId, expectedRevision = revision, account = "manager", name = "自行晋升", roleId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/api/access/users", new { expectedRevision = revision, account = "promoted", name = "越权创建", roleId = 1, password = "test-promoted-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync("/api/access/users", new { id = adminId, expectedRevision = revision, account = "admin", name = "接管管理员", roleId = 2, password = "test-takeover-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PostAsJsonAsync("/api/access/users", new { expectedRevision = revision, account = "normal", name = "普通成员", roleId = 2, password = "test-normal-password" })).StatusCode);
        directory = await admin.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/users", new { id = managerId, expectedRevision = directory.GetProperty("revision").GetInt32(), account = "manager", name = "授权管理员", roleId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await manager.GetAsync("/api/diagnostics/slow-queries")).StatusCode);
        UseCookie(manager, await manager.PostAsJsonAsync("/api/access/login", new { username = "manager", password = "test-manager-password" }));
        Assert.True((await manager.GetFromJsonAsync<JsonElement>("/api/access/session")).GetProperty("isSuperAdministrator").GetBoolean());
    }
    /// <summary>手工新增和批量入队始终仅供管理员测试，来源处理接口继续允许机器身份。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ManualParcelCreationRequiresAdminEvenWhenGeneralAuthorizationIsDisabled(bool enforceAuthorization) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db, enforceAuthorization);
        using var admin = app.GetTestClient(); using var writer = app.GetTestClient(); using var machine = app.GetTestClient();
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); writer.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        machine.DefaultRequestHeaders.Add("X-Sorting-Api-Key", "test-machine-key");
        UseCookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "包裹业务员", permissions = CachedParcelsReadParcelsWriteValues })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "writer", name = "包裹业务员", password = "test-writer-password", roleId = 2, enabled = true })).StatusCode);
        UseCookie(writer, await writer.PostAsJsonAsync("/api/access/login", new { username = "writer", password = "test-writer-password" }));
        foreach (var path in new[] { "/api/admin/parcels", "/api/admin/parcels/batch-buffer", "/API/ADMIN/PARCELS/" }) {
            Assert.Equal(HttpStatusCode.Unauthorized, (await machine.PostAsJsonAsync(path, new { })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await writer.PostAsJsonAsync(path, new { })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(path, new { })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await machine.PostAsJsonAsync("/api/admin/parcels/processing-records", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await writer.GetAsync("/api/parcels/42/images")).StatusCode);
    }
    /// <summary>已登录浏览器访问公开探针时，不因账号数据库故障失去健康诊断。</summary>
    [Fact]
    public async Task HealthProbeWithCookieDoesNotDependOnAccountDatabase() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db); using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        UseCookie(client, await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        await db.DisposeAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }
    /// <summary>首个管理员需要密钥，密码散列不出现在公开目录，旧版本不能覆盖。</summary>
    [Fact]
    public async Task BootstrapLoginAndDirectoryUseRealAuthentication() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db); using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/operations/rules/exception")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "wrong" })).StatusCode);
        var bootstrap = await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" });
        Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode); UseCookie(client, bootstrap);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "other", name = "其他", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" })).StatusCode);
        var directory = await client.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.Equal("admin", Assert.Single(directory.GetProperty("users").EnumerateArray()).GetProperty("account").GetString());
        Assert.Equal(1, Assert.Single(directory.GetProperty("roles").EnumerateArray()).GetProperty("members").GetInt32());
        Assert.DoesNotContain(BuiltInSuperUser.Id, directory.GetRawText());
        Assert.DoesNotContain("passwordHash", directory.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-admin-password", directory.GetRawText(), StringComparison.Ordinal);
        var stored = await new ManagedDocumentService(db.Factory).ReadAsync("access-directory", default);
        Assert.DoesNotContain("test-admin-password", stored!.Json); Assert.Contains("passwordHash", stored.Json);
        var role = await client.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "查询员", description = "只读权限", permissions = CachedParcelsReadValues });
        Assert.Equal(HttpStatusCode.OK, role.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "过时", permissions = CachedParcelsReadValues })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/operations/rules/exception")).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Zeye-Client");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/access/logout", new { })).StatusCode);
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/access/logout", new { })).StatusCode);
        client.DefaultRequestHeaders.Remove("Cookie");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/access/login", new { username = "admin", password = "incorrect" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/access/login", CachedInvalidValues)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/access/login", new { username = "admin", password = "test-admin-password" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("httponly", login.Headers.GetValues("Set-Cookie").Single(), StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>普通账号权限不能越界；停用账号立即失效，内置身份始终保留管理员权限。</summary>
    [Fact]
    public async Task RolePermissionsAndAccountDisableAreEnforced() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db); using var admin = app.GetTestClient(); using var reader = app.GetTestClient();
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); reader.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        UseCookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "查询员", permissions = CachedParcelsReadValues });
        var created = await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "reader", name = "查询员", password = "test-reader-password", roleId = 2, enabled = true });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        UseCookie(reader, await reader.PostAsJsonAsync("/api/access/login", new { username = "reader", password = "test-reader-password" }));
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/parcels/test")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/operations/rules/exception")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/access")).StatusCode);
        var directory = await admin.GetFromJsonAsync<JsonElement>("/api/access");
        var users = directory.GetProperty("users").EnumerateArray().ToArray();
        var userId = users.Single(x => x.GetProperty("account").GetString() == "reader").GetProperty("id").GetString();
        var disabled = await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = directory.GetProperty("revision").GetInt32(), id = userId, account = "reader", name = "查询员", roleId = 2, enabled = false });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.GetAsync("/api/parcels/test")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.PostAsJsonAsync("/api/access/login", new { username = "reader", password = "test-reader-password" })).StatusCode);
        directory = await admin.GetFromJsonAsync<JsonElement>("/api/access");
        var adminId = directory.GetProperty("users").EnumerateArray().Single(x => x.GetProperty("account").GetString() == "admin").GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = directory.GetProperty("revision").GetInt32(), id = adminId, account = "admin", name = "管理员", roleId = 1, enabled = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/access")).StatusCode);
        using var machine = app.GetTestClient(); machine.DefaultRequestHeaders.Add("X-Sorting-Api-Key", "test-machine-key");
        Assert.Equal(HttpStatusCode.OK, (await machine.PostAsJsonAsync("/api/admin/parcels/processing-records", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await machine.GetAsync("/api/parcels/test")).StatusCode);
    }
    /// <summary>普通用户可以更新自己的资料和头像，名称同步到会话和账号目录，版本冲突不覆盖。</summary>
    [Fact]
    public async Task PersonalProfilePersistsForOrdinaryUsersWithoutChangingPermissions() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db); using var admin = app.GetTestClient(); using var reader = app.GetTestClient();
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); reader.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        UseCookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = 1, name = "查询员", permissions = CachedParcelsReadValues });
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = 2, account = "reader", name = "查询员", password = "test-reader-password", roleId = 2 })).StatusCode);
        UseCookie(reader, await reader.PostAsJsonAsync("/api/access/login", new { username = "reader", password = "test-reader-password" }));
        var original = await reader.GetFromJsonAsync<JsonElement>("/api/access/profile");
        const string avatar = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aZxkAAAAASUVORK5CYII=";
        var change = new { name = " 新名称 ", email = "reader@example.test", phone = "+86 13800000000", bio = "测试个人简介", avatarDataUrl = avatar,
            expectedRevision = original.GetProperty("revision").GetInt32(), directoryRevision = original.GetProperty("directoryRevision").GetInt32() };
        var saved = await reader.PutAsJsonAsync("/api/access/profile", change);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await reader.PutAsJsonAsync("/api/access/profile", change)).StatusCode);
        var reloaded = await reader.GetFromJsonAsync<JsonElement>("/api/access/profile");
        Assert.Equal("新名称", reloaded.GetProperty("name").GetString()); Assert.Equal("reader@example.test", reloaded.GetProperty("email").GetString());
        Assert.Equal("测试个人简介", reloaded.GetProperty("bio").GetString()); Assert.Equal("查询员", reloaded.GetProperty("roleName").GetString());
        Assert.DoesNotContain("passwordHash", reloaded.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var session = await reader.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.Equal("新名称", session.GetProperty("name").GetString()); Assert.Equal("parcels.read", session.GetProperty("permissions").EnumerateArray().Single().GetString());
        Assert.Equal(reloaded.GetProperty("avatarUrl").GetString(), session.GetProperty("avatarUrl").GetString());
        var image = await reader.GetAsync(session.GetProperty("avatarUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, image.StatusCode); Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Convert.FromBase64String(avatar.Split(',')[1]), await image.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/access/profile/avatar")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/access")).StatusCode);
        var directory = await admin.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.Equal("新名称", directory.GetProperty("users").EnumerateArray().Single(x => x.GetProperty("account").GetString() == "reader").GetProperty("name").GetString());
        var storedProfile = await new ManagedDocumentService(db.Factory).ReadAsync(PersonalProfile.Key(reloaded.GetProperty("id").GetString()!), default);
        Assert.Contains("reader@example.test", storedProfile!.Json);
        var storedDirectory = await new ManagedDocumentService(db.Factory).ReadAsync("access-directory", default);
        Assert.DoesNotContain(avatar, storedDirectory!.Json);
        Assert.Equal(HttpStatusCode.OK, (await reader.PutAsJsonAsync("/api/access/profile", new { name = "新名称", email = "", phone = "", bio = "", avatarDataUrl = "",
            expectedRevision = reloaded.GetProperty("revision").GetInt32(), directoryRevision = reloaded.GetProperty("directoryRevision").GetInt32() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync("/api/access/profile/avatar")).StatusCode);
    }
    /// <summary>资料接口验证输入和来源；即使关闭全平台权限，也必须登录后才能使用。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PersonalProfileRejectsAnonymousInvalidAndPrivilegeChangingRequests(bool enforceAuthorization) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db, enforceAuthorization); using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/access/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/access/profile/avatar")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/access/profile", new { })).StatusCode);
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        UseCookie(client, await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        var body = new Dictionary<string, object> { ["name"] = "名称", ["email"] = "", ["phone"] = "", ["bio"] = "", ["expectedRevision"] = 0, ["directoryRevision"] = 1 };
        foreach (var (field, value) in new (string, object)[] { ("name", "  "), ("name", new string('名', 101)), ("email", "invalid-address"), ("phone", "not-a-phone"), ("bio", new string('字', 501)),
            ("expectedRevision", "invalid"), ("avatarDataUrl", "https://example.test/avatar.png"), ("avatarDataUrl", "data:image/svg+xml;base64,PHN2Zy8+"), ("avatarDataUrl", "data:image/png;base64,invalid"),
            ("avatarDataUrl", "data:image/png;base64," + new string('A', 131105)), ("roleId", 2), ("id", "another-user"), ("enabled", false), ("password", "new-test-password") }) {
            var invalid = new Dictionary<string, object>(body) { [field] = value };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/access/profile", invalid)).StatusCode);
        }
        client.DefaultRequestHeaders.Remove("X-Zeye-Client");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync("/api/access/profile", body)).StatusCode);
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/access/profile");
        Assert.Equal("管理员", profile.GetProperty("name").GetString()); Assert.Equal(0, profile.GetProperty("revision").GetInt32());
    }
    /// <summary>多个文档的版本检查必须在写入前完成，不能留下部分修改。</summary>
    [Fact]
    public async Task ProfileBatchRejectsConflictWithoutPartiallyUpdatingDirectory() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var store = new ManagedDocumentService(db.Factory);
        await store.WriteAsync("access-directory", "{\"name\":\"original\"}", 0, default);
        await store.WriteAsync("access-profile:test", "{}", 0, default);
        Assert.False(await store.WriteBatchAsync([("access-directory", "{\"name\":\"changed\"}", 1), ("access-profile:test", "{}", 0)], default));
        var directory = await store.ReadAsync("access-directory", default);
        Assert.Equal(1, directory!.Revision); Assert.Contains("original", directory.Json);
    }
    /// <summary>从响应提取测试会话，模拟浏览器的 HttpOnly Cookie 发送。</summary>
    internal static void UseCookie(HttpClient client, HttpResponseMessage response) {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie").Last(value => value.StartsWith("Zeye.Sorting.Session=", StringComparison.Ordinal)).Split(';')[0]);
    }
    /// <summary>使用同一生产认证管线及数据库的隔离宿主。</summary>
    internal static async Task<WebApplication> CreateAsync(RelationalParcelTestDatabase db, bool enforceAuthorization = true,
        Action<WebApplicationBuilder>? configureServices = null, Action<WebApplication>? configureRoutes = null) {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Access:EnforceAuthorization"] = enforceAuthorization.ToString(), ["Access:BootstrapKey"] = "test-bootstrap-key", ["Access:MachineApiKey"] = "test-machine-key" });
        builder.Services.AddSingleton(db.Factory); builder.Services.AddScoped<ManagedDocumentService>();
        builder.Services.AddSortingHubAccess(Path.Combine(Path.GetTempPath(), "zeye-access-tests")); builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthorization(); builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("account-login", x => { x.PermitLimit = 100; x.Window = TimeSpan.FromMinutes(1); }));
        configureServices?.Invoke(builder);
        var app = builder.Build(); app.UseRouting(); app.UseRateLimiter(); app.UseAuthentication(); app.UseAuthorization(); app.UseSortingHubAccess();
        app.MapAccessApis(); app.MapRuleManagementApis(); app.MapGet("/api/parcels/test", () => Results.Ok()); app.MapPost("/api/admin/parcels/processing-records", () => Results.Ok());
        app.MapPost("/api/admin/parcels", () => Results.Ok()); app.MapPost("/api/admin/parcels/batch-buffer", () => Results.Ok());
        app.MapGet("/api/parcels/{id:long}/images", () => Results.Ok());
        app.MapGet("/health/ready", () => Results.Ok(new { status = "Healthy" }));
        configureRoutes?.Invoke(app);
        await app.StartAsync(); return app;
    }
}
