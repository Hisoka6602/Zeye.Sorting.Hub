using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Host.Queries;
namespace Zeye.Sorting.Hub.Host.Tests;
/// <summary>内置超级用户、首次初始化、保留名冲突处理及会话保护的端到端回归。</summary>
public sealed class BuiltInSuperUserTests {
    /// <summary>用户指定的内置登录口令，仅用于隔离认证测试。</summary>
    private const string BuiltInPassword = "15876396602";
    /// <summary>内置身份不能跳过首次初始化；初始化后可用固定口令登录并获得全部权限。</summary>
    [Fact]
    public async Task BuiltInLoginRequiresBootstrapAndHasAllPermissions() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await AccessApiTests.CreateAsync(db);
        using var admin = app.GetTestClient(); using var builtIn = app.GetTestClient();
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); builtIn.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        var initial = await builtIn.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.False(initial.GetProperty("configured").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await builtIn.PostAsJsonAsync("/api/access/login", new { username = "hisoka", password = BuiltInPassword })).StatusCode);
        var store = new ManagedDocumentService(db.Factory);
        Assert.Null(await store.ReadAsync("access-directory", default));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = BuiltInPassword, bootstrapKey = "test-bootstrap-key" })).StatusCode);
        AccessApiTests.UseCookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await builtIn.PostAsJsonAsync("/api/access/login", new { username = "hisoka", password = "wrong-password" })).StatusCode);
        AccessApiTests.UseCookie(builtIn, await builtIn.PostAsJsonAsync("/api/access/login", new { username = " HiSoKa ", password = BuiltInPassword }));
        var session = await builtIn.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.True(session.GetProperty("configured").GetBoolean()); Assert.True(session.GetProperty("authenticated").GetBoolean());
        Assert.Equal(AccessDirectoryService.PermissionCodes.Order(), session.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).Order());
        foreach (var path in new[] { "/api/access", "/api/parcels/test", "/api/operations/rules/exception", "/api/access/profile" })
            Assert.Equal(HttpStatusCode.OK, (await builtIn.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await builtIn.PostAsJsonAsync("/api/admin/parcels", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await builtIn.PostAsJsonAsync("/api/access/bootstrap", new { username = "other", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" })).StatusCode);
        var directory = await builtIn.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.DoesNotContain(BuiltInPassword, directory.GetRawText()); Assert.DoesNotContain("passwordHash", directory.GetRawText()); Assert.DoesNotContain("securityStamp", directory.GetRawText());
        Assert.DoesNotContain(BuiltInPassword, (await store.ReadAsync("access-directory", default))!.Json);
    }

    /// <summary>仅内置账号或空目录重新提供管理员创建，仍要求密钥且保留内置身份资料。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NoManagedMembersReopensBootstrapWithoutRemovingBuiltInIdentity(bool includeBuiltIn) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var store = new ManagedDocumentService(db.Factory);
        var config = new ConfigurationBuilder().Build();
        var service = new AccessDirectoryService(store, config);
        var builtIn = BuiltInSuperUser.Restore(null) with { Name = "内置身份资料" };
        Assert.True(await service.SaveAsync(new AccessDirectory { Initialized = true, Users = includeBuiltIn ? [builtIn] : [] }, 0, default));
        var (before, _) = await service.ReadAsync(default);
        Assert.True(before.Initialized); Assert.False(before.HasManagedUsers);
        await using var app = await AccessApiTests.CreateAsync(db);
        using var builtinClient = app.GetTestClient(); using var recovery = app.GetTestClient();
        builtinClient.DefaultRequestHeaders.Add("X-Zeye-Client", "web"); recovery.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        var initial = await recovery.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.False(initial.GetProperty("configured").GetBoolean()); Assert.True(initial.GetProperty("bootstrapAvailable").GetBoolean());
        AccessApiTests.UseCookie(builtinClient, await builtinClient.PostAsJsonAsync("/api/access/login", new { username = "hisoka", password = BuiltInPassword }));
        var hidden = await builtinClient.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.False(hidden.GetProperty("configured").GetBoolean()); Assert.Empty(hidden.GetProperty("users").EnumerateArray());
        Assert.All(hidden.GetProperty("roles").EnumerateArray(), role => Assert.Equal(0, role.GetProperty("members").GetInt32()));
        Assert.DoesNotContain(BuiltInSuperUser.Id, hidden.GetRawText());
        Assert.Equal(HttpStatusCode.Forbidden, (await recovery.PostAsJsonAsync("/api/access/bootstrap", new { username = "recovered-admin", name = "恢复管理员", password = "test-recovered-password", bootstrapKey = "wrong-key" })).StatusCode);
        Assert.False((await recovery.GetFromJsonAsync<JsonElement>("/api/access/session")).GetProperty("configured").GetBoolean());
        AccessApiTests.UseCookie(recovery, await recovery.PostAsJsonAsync("/api/access/bootstrap", new { username = "recovered-admin", name = "恢复管理员", password = "test-recovered-password", bootstrapKey = "test-bootstrap-key" }));
        var session = await recovery.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.True(session.GetProperty("configured").GetBoolean()); Assert.True(session.GetProperty("authenticated").GetBoolean());
        Assert.Equal(AccessDirectoryService.PermissionCodes.Order(), session.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()!).Order());
        var visible = await recovery.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.Equal("recovered-admin", Assert.Single(visible.GetProperty("users").EnumerateArray()).GetProperty("account").GetString());
        Assert.Equal(1, Assert.Single(visible.GetProperty("roles").EnumerateArray()).GetProperty("members").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await recovery.PostAsJsonAsync("/api/access/bootstrap", new { username = "duplicate-admin", name = "重复管理员", password = "test-recovered-password", bootstrapKey = "test-bootstrap-key" })).StatusCode);
        Assert.True((await builtinClient.GetFromJsonAsync<JsonElement>("/api/access/session")).GetProperty("authenticated").GetBoolean());
        var (saved, _) = await service.ReadAsync(default);
        Assert.True(saved.HasManagedUsers); Assert.Equal(2, saved.Users.Length);
        Assert.Equal(before.Users.Single().Name, Assert.Single(saved.Users.Where(BuiltInSuperUser.Is)).Name);
        Assert.DoesNotContain("hasManagedUsers", (await store.ReadAsync("access-directory", default))!.Json);
    }

    /// <summary>普通成员存在时不能重新初始化，成员停用也不开放管理员创建入口。</summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task ExistingManagedMemberKeepsBootstrapClosed(bool memberEnabled, bool enforceAuthorization) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var service = new AccessDirectoryService(new ManagedDocumentService(db.Factory), new ConfigurationBuilder().Build());
        var reader = AccessDirectoryService.CreateUser("reader", "普通成员", 2, "test-reader-password") with { Enabled = memberEnabled };
        Assert.True(await service.SaveAsync(new AccessDirectory { Initialized = true, Users = [reader], Roles = [new AccessRole { Id = 2, Name = "查询员", Permissions = ["parcels.read"] }] }, 0, default));
        await using var app = await AccessApiTests.CreateAsync(db, enforceAuthorization); using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/access/session")).GetProperty("configured").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "other-admin", name = "重复管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" })).StatusCode);
        AccessApiTests.UseCookie(client, await client.PostAsJsonAsync("/api/access/login", new { username = "hisoka", password = BuiltInPassword }));
        var directory = await client.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.Equal(reader.Id, Assert.Single(directory.GetProperty("users").EnumerateArray()).GetProperty("id").GetString());
        Assert.All(directory.GetProperty("roles").EnumerateArray(), role => Assert.Equal(role.GetProperty("id").GetInt64() == 2 ? 1 : 0, role.GetProperty("members").GetInt32()));
    }

    /// <summary>重新创建管理员的并发请求只能成功一次，避免同一入口创建多个管理员。</summary>
    [Fact]
    public async Task ReopenedBootstrapAllowsOnlyOneConcurrentCreation() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var service = new AccessDirectoryService(new ManagedDocumentService(db.Factory), new ConfigurationBuilder().Build());
        Assert.True(await service.SaveAsync(new AccessDirectory { Initialized = true, Users = [BuiltInSuperUser.Restore(null)] }, 0, default));
        await using var app = await AccessApiTests.CreateAsync(db); using var first = app.GetTestClient(); using var second = app.GetTestClient();
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync("/api/access/bootstrap", new { username = "first-admin", name = "管理员甲", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }),
            second.PostAsJsonAsync("/api/access/bootstrap", new { username = "second-admin", name = "管理员乙", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.OK)); Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();
        var (directory, _) = await service.ReadAsync(default);
        Assert.Single(directory.Users.Where(x => !BuiltInSuperUser.Is(x))); Assert.Single(directory.Users.Where(BuiltInSuperUser.Is));
    }
    /// <summary>首次创建管理员不能使用任何大小写形式的保留账号名。</summary>
    [Theory]
    [InlineData("hisoka")]
    [InlineData("HISOKA")]
    [InlineData("HiSoKa")]
    [InlineData(" hisoka ")]
    public async Task BootstrapRejectsReservedName(string account) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await AccessApiTests.CreateAsync(db); using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/access/bootstrap", new { username = account, name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" })).StatusCode);
        Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/access/session")).GetProperty("configured").GetBoolean());
        Assert.Null(await new ManagedDocumentService(db.Factory).ReadAsync("access-directory", default));
    }
    /// <summary>内置用户不可修改；新建和重命名普通用户均不能抢占保留名。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReservedAccountAndBuiltInIdentityCannotBeModified(bool enforceAuthorization) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await AccessApiTests.CreateAsync(db, enforceAuthorization); using var admin = app.GetTestClient();
        admin.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(admin, await admin.PostAsJsonAsync("/api/access/bootstrap", new { username = "admin", name = "管理员", password = "test-admin-password", bootstrapKey = "test-bootstrap-key" }));
        var directory = await admin.GetFromJsonAsync<JsonElement>("/api/access");
        var revision = directory.GetProperty("revision").GetInt32();
        var adminId = directory.GetProperty("users").EnumerateArray().Single(x => x.GetProperty("account").GetString() == "admin").GetProperty("id").GetString();
        foreach (var account in new[] { "hisoka", "HISOKA", "HiSoKa", " hisoka " }) {
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = revision, account, name = "占用保留名", roleId = 1, password = "test-normal-password" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = revision, id = adminId, account, name = "重命名", roleId = 1 })).StatusCode);
        }
        foreach (var change in new[] {
            new { account = "hisoka", roleId = 1, enabled = false, password = "" },
            new { account = "renamed", roleId = 1, enabled = true, password = "" },
            new { account = "hisoka", roleId = 2, enabled = true, password = "test-replacement-password" }
        }) Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = revision, id = BuiltInSuperUser.Id, change.account, change.roleId, change.enabled, change.password, name = "修改内置" })).StatusCode);
        Assert.Equal(revision, (await admin.GetFromJsonAsync<JsonElement>("/api/access")).GetProperty("revision").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/roles", new { expectedRevision = revision, name = "无权限角色", permissions = Array.Empty<string>() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/access/users", new { expectedRevision = revision + 1, account = "normal", name = "普通账号", roleId = 2, password = "test-normal-password", builtIn = true })).StatusCode);
        directory = await admin.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.False(directory.GetProperty("users").EnumerateArray().Single(x => x.GetProperty("account").GetString() == "normal").GetProperty("builtIn").GetBoolean());
    }
    /// <summary>启动自动移除旧同名账号，保留其他账号，并撤销冲突账号的旧会话。</summary>
    [Theory]
    [InlineData("hisoka")]
    [InlineData("HISOKA")]
    [InlineData("HiSoKa")]
    [InlineData(" hisoka ")]
    public async Task LegacyCollisionIsRemovedAndOldSessionRevoked(string account) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var legacyUser = AccessDirectoryService.CreateUser(account, "旧同名账号", 1, "legacy-admin-password");
        var admin = AccessDirectoryService.CreateUser("existing-admin", "现有管理员", 1, "test-admin-password");
        var role = new AccessRole { Id = 1, BuiltIn = true, Name = "超级管理员", Permissions = AccessDirectoryService.PermissionCodes };
        var store = new ManagedDocumentService(db.Factory);
        await store.WriteAsync("access-directory", JsonSerializer.Serialize(new AccessDirectory { Users = [admin, legacyUser], Roles = [role] }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), 0, default);
        await using var app = await AccessApiTests.CreateAsync(db); using var oldSession = app.GetTestClient();
        var cookie = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("SortingCookie");
        var ticket = new AuthenticationTicket(AccessDirectoryService.Principal(legacyUser, role), new AuthenticationProperties { ExpiresUtc = DateTimeOffset.Now.AddHours(1) }, "SortingCookie");
        oldSession.DefaultRequestHeaders.Add("Cookie", cookie.Cookie.Name + "=" + cookie.TicketDataFormat.Protect(ticket));
        await new BuiltInAccountHostedService(app.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<BuiltInAccountHostedService>.Instance).StartAsync(default);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/access")).StatusCode);
        var service = new AccessDirectoryService(store, app.Services.GetRequiredService<IConfiguration>());
        var (directory, revision) = await service.ReadAsync(default);
        Assert.True(directory.Initialized); Assert.Equal(2, directory.Users.Length);
        Assert.Contains(directory.Users, x => x.Id == admin.Id); Assert.DoesNotContain(directory.Users, x => x.Id == legacyUser.Id);
        var builtIn = Assert.Single(directory.Users.Where(x => BuiltInSuperUser.IsReservedAccount(x.Account)));
        Assert.Equal(BuiltInSuperUser.Id, builtIn.Id); Assert.True(builtIn.BuiltIn); Assert.True(AccessDirectoryService.VerifyPassword(builtIn, BuiltInPassword));
        Assert.Equal(revision, (await service.ReadAsync(default)).Revision);
        Assert.DoesNotContain(legacyUser.Id, (await store.ReadAsync("access-directory", default))!.Json);
    }
    /// <summary>保留名冲突处理仍恢复固定身份；无普通成员时重新提示管理员创建。</summary>
    [Fact]
    public async Task LegacySoleCollisionRetainsInitializationStateAndRestoresFixedCredentials() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var store = new ManagedDocumentService(db.Factory);
        var legacy = AccessDirectoryService.CreateUser("HISOKA", "旧管理员", 1, "legacy-admin-password");
        await store.WriteAsync("access-directory", JsonSerializer.Serialize(new AccessDirectory { Users = [legacy] }, new JsonSerializerOptions(JsonSerializerDefaults.Web)), 0, default);
        var service = new AccessDirectoryService(store, new ConfigurationBuilder().Build());
        var (directory, revision) = await service.ReadAsync(default);
        Assert.True(directory.Initialized); var builtIn = Assert.Single(directory.Users); Assert.True(BuiltInSuperUser.Is(builtIn));
        var corrupted = directory with { Users = [builtIn with { RoleId = 2, Enabled = false, PasswordHash = legacy.PasswordHash, SecurityStamp = "tampered" }], Roles = [new AccessRole { Id = 2, Name = "空权限", Permissions = [] }] };
        await store.WriteAsync("access-directory", JsonSerializer.Serialize(corrupted, new JsonSerializerOptions(JsonSerializerDefaults.Web)), revision, default);
        var (restored, _) = await service.ReadAsync(default); var user = Assert.Single(restored.Users);
        Assert.True(user.Enabled); Assert.Equal(1L, user.RoleId); Assert.True(AccessDirectoryService.VerifyPassword(user, BuiltInPassword)); Assert.False(AccessDirectoryService.VerifyPassword(user, "legacy-admin-password"));
        Assert.Equal(AccessDirectoryService.PermissionCodes.Order(), AccessDirectoryService.Principal(user, restored.Roles[0]).FindAll("permission").Select(x => x.Value).Order());
        await using var app = await AccessApiTests.CreateAsync(db); using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        AccessApiTests.UseCookie(client, await client.PostAsJsonAsync("/api/access/login", new { username = "hisoka", password = BuiltInPassword }));
        var snapshot = await client.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.False(snapshot.GetProperty("configured").GetBoolean()); Assert.Empty(snapshot.GetProperty("users").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/access/roles", new { expectedRevision = snapshot.GetProperty("revision").GetInt32(), name = "普通查询角色", permissions = new[] { "parcels.read" } })).StatusCode);
        snapshot = await client.GetFromJsonAsync<JsonElement>("/api/access");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/access/users", new { expectedRevision = snapshot.GetProperty("revision").GetInt32(), account = "reader", name = "普通查询用户", roleId = 2, password = "test-reader-password" })).StatusCode);
        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/access/session")).GetProperty("configured").GetBoolean());
    }
}
