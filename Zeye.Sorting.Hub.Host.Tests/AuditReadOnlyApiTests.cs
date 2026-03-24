using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Services.AuditLogs;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Enums.AuditLogs;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Host;
using Zeye.Sorting.Hub.Host.Middleware;
using WebRequestAuditLogListResponse = Zeye.Sorting.Hub.Contracts.Models.AuditLogs.WebRequests.WebRequestAuditLogListResponse;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 审计日志只读 API 端点回归测试。
/// </summary>
public sealed class AuditReadOnlyApiTests {
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
        Assert.Equal(1001, payload.RootElement.GetProperty("id").GetInt64());
        Assert.Equal("{}", payload.RootElement.GetProperty("requestHeadersJson").GetString());
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
        builder.Services.AddScoped<IWebRequestAuditLogRepository>(_ => repository);
        builder.Services.AddScoped<IWebRequestAuditLogQueryRepository>(_ => repository);
        builder.Services.AddScoped<GetWebRequestAuditLogPagedQueryService>();
        builder.Services.AddScoped<GetWebRequestAuditLogByIdQueryService>();
        var app = builder.Build();
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
        builder.Services.AddScoped<IWebRequestAuditLogRepository>(_ => repository);
        builder.Services.AddScoped<IWebRequestAuditLogQueryRepository>(_ => repository);
        builder.Services.AddScoped<WriteWebRequestAuditLogCommandService>();
        builder.Services.AddScoped<GetWebRequestAuditLogPagedQueryService>();
        builder.Services.AddScoped<GetWebRequestAuditLogByIdQueryService>();
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
        app.MapGet("/ok", () => Results.Text("pong", "text/plain"));
        app.MapAuditReadOnlyApis();
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
            UserId = null,
            UserName = string.Empty,
            IsAuthenticated = false,
            TenantId = null,
            RequestPayloadType = WebRequestPayloadType.None,
            RequestSizeBytes = 0,
            HasRequestBody = false,
            IsRequestBodyTruncated = false,
            ResponsePayloadType = WebResponsePayloadType.Json,
            ResponseSizeBytes = 128,
            HasResponseBody = true,
            IsResponseBodyTruncated = false,
            StatusCode = statusCode,
            IsSuccess = isSuccess,
            HasException = !isSuccess,
            AuditResourceType = AuditResourceType.Api,
            ResourceId = "resource-1",
            StartedAt = startedAt,
            EndedAt = startedAt.AddMilliseconds(50),
            DurationMs = 50,
            CreatedAt = startedAt,
            Detail = new WebRequestAuditLogDetail {
                WebRequestAuditLogId = id,
                StartedAt = startedAt,
                RequestUrl = $"http://localhost{requestPath}",
                RequestQueryString = string.Empty,
                RequestHeadersJson = "{}",
                ResponseHeadersJson = "{}",
                RequestContentType = "application/json",
                ResponseContentType = "application/json",
                Accept = "application/json",
                Referer = string.Empty,
                Origin = string.Empty,
                AuthorizationType = string.Empty,
                UserAgent = "xunit",
                RequestBody = string.Empty,
                ResponseBody = "{\"ok\":true}",
                CurlCommand = "curl",
                ErrorMessage = string.Empty,
                ExceptionType = string.Empty,
                ErrorCode = string.Empty,
                ExceptionStackTrace = string.Empty,
                FileMetadataJson = "{}",
                HasFileAccess = false,
                FileOperationType = FileOperationType.None,
                FileCount = 0,
                FileTotalBytes = 0,
                ImageMetadataJson = "{}",
                HasImageAccess = false,
                ImageCount = 0,
                DatabaseOperationSummary = string.Empty,
                HasDatabaseAccess = false,
                DatabaseAccessCount = 0,
                DatabaseDurationMs = 0,
                ResourceCode = string.Empty,
                ResourceName = string.Empty,
                ActionDurationMs = 0,
                MiddlewareDurationMs = 0,
                Tags = string.Empty,
                ExtraPropertiesJson = "{}",
                Remark = string.Empty
            }
        };
    }
}
