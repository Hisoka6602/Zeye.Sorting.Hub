using System.Net;
using System.Net.Http.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Security;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>缺少部署密钥时，本机首次管理员初始化仍须验证独立且受保护的密钥。</summary>
public sealed class AdministratorBootstrapTests {
    /// <summary>自动生成密钥，重启保留；真实账号提交完成后关闭入口并删除文件。</summary>
    [Fact]
    public async Task LocalFirstRunCreatesProtectedKeyAndRetiresItAfterAccountCommit() {
        using var storage = new ConfigurationTestStorage();
        await using var database = new RelationalParcelTestDatabase(); await database.InitializeAsync();
        var keys = new AdministratorBootstrapKeyStore(Path.Combine(storage.DirectoryPath, "administrator-bootstrap.key"));
        await using var app = await CreateAsync(database, keys);
        using var client = app.GetTestClient(); client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        var session = await client.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.False(session.GetProperty("configured").GetBoolean());
        Assert.True(session.GetProperty("bootstrapAvailable").GetBoolean());
        Assert.True(session.GetProperty("bootstrapLocalOnly").GetBoolean());
        Assert.Equal(keys.KeyPath, session.GetProperty("bootstrapKeyPath").GetString());
        var key = File.ReadAllText(keys.KeyPath);
        Assert.Equal(64, key.Length); Assert.DoesNotContain(key, session.GetRawText());
        Assert.Equal(key, new AdministratorBootstrapKeyStore(keys.KeyPath).GetOrCreate());
        if (OperatingSystem.IsWindows()) {
            var permissions = new FileInfo(keys.KeyPath).GetAccessControl();
            Assert.True(permissions.AreAccessRulesProtected);
            var allowed = permissions.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>().ToArray();
            using var identity = WindowsIdentity.GetCurrent();
            var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            Assert.All(allowed, rule => Assert.True(rule.IdentityReference.Equals(identity.User) || rule.IdentityReference.Equals(administrators)));
        }
        else Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(keys.KeyPath));
        var created = await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "first-admin", name = "测试管理员", password = "test-admin-password", bootstrapKey = key });
        AccessApiTests.UseCookie(client, created);
        Assert.False(File.Exists(keys.KeyPath));
        Assert.Null(keys.GetOrCreate());
        session = await client.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.True(session.GetProperty("configured").GetBoolean());
        Assert.True(session.GetProperty("isSuperAdministrator").GetBoolean());
        Assert.False(session.GetProperty("bootstrapAvailable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, session.GetProperty("bootstrapKeyPath").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "duplicate", name = "重复管理员", password = "test-admin-password", bootstrapKey = key })).StatusCode);
    }

    /// <summary>远程、伪造主机、跨源、缺少来源标识和错误密钥都不能创建管理员。</summary>
    [Theory]
    [InlineData("remote")]
    [InlineData("wrong-host")]
    [InlineData("cross-origin")]
    [InlineData("missing-client")]
    [InlineData("wrong-key")]
    public async Task LocalBootstrapRejectsUntrustedRequests(string scenario) {
        using var storage = new ConfigurationTestStorage();
        await using var database = new RelationalParcelTestDatabase(); await database.InitializeAsync();
        var keys = new AdministratorBootstrapKeyStore(Path.Combine(storage.DirectoryPath, "administrator-bootstrap.key"));
        var key = keys.GetOrCreate(); Assert.NotNull(key);
        await using var app = await CreateAsync(database, keys, scenario == "remote");
        using var client = app.GetTestClient();
        if (scenario != "missing-client") client.DefaultRequestHeaders.Add("X-Zeye-Client", "web");
        if (scenario == "wrong-host") client.DefaultRequestHeaders.Host = "untrusted.example";
        if (scenario == "cross-origin") client.DefaultRequestHeaders.Add("Origin", "http://untrusted.example");
        var session = await client.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.DoesNotContain(key, session.GetRawText());
        if (scenario is "remote" or "wrong-host") {
            Assert.False(session.GetProperty("bootstrapAvailable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, session.GetProperty("bootstrapKeyPath").ValueKind);
        }
        var response = await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "unauthorized", name = "测试管理员", password = "test-admin-password", bootstrapKey = scenario == "wrong-key" ? "old-database-setup-key" : key });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        session = await client.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.False(session.GetProperty("configured").GetBoolean());
        Assert.True(File.Exists(keys.KeyPath));
    }

    /// <summary>远程会话不能触发本机密钥生成；显式部署密钥继续沿用原有初始化方式。</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteSessionsDoNotCreateOrExposeLocalKeys(bool configuredKey) {
        using var storage = new ConfigurationTestStorage();
        await using var database = new RelationalParcelTestDatabase(); await database.InitializeAsync();
        var keys = new AdministratorBootstrapKeyStore(Path.Combine(storage.DirectoryPath, "administrator-bootstrap.key"));
        await using var app = await CreateAsync(database, keys, remote: true, configuredKey ? "deployment-test-key" : "");
        using var client = app.GetTestClient();
        var session = await client.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.Equal(configuredKey, session.GetProperty("bootstrapAvailable").GetBoolean());
        Assert.Equal(JsonValueKind.Null, session.GetProperty("bootstrapKeyPath").ValueKind);
        Assert.False(File.Exists(keys.KeyPath));
        if (configuredKey) {
            var response = await client.PostAsJsonAsync("/api/access/bootstrap", new { username = "configured-admin", name = "部署管理员", password = "test-admin-password", bootstrapKey = "deployment-test-key" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(File.Exists(keys.KeyPath));
        }
    }

    /// <summary>损坏的本机文件不能被静默覆盖，也不能开放无有效凭据的初始化。</summary>
    [Fact]
    public async Task InvalidLocalKeyFailsClosedWithoutReplacingTheFile() {
        using var storage = new ConfigurationTestStorage();
        await using var database = new RelationalParcelTestDatabase(); await database.InitializeAsync();
        var keys = new AdministratorBootstrapKeyStore(Path.Combine(storage.DirectoryPath, "administrator-bootstrap.key"));
        ProtectedSecretFile.Write(keys.KeyPath, "invalid-key");
        await using var app = await CreateAsync(database, keys);
        using var client = app.GetTestClient();
        var session = await client.GetFromJsonAsync<JsonElement>("/api/access/session");
        Assert.False(session.GetProperty("bootstrapAvailable").GetBoolean());
        Assert.Equal("invalid-key", File.ReadAllText(keys.KeyPath));
    }

    /// <summary>使用真实认证与关系库，只替换独立密钥路径和测试连接地址。</summary>
    private static Task<WebApplication> CreateAsync(RelationalParcelTestDatabase database, AdministratorBootstrapKeyStore keys, bool remote = false, string configuredKey = "") =>
        AccessApiTests.CreateAsync(database, configureServices: builder => {
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Access:BootstrapKey"] = configuredKey });
            builder.Services.AddSingleton(keys);
        }, configureRoutes: app => app.Use((context, next) => {
            context.Connection.RemoteIpAddress = remote ? IPAddress.Parse("192.0.2.10") : IPAddress.Loopback;
            return next();
        }));
}
