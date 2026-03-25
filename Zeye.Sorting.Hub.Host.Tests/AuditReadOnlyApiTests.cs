using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Services.AuditLogs;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Enums.AuditLogs;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Host.Options;
using Zeye.Sorting.Hub.Host.Middleware;
using Zeye.Sorting.Hub.Host.Routing;
using WebRequestAuditLogListResponse = Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests.WebRequestAuditLogListResponse;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 审计日志只读 API 端点回归测试。
/// </summary>
public sealed class AuditReadOnlyApiTests {
    /// <summary>
    /// 验证场景：AuditReadOnlyApi:Enabled=true 时映射审计只读端点。
    /// </summary>
    [Fact]
    public async Task AuditReadOnlyApiEnabled_WhenTrue_ShouldMapEndpoints() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        SeedLogs(repository);
        await using var app = await BuildConditionalAuditRouteAppAsync(repository, enabled: true);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/audit/web-requests?pageNumber=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：AuditReadOnlyApi:Enabled=false 时不映射审计只读端点。
    /// </summary>
    [Fact]
    public async Task AuditReadOnlyApiEnabled_WhenFalse_ShouldNotMapEndpoints() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        SeedLogs(repository);
        await using var app = await BuildConditionalAuditRouteAppAsync(repository, enabled: false);
        using var client = app.GetTestClient();

        var listResponse = await client.GetAsync("/api/audit/web-requests?pageNumber=1&pageSize=10");
        var detailResponse = await client.GetAsync("/api/audit/web-requests/1001");

        Assert.Equal(HttpStatusCode.NotFound, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
    }

    /// <summary>
    /// 验证场景：默认分页查询返回列表。
    /// </summary>
    [Fact]
    public async Task GetAuditWebRequests_ShouldReturnPagedList() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        SeedLogs(repository);
        await using var app = await BuildTestAppAsync(repository);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/audit/web-requests?pageNumber=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<WebRequestAuditLogListResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.PageNumber);
        Assert.Equal(10, payload.PageSize);
        Assert.True(payload.TotalCount >= 3);
        Assert.True(payload.Items.Count >= 3);
    }

    /// <summary>
    /// 验证场景：过滤条件组合生效。
    /// </summary>
    [Fact]
    public async Task GetAuditWebRequests_WithFilters_ShouldReturnExpectedItems() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        SeedLogs(repository);
        await using var app = await BuildTestAppAsync(repository);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/audit/web-requests?pageNumber=1&pageSize=10&startedAtStart=2026-03-20 09:00:00&startedAtEnd=2026-03-20 10:30:00&statusCode=200&isSuccess=true&traceId=trace-filter&correlationId=corr-filter");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<WebRequestAuditLogListResponse>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload.TotalCount);
        Assert.Single(payload.Items);
        Assert.Equal("trace-filter", payload.Items[0].TraceId);
    }

    /// <summary>
    /// 验证场景：非法分页参数返回 400。
    /// </summary>
    [Fact]
    public async Task GetAuditWebRequests_WithInvalidPaging_ShouldReturnBadRequest() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        SeedLogs(repository);
        await using var app = await BuildTestAppAsync(repository);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/audit/web-requests?pageNumber=0&pageSize=10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：UTC/offset 时间参数返回 400。
    /// </summary>
    [Fact]
    public async Task GetAuditWebRequests_WithUtcOrOffsetTime_ShouldReturnBadRequest() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        SeedLogs(repository);
        await using var app = await BuildTestAppAsync(repository);
        using var client = app.GetTestClient();

        var utcResponse = await client.GetAsync("/api/audit/web-requests?startedAtStart=2026-03-20T10:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, utcResponse.StatusCode);

        var offsetResponse = await client.GetAsync("/api/audit/web-requests?startedAtEnd=2026-03-20T10:00:00+08:00");
        Assert.Equal(HttpStatusCode.BadRequest, offsetResponse.StatusCode);
    }

    /// <summary>
    /// 验证场景：详情存在返回 200 且带冷表详情字段。
    /// </summary>
    [Fact]
    public async Task GetAuditWebRequestById_WhenExists_ShouldReturnDetail() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        SeedLogs(repository);
        await using var app = await BuildTestAppAsync(repository);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/audit/web-requests/1001");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.NotNull(payload);
        var root = payload.RootElement;
        Assert.Equal(1001, root.GetProperty("webRequestAuditLogId").GetInt64());
        Assert.Equal(1001, root.GetProperty("id").GetInt64());
        Assert.Equal("trace-filter", root.GetProperty("traceId").GetString());
        Assert.Equal("corr-filter", root.GetProperty("correlationId").GetString());
        Assert.Equal("span-1001", root.GetProperty("spanId").GetString());
        Assert.Equal("Test.Operation", root.GetProperty("operationName").GetString());
        Assert.Equal("GET", root.GetProperty("requestMethod").GetString());
        Assert.Equal("http", root.GetProperty("requestScheme").GetString());
        Assert.Equal("localhost", root.GetProperty("requestHost").GetString());
        Assert.Equal(80, root.GetProperty("requestPort").GetInt32());
        Assert.Equal("/api/parcels", root.GetProperty("requestPath").GetString());
        Assert.Equal("/api/parcels", root.GetProperty("requestRouteTemplate").GetString());
        Assert.Equal(3001, root.GetProperty("userId").GetInt64());
        Assert.Equal("seed-user-1001", root.GetProperty("userName").GetString());
        Assert.True(root.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal(9001, root.GetProperty("tenantId").GetInt64());
        Assert.Equal(2, root.GetProperty("requestPayloadType").GetInt32());
        Assert.Equal(201, root.GetProperty("requestSizeBytes").GetInt64());
        Assert.True(root.GetProperty("hasRequestBody").GetBoolean());
        Assert.False(root.GetProperty("isRequestBodyTruncated").GetBoolean());
        Assert.Equal(2, root.GetProperty("responsePayloadType").GetInt32());
        Assert.Equal(301, root.GetProperty("responseSizeBytes").GetInt64());
        Assert.True(root.GetProperty("hasResponseBody").GetBoolean());
        Assert.False(root.GetProperty("isResponseBodyTruncated").GetBoolean());
        Assert.Equal(200, root.GetProperty("statusCode").GetInt32());
        Assert.True(root.GetProperty("isSuccess").GetBoolean());
        Assert.False(root.GetProperty("hasException").GetBoolean());
        Assert.Equal(1, root.GetProperty("auditResourceType").GetInt32());
        Assert.Equal("resource-1001", root.GetProperty("resourceId").GetString());
        Assert.Equal("http://localhost/api/parcels?source=seed&id=1001", root.GetProperty("requestUrl").GetString());
        Assert.Equal("?source=seed&id=1001", root.GetProperty("requestQueryString").GetString());
        Assert.Equal("{\"x-seed\":\"1001\"}", root.GetProperty("requestHeadersJson").GetString());
        Assert.Equal("{\"x-response\":\"1001\"}", root.GetProperty("responseHeadersJson").GetString());
        Assert.Equal("application/json", root.GetProperty("requestContentType").GetString());
        Assert.Equal("application/json", root.GetProperty("responseContentType").GetString());
        Assert.Equal("application/json", root.GetProperty("accept").GetString());
        Assert.Equal("https://ref.example.local/path", root.GetProperty("referer").GetString());
        Assert.Equal("https://origin.example.local", root.GetProperty("origin").GetString());
        Assert.Equal("Bearer", root.GetProperty("authorizationType").GetString());
        Assert.Equal("xunit-seed/1.0", root.GetProperty("userAgent").GetString());
        Assert.Equal("{\"requestId\":1001}", root.GetProperty("requestBody").GetString());
        Assert.Equal("{\"ok\":true,\"id\":1001}", root.GetProperty("responseBody").GetString());
        Assert.Contains("curl -X", root.GetProperty("curlCommand").GetString(), StringComparison.Ordinal);
        Assert.Equal("detail-error-code-1001", root.GetProperty("errorCode").GetString());
        Assert.Equal("{\"files\":[1001]}", root.GetProperty("fileMetadataJson").GetString());
        Assert.True(root.GetProperty("hasFileAccess").GetBoolean());
        Assert.Equal(1, root.GetProperty("fileOperationType").GetInt32());
        Assert.Equal(2, root.GetProperty("fileCount").GetInt32());
        Assert.Equal(2049, root.GetProperty("fileTotalBytes").GetInt64());
        Assert.Equal("{\"images\":[1001]}", root.GetProperty("imageMetadataJson").GetString());
        Assert.True(root.GetProperty("hasImageAccess").GetBoolean());
        Assert.Equal(3, root.GetProperty("imageCount").GetInt32());
        Assert.Equal("db-op-summary-1001", root.GetProperty("databaseOperationSummary").GetString());
        Assert.True(root.GetProperty("hasDatabaseAccess").GetBoolean());
        Assert.Equal(4, root.GetProperty("databaseAccessCount").GetInt32());
        Assert.Equal(510, root.GetProperty("databaseDurationMs").GetInt64());
        Assert.Equal("resource-code-1001", root.GetProperty("resourceCode").GetString());
        Assert.Equal("resource-name-1001", root.GetProperty("resourceName").GetString());
        Assert.Equal(121, root.GetProperty("actionDurationMs").GetInt64());
        Assert.Equal(221, root.GetProperty("middlewareDurationMs").GetInt64());
        Assert.Equal("tag-seed-1001", root.GetProperty("tags").GetString());
        Assert.Equal("{\"seed\":\"1001\"}", root.GetProperty("extraPropertiesJson").GetString());
        Assert.Equal("remark-seed-1001", root.GetProperty("remark").GetString());
    }

    /// <summary>
    /// 验证场景：详情不存在返回 404。
    /// </summary>
    [Fact]
    public async Task GetAuditWebRequestById_WhenNotFound_ShouldReturnNotFound() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        SeedLogs(repository);
        await using var app = await BuildTestAppAsync(repository);
        using var client = app.GetTestClient();

        var response = await client.GetAsync("/api/audit/web-requests/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：与中间件写入链路联动后可查询写入数据。
    /// </summary>
    [Fact]
    public async Task GetAuditWebRequests_AfterMiddlewareWrite_ShouldReadInsertedLog() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        await using var app = await BuildTestAppWithMiddlewareAsync(repository);
        using var client = app.GetTestClient();

        using var writeResponse = await client.GetAsync("/ok");
        Assert.Equal(HttpStatusCode.OK, writeResponse.StatusCode);

        for (var attempt = 0; attempt < 20 && repository.WriteCount < 1; attempt++) {
            await Task.Delay(50);
        }

        var queryResponse = await client.GetAsync("/api/audit/web-requests?pageNumber=1&pageSize=10&requestPathKeyword=%2Fok");
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        var payload = await queryResponse.Content.ReadFromJsonAsync<WebRequestAuditLogListResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.TotalCount >= 1);
        Assert.Contains(payload.Items, item => item.RequestPath.Contains("/ok", StringComparison.Ordinal));
    }

    /// <summary>
    /// 构建测试应用（仅查询端点）。
    /// </summary>
    /// <param name="repository">审计日志仓储替身。</param>
    /// <returns>已启动应用。</returns>
    private static async Task<WebApplication> BuildTestAppAsync(InMemoryWebRequestAuditLogRepository repository) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<IWebRequestAuditLogRepository>(_ => repository);
        builder.Services.AddScoped<IWebRequestAuditLogQueryRepository>(_ => repository);
        builder.Services.AddScoped<GetWebRequestAuditLogPagedQueryService>();
        builder.Services.AddScoped<GetWebRequestAuditLogByIdQueryService>();
        var app = builder.Build();
        app.Use(async (context, next) => {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "audit-test-user")],
                authenticationType: "TestAuth"));
            await next();
        });
        app.UseAuthorization();
        app.MapAuditReadOnlyApis();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 构建测试应用（中间件写入 + 查询端点联动）。
    /// </summary>
    /// <param name="repository">审计日志仓储替身。</param>
    /// <returns>已启动应用。</returns>
    private static async Task<WebApplication> BuildTestAppWithMiddlewareAsync(InMemoryWebRequestAuditLogRepository repository) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<IWebRequestAuditLogRepository>(_ => repository);
        builder.Services.AddScoped<IWebRequestAuditLogQueryRepository>(_ => repository);
        builder.Services.AddScoped<WriteWebRequestAuditLogCommandService>();
        builder.Services.AddScoped<GetWebRequestAuditLogPagedQueryService>();
        builder.Services.AddScoped<GetWebRequestAuditLogByIdQueryService>();
        builder.Services.AddSingleton(new WebRequestAuditBackgroundQueue(256));
        builder.Services.AddHostedService<WebRequestAuditBackgroundWorkerHostedService>();
        builder.Services.Configure<WebRequestAuditLogOptions>(options => {
            options.Enabled = true;
            options.SampleRate = 1;
            options.IncludeRequestBody = true;
            options.IncludeResponseBody = true;
            options.MaxRequestBodyLength = 1024;
            options.MaxResponseBodyLength = 1024;
        });
        var app = builder.Build();
        app.UseWebRequestAuditLogging();
        app.Use(async (context, next) => {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "audit-test-user")],
                authenticationType: "TestAuth"));
            await next();
        });
        app.UseAuthorization();
        app.MapGet("/ok", () => Results.Text("pong", "text/plain"));
        app.MapAuditReadOnlyApis();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 构建按配置条件映射审计只读端点的测试应用。
    /// </summary>
    /// <param name="repository">审计日志仓储替身。</param>
    /// <param name="enabled">是否启用审计只读端点。</param>
    /// <returns>已启动应用。</returns>
    private static async Task<WebApplication> BuildConditionalAuditRouteAppAsync(
        InMemoryWebRequestAuditLogRepository repository,
        bool enabled) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration[$"{AuditReadOnlyApiOptions.SectionName}:Enabled"] = enabled.ToString();
        builder.Services.AddProblemDetails();
        builder.Services.AddAuthorization();
        builder.Services.AddScoped<IWebRequestAuditLogRepository>(_ => repository);
        builder.Services.AddScoped<IWebRequestAuditLogQueryRepository>(_ => repository);
        builder.Services.AddScoped<GetWebRequestAuditLogPagedQueryService>();
        builder.Services.AddScoped<GetWebRequestAuditLogByIdQueryService>();
        var app = builder.Build();
        app.Use(async (context, next) => {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "audit-test-user")],
                authenticationType: "TestAuth"));
            await next();
        });
        app.UseAuthorization();
        var options = new AuditReadOnlyApiOptions {
            Enabled = bool.TryParse(
                builder.Configuration[$"{AuditReadOnlyApiOptions.SectionName}:Enabled"],
                out var optionEnabled)
                ? optionEnabled
                : false
        };
        if (options.Enabled) {
            app.MapAuditReadOnlyApis();
        }

        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 写入预置日志数据。
    /// </summary>
    /// <param name="repository">审计日志仓储替身。</param>
    private static void SeedLogs(InMemoryWebRequestAuditLogRepository repository) {
        var startedAt = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Local);
        var seedLogs = new[] {
            CreateAuditLog(
                id: 1001,
                traceId: "trace-filter",
                correlationId: "corr-filter",
                requestPath: "/api/parcels",
                statusCode: 200,
                isSuccess: true,
                startedAt: startedAt),
            CreateAuditLog(
                id: 1002,
                traceId: "trace-other",
                correlationId: "corr-filter",
                requestPath: "/api/parcels/2",
                statusCode: 500,
                isSuccess: false,
                startedAt: startedAt.AddHours(1)),
            CreateAuditLog(
                id: 1003,
                traceId: "trace-third",
                correlationId: "corr-third",
                requestPath: "/api/admin/parcels",
                statusCode: 201,
                isSuccess: true,
                startedAt: startedAt.AddHours(-1))
        };

        foreach (var log in seedLogs) {
            var result = repository.AddAsync(log, CancellationToken.None).GetAwaiter().GetResult();
            Assert.True(result.IsSuccess, result.ErrorMessage);
        }
    }

    /// <summary>
    /// 构建单条审计日志。
    /// </summary>
    /// <param name="id">日志主键。</param>
    /// <param name="traceId">追踪 Id。</param>
    /// <param name="correlationId">关联 Id。</param>
    /// <param name="requestPath">请求路径。</param>
    /// <param name="statusCode">状态码。</param>
    /// <param name="isSuccess">是否成功。</param>
    /// <param name="startedAt">开始时间。</param>
    /// <returns>审计日志实体。</returns>
    private static WebRequestAuditLog CreateAuditLog(
        long id,
        string traceId,
        string correlationId,
        string requestPath,
        int statusCode,
        bool isSuccess,
        DateTime startedAt) {
        return new WebRequestAuditLog {
            Id = id,
            TraceId = traceId,
            CorrelationId = correlationId,
            SpanId = $"span-{id}",
            OperationName = "Test.Operation",
            RequestMethod = "GET",
            RequestScheme = "http",
            RequestHost = "localhost",
            RequestPort = 80,
            RequestPath = requestPath,
            RequestRouteTemplate = requestPath,
            UserId = 3001,
            UserName = $"seed-user-{id}",
            IsAuthenticated = true,
            TenantId = 9001,
            RequestPayloadType = WebRequestPayloadType.Json,
            RequestSizeBytes = 201,
            HasRequestBody = true,
            IsRequestBodyTruncated = false,
            ResponsePayloadType = WebResponsePayloadType.Json,
            ResponseSizeBytes = 301,
            HasResponseBody = true,
            IsResponseBodyTruncated = false,
            StatusCode = statusCode,
            IsSuccess = isSuccess,
            HasException = !isSuccess,
            AuditResourceType = AuditResourceType.Api,
            ResourceId = $"resource-{id}",
            StartedAt = startedAt,
            EndedAt = startedAt.AddMilliseconds(50),
            DurationMs = 50,
            CreatedAt = startedAt,
            Detail = new WebRequestAuditLogDetail {
                WebRequestAuditLogId = id,
                StartedAt = startedAt,
                RequestUrl = $"http://localhost{requestPath}?source=seed&id={id}",
                RequestQueryString = $"?source=seed&id={id}",
                RequestHeadersJson = $"{{\"x-seed\":\"{id}\"}}",
                ResponseHeadersJson = $"{{\"x-response\":\"{id}\"}}",
                RequestContentType = "application/json",
                ResponseContentType = "application/json",
                Accept = "application/json",
                Referer = "https://ref.example.local/path",
                Origin = "https://origin.example.local",
                AuthorizationType = "Bearer",
                UserAgent = "xunit-seed/1.0",
                RequestBody = $"{{\"requestId\":{id}}}",
                ResponseBody = $"{{\"ok\":true,\"id\":{id}}}",
                CurlCommand = $"curl -X GET 'http://localhost{requestPath}?source=seed&id={id}'",
                ErrorMessage = string.Empty,
                ExceptionType = string.Empty,
                ErrorCode = $"detail-error-code-{id}",
                ExceptionStackTrace = string.Empty,
                FileMetadataJson = $"{{\"files\":[{id}]}}",
                HasFileAccess = true,
                FileOperationType = FileOperationType.Read,
                FileCount = 2,
                FileTotalBytes = 2049,
                ImageMetadataJson = $"{{\"images\":[{id}]}}",
                HasImageAccess = true,
                ImageCount = 3,
                DatabaseOperationSummary = $"db-op-summary-{id}",
                HasDatabaseAccess = true,
                DatabaseAccessCount = 4,
                DatabaseDurationMs = 510,
                ResourceCode = $"resource-code-{id}",
                ResourceName = $"resource-name-{id}",
                ActionDurationMs = 121,
                MiddlewareDurationMs = 221,
                Tags = $"tag-seed-{id}",
                ExtraPropertiesJson = $"{{\"seed\":\"{id}\"}}",
                Remark = $"remark-seed-{id}"
            }
        };
    }
}
