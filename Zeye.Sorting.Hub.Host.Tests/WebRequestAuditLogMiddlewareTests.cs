using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Services.AuditLogs;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Host.Middleware;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Web 请求审计日志中间件回归测试。
/// </summary>
public sealed class WebRequestAuditLogMiddlewareTests {
    /// <summary>
    /// 验证场景：Enabled=false 时不写审计日志。
    /// </summary>
    [Fact]
    public async Task Middleware_WithDisabledOption_ShouldNotWriteAuditLog() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        await using var app = await BuildTestAppAsync(
            new WebRequestAuditLogOptions {
                Enabled = false,
                SampleRate = 1,
                IncludeRequestBody = true,
                IncludeResponseBody = true,
                MaxRequestBodyLength = 1024,
                MaxResponseBodyLength = 1024
            },
            repository);

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/ok");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, repository.WriteCount);
    }

    /// <summary>
    /// 验证场景：SampleRate=0 不写、SampleRate=1 必写。
    /// </summary>
    [Fact]
    public async Task Middleware_ShouldHonorSampleRateBoundaries() {
        var repository = new InMemoryWebRequestAuditLogRepository();

        await using (var app = await BuildTestAppAsync(
                         new WebRequestAuditLogOptions {
                             Enabled = true,
                             SampleRate = 0,
                             IncludeRequestBody = false,
                             IncludeResponseBody = false,
                             MaxRequestBodyLength = 1024,
                             MaxResponseBodyLength = 1024
                         },
                         repository)) {
            using var client = app.GetTestClient();
            using var response = await client.GetAsync("/ok");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await Task.Delay(100);
            Assert.Equal(0, repository.WriteCount);
        }

        repository.Reset();

        await using (var app = await BuildTestAppAsync(
                         new WebRequestAuditLogOptions {
                             Enabled = true,
                             SampleRate = 1,
                             IncludeRequestBody = false,
                             IncludeResponseBody = false,
                             MaxRequestBodyLength = 1024,
                             MaxResponseBodyLength = 1024
                         },
                         repository)) {
            using var client = app.GetTestClient();
            using var response = await client.GetAsync("/ok");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            await WaitForWriteCountAsync(repository, expectedCount: 1, maxAttempts: 20, delayMilliseconds: 50);
        }
    }

    /// <summary>
    /// 验证场景：正常请求写入热+冷模型关键字段。
    /// </summary>
    [Fact]
    public async Task Middleware_WithNormalRequest_ShouldWriteHotAndColdModelFields() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        await using var app = await BuildTestAppAsync(
            new WebRequestAuditLogOptions {
                Enabled = true,
                SampleRate = 1,
                IncludeRequestBody = true,
                IncludeResponseBody = true,
                MaxRequestBodyLength = 2048,
                MaxResponseBodyLength = 2048
            },
            repository);

        using var client = app.GetTestClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/echo?source=test");
        request.Headers.Add("X-Correlation-Id", "corr-test-001");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer abcdefghijklmnopqrstuvwxyz9876543210");
        request.Headers.TryAddWithoutValidation("User-Agent", "middleware-tests/1.0");
        request.Headers.TryAddWithoutValidation("X-Request-Id", "req-123");
        request.Headers.TryAddWithoutValidation("Cookie", "sessionid=secret-cookie");
        request.Headers.TryAddWithoutValidation("X-Api-Key", "secret-api-key");
        request.Content = JsonContent.Create(new { name = "zeye", count = 2 });

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("{\"result\":\"ok\"}", body);
        await WaitForWriteCountAsync(repository, expectedCount: 1, maxAttempts: 20, delayMilliseconds: 50);
        var log = Assert.Single(repository.Logs);
        Assert.Equal("POST", log.RequestMethod);
        Assert.Equal("http", log.RequestScheme);
        Assert.Equal("corr-test-001", log.CorrelationId);
        Assert.Equal("/echo", log.RequestPath);
        Assert.Equal("/echo", log.RequestRouteTemplate);
        Assert.Equal(StatusCodes.Status200OK, log.StatusCode);
        Assert.True(log.IsSuccess);
        Assert.False(log.HasException);
        Assert.True(log.Detail is not null);
        Assert.Contains("source=test", log.Detail!.RequestQueryString, StringComparison.Ordinal);
        Assert.Contains("application/json", log.Detail.RequestContentType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"name\":\"zeye\"", log.Detail.RequestBody, StringComparison.Ordinal);
        Assert.Contains("result", log.Detail.ResponseBody, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(log.Detail.RequestUrl));
        Assert.Equal("middleware-tests/1.0", log.Detail.UserAgent);

        var curl = log.Detail.CurlCommand;
        Assert.Contains("curl -X", curl, StringComparison.Ordinal);
        Assert.Contains("http://localhost/echo?source=test", curl, StringComparison.Ordinal);
        Assert.Contains("-H 'Content-Type: application/json", curl, StringComparison.Ordinal);
        Assert.Contains("-H 'Accept: application/json", curl, StringComparison.Ordinal);
        Assert.Contains("-H 'User-Agent: middleware-tests/1.0'", curl, StringComparison.Ordinal);
        Assert.Contains("-H 'Authorization: Bearer", curl, StringComparison.Ordinal);
        Assert.DoesNotContain("abcdefghijklmnopqrstuvwxyz9876543210", curl, StringComparison.Ordinal);
        Assert.DoesNotContain("sessionid=secret-cookie", curl, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-api-key", curl, StringComparison.Ordinal);
        Assert.Contains("-H 'X-Request-Id: req-123'", curl, StringComparison.Ordinal);
        Assert.Contains("--data-raw", curl, StringComparison.Ordinal);

        var bodyShellLiteral = ToSingleQuotedShellLiteral(log.Detail.RequestBody);
        Assert.Contains($"--data-raw {bodyShellLiteral}", curl, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：异常请求仍写审计且异常字段非空。
    /// </summary>
    [Fact]
    public async Task Middleware_WithExceptionRequest_ShouldWriteExceptionAuditFields() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        await using var app = await BuildTestAppAsync(
            new WebRequestAuditLogOptions {
                Enabled = true,
                SampleRate = 1,
                IncludeRequestBody = true,
                IncludeResponseBody = true,
                MaxRequestBodyLength = 2048,
                MaxResponseBodyLength = 2048
            },
            repository);

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/throw");
        var responseText = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var problemJson = JsonDocument.Parse(responseText);
        Assert.Equal("服务器内部错误", problemJson.RootElement.GetProperty("title").GetString());
        await WaitForWriteCountAsync(repository, expectedCount: 1, maxAttempts: 20, delayMilliseconds: 50);
        var log = Assert.Single(repository.Logs);
        Assert.True(log.HasException);
        Assert.True(log.Detail is not null);
        Assert.False(string.IsNullOrWhiteSpace(log.Detail!.ExceptionType));
        Assert.False(string.IsNullOrWhiteSpace(log.Detail.ErrorMessage));
    }

    /// <summary>
    /// 验证场景：请求/响应正文超长会截断并设置截断标记。
    /// </summary>
    [Fact]
    public async Task Middleware_WithOversizedBodies_ShouldTruncateAndMarkFlags() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        await using var app = await BuildTestAppAsync(
            new WebRequestAuditLogOptions {
                Enabled = true,
                SampleRate = 1,
                IncludeRequestBody = true,
                IncludeResponseBody = true,
                MaxRequestBodyLength = 16,
                MaxResponseBodyLength = 16
            },
            repository);

        using var client = app.GetTestClient();
        var oversizedRequest = new string('A', 128);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/echo-large");
        request.Content = new StringContent(oversizedRequest, Encoding.UTF8, "text/plain");
        using var response = await client.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("BBBB", responseText, StringComparison.Ordinal);

        await WaitForWriteCountAsync(repository, expectedCount: 1, maxAttempts: 20, delayMilliseconds: 50);
        var log = Assert.Single(repository.Logs);
        Assert.True(log.IsRequestBodyTruncated);
        Assert.True(log.IsResponseBodyTruncated);
        Assert.True(log.Detail is not null);
        // 请求体超限短路时仅标记截断，不采集正文内容。
        Assert.Equal(0, log.Detail!.RequestBody.Length);
        Assert.Equal(16, log.Detail.ResponseBody.Length);
    }

    /// <summary>
    /// 验证场景：multipart 二进制正文使用占位文本，且主请求成功。
    /// </summary>
    [Fact]
    public async Task Middleware_WithMultipartRequest_ShouldRecordBinaryPlaceholder() {
        var repository = new InMemoryWebRequestAuditLogRepository();
        await using var app = await BuildTestAppAsync(
            new WebRequestAuditLogOptions {
                Enabled = true,
                SampleRate = 1,
                IncludeRequestBody = true,
                IncludeResponseBody = true,
                MaxRequestBodyLength = 1024,
                MaxResponseBodyLength = 1024
            },
            repository);

        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/echo");
        request.Content = new MultipartFormDataContent {
            { new StringContent("text-part"), "name" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes("binary-part")), "file", "a.bin" }
        };
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await WaitForWriteCountAsync(repository, expectedCount: 1, maxAttempts: 20, delayMilliseconds: 50);
        var log = Assert.Single(repository.Logs);
        Assert.True(log.HasRequestBody);
        Assert.Equal("[binary payload omitted]", log.Detail?.RequestBody);
        Assert.Contains("--data-raw '[binary payload omitted]'", log.Detail?.CurlCommand ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：审计写入失败不影响主请求状态码与响应体。
    /// </summary>
    [Fact]
    public async Task Middleware_WhenAuditWriteFails_ShouldNotAffectMainResponse() {
        var repository = new InMemoryWebRequestAuditLogRepository {
            ShouldThrowException = true
        };
        await using var app = await BuildTestAppAsync(
            new WebRequestAuditLogOptions {
                Enabled = true,
                SampleRate = 1,
                IncludeRequestBody = false,
                IncludeResponseBody = true,
                MaxRequestBodyLength = 1024,
                MaxResponseBodyLength = 1024
            },
            repository);

        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/ok");
        var responseText = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("pong", responseText);
        await WaitForWriteCountAsync(repository, expectedCount: 1, maxAttempts: 20, delayMilliseconds: 50);
    }

    /// <summary>
    /// 验证场景：审计仓储慢写不阻塞主请求返回。
    /// </summary>
    [Fact]
    public async Task Middleware_WithSlowAuditWrite_ShouldNotBlockMainResponse() {
        var repository = new InMemoryWebRequestAuditLogRepository {
            AddDelayMilliseconds = 1200
        };
        await using var app = await BuildTestAppAsync(
            new WebRequestAuditLogOptions {
                Enabled = true,
                SampleRate = 1,
                IncludeRequestBody = false,
                IncludeResponseBody = false,
                MaxRequestBodyLength = 1024,
                MaxResponseBodyLength = 1024
            },
            repository);

        using var client = app.GetTestClient();
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync("/ok");
        var elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        var maxAllowedMilliseconds = Math.Max(400d, repository.AddDelayMilliseconds * 0.5d);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(elapsedMilliseconds < maxAllowedMilliseconds, $"主请求被审计写入阻塞，ElapsedMilliseconds={elapsedMilliseconds}, MaxAllowedMilliseconds={maxAllowedMilliseconds}");
        await WaitForWriteCountAsync(repository, expectedCount: 1, maxAttempts: 40, delayMilliseconds: 50);
    }

    /// <summary>
    /// 验证场景：中间件写入真实仓储时可落热表+冷表。
    /// </summary>
    [Fact]
    public async Task Middleware_WithRealRepository_ShouldPersistHotAndColdTables() {
        var databaseName = $"web-request-auditlog-middleware-{Guid.NewGuid():N}";
        var options = BuildInMemoryDbOptions(databaseName);
        var contextFactory = new SortingHubTestDbContextFactory(options);

        await using var app = await BuildTestAppWithRealRepositoryAsync(
            new WebRequestAuditLogOptions {
                Enabled = true,
                SampleRate = 1,
                IncludeRequestBody = true,
                IncludeResponseBody = true,
                MaxRequestBodyLength = 4096,
                MaxResponseBodyLength = 4096
            },
            contextFactory);

        try {
            using var client = app.GetTestClient();
            var request = new HttpRequestMessage(HttpMethod.Post, "/echo");
            request.Content = JsonContent.Create(new { id = 1, name = "persist" });
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var hot = await WaitForPersistedAuditLogAsync(options, maxAttempts: 20, delayMilliseconds: 100);
            Assert.Equal("POST", hot.RequestMethod);
            Assert.Equal(StatusCodes.Status200OK, hot.StatusCode);
            Assert.NotNull(hot.Detail);
            Assert.Contains("persist", hot.Detail!.RequestBody, StringComparison.Ordinal);
        }
        finally {
            await using var cleanupDb = new SortingHubDbContext(options);
            await cleanupDb.Database.EnsureDeletedAsync();
        }
    }

    /// <summary>
    /// 构建使用内存仓储的测试应用。
    /// </summary>
    /// <param name="options">中间件配置。</param>
    /// <param name="repository">内存仓储。</param>
    /// <returns>已启动应用。</returns>
    private static Task<WebApplication> BuildTestAppAsync(
        WebRequestAuditLogOptions options,
        InMemoryWebRequestAuditLogRepository repository) {
        return BuildAppAsync(
            options,
            services => {
                services.AddScoped<IWebRequestAuditLogRepository>(_ => repository);
                services.AddScoped<WriteWebRequestAuditLogCommandService>();
            });
    }

    /// <summary>
    /// 构建使用真实仓储的测试应用。
    /// </summary>
    /// <param name="middlewareOptions">中间件配置。</param>
    /// <param name="contextFactory">DbContext 工厂。</param>
    /// <returns>已启动应用。</returns>
    private static Task<WebApplication> BuildTestAppWithRealRepositoryAsync(
        WebRequestAuditLogOptions middlewareOptions,
        IDbContextFactory<SortingHubDbContext> contextFactory) {
        return BuildAppAsync(
            middlewareOptions,
            services => {
                services.AddSingleton(contextFactory);
                services.AddScoped<IWebRequestAuditLogRepository, WebRequestAuditLogRepository>();
                services.AddScoped<WriteWebRequestAuditLogCommandService>();
            });
    }

    /// <summary>
    /// 构建测试应用通用流程。
    /// </summary>
    /// <param name="options">中间件配置。</param>
    /// <param name="configureServices">服务注册动作。</param>
    /// <returns>已启动应用。</returns>
    private static async Task<WebApplication> BuildAppAsync(
        WebRequestAuditLogOptions options,
        Action<IServiceCollection> configureServices) {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddSingleton(new WebRequestAuditBackgroundQueue(Math.Max(1, options.BackgroundQueueCapacity)));
        builder.Services.AddHostedService<WebRequestAuditBackgroundWorkerHostedService>();
        configureServices(builder.Services);
        builder.Services.Configure<WebRequestAuditLogOptions>(configured => CopyOptions(options, configured));

        var app = builder.Build();
        app.UseWebRequestAuditLogging();
        ConfigureErrorHandling(app);
        ConfigureEndpoints(app);
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// 配置测试端点。
    /// </summary>
    /// <param name="app">应用对象。</param>
    private static void ConfigureEndpoints(WebApplication app) {
        app.MapGet("/ok", () => Results.Text("pong", "text/plain"));
        app.MapPost("/echo", async context => {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            _ = await reader.ReadToEndAsync();
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"result\":\"ok\"}");
        });
        app.MapPost("/echo-large", async context => {
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
            _ = await reader.ReadToEndAsync();
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(new string('B', 256));
        });
        app.MapGet("/throw", (HttpContext _) => throw new InvalidOperationException("middleware-test-exception"));
    }

    /// <summary>
    /// 配置异常处理。
    /// </summary>
    /// <param name="app">应用对象。</param>
    private static void ConfigureErrorHandling(WebApplication app) {
        app.UseExceptionHandler(exceptionHandlerApp => {
            exceptionHandlerApp.Run(async context => {
                var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                var detail = exceptionFeature?.Error.Message ?? "unknown";
                if (!context.Response.HasStarted) {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/problem+json";
                    var payload = JsonSerializer.Serialize(new { title = "服务器内部错误", detail });
                    await context.Response.WriteAsync(payload);
                }
            });
        });
    }

    /// <summary>
    /// 复制中间件配置。
    /// </summary>
    /// <param name="source">来源配置。</param>
    /// <param name="target">目标配置。</param>
    private static void CopyOptions(WebRequestAuditLogOptions source, WebRequestAuditLogOptions target) {
        target.Enabled = source.Enabled;
        target.SampleRate = source.SampleRate;
        target.IncludeRequestBody = source.IncludeRequestBody;
        target.IncludeResponseBody = source.IncludeResponseBody;
        target.MaxRequestBodyLength = source.MaxRequestBodyLength;
        target.MaxResponseBodyLength = source.MaxResponseBodyLength;
    }

    /// <summary>
    /// 等待审计日志落库并返回热冷聚合。
    /// </summary>
    /// <param name="options">DbContext 选项。</param>
    /// <param name="maxAttempts">最大重试次数。</param>
    /// <param name="delayMilliseconds">重试间隔毫秒。</param>
    /// <returns>审计日志聚合。</returns>
    private static async Task<WebRequestAuditLog> WaitForPersistedAuditLogAsync(
        DbContextOptions<SortingHubDbContext> options,
        int maxAttempts,
        int delayMilliseconds) {
        for (var attempt = 0; attempt < maxAttempts; attempt++) {
            await using var db = new SortingHubDbContext(options);
            var hot = await db.Set<WebRequestAuditLog>()
                .Include(x => x.Detail)
                .SingleOrDefaultAsync();
            if (hot is not null) {
                return hot;
            }

            await Task.Delay(delayMilliseconds);
        }

        throw new InvalidOperationException("等待审计日志落库超时");
    }

    /// <summary>
    /// 等待写入次数达到预期值。
    /// </summary>
    /// <param name="repository">内存仓储。</param>
    /// <param name="expectedCount">期望写入次数。</param>
    /// <param name="maxAttempts">最大重试次数。</param>
    /// <param name="delayMilliseconds">重试间隔毫秒。</param>
    private static async Task WaitForWriteCountAsync(
        InMemoryWebRequestAuditLogRepository repository,
        int expectedCount,
        int maxAttempts,
        int delayMilliseconds) {
        for (var attempt = 0; attempt < maxAttempts; attempt++) {
            if (repository.WriteCount >= expectedCount) {
                return;
            }

            await Task.Delay(delayMilliseconds);
        }

        Assert.Equal(expectedCount, repository.WriteCount);
    }

    /// <summary>
    /// 构建 InMemory DbContext 选项。
    /// </summary>
    /// <param name="databaseName">数据库名。</param>
    /// <returns>DbContext 选项。</returns>
    private static DbContextOptions<SortingHubDbContext> BuildInMemoryDbOptions(string databaseName) {
        return new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    /// <summary>
    /// 构建 shell 单引号字面量。
    /// </summary>
    /// <param name="value">原始文本。</param>
    /// <returns>shell 单引号字面量。</returns>
    private static string ToSingleQuotedShellLiteral(string value) {
        if (string.IsNullOrEmpty(value)) {
            return "''";
        }

        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }
}
