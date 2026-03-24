using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Services.AuditLogs;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Enums.AuditLogs;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// WebRequestAuditLog 仓储写入测试。
/// </summary>
public sealed class WebRequestAuditLogRepositoryTests {
    /// <summary>
    /// 验证场景：IWebRequestAuditLogRepository_ShouldResolveFromDependencyInjection。
    /// </summary>
    [Fact]
    public void IWebRequestAuditLogRepository_ShouldResolveFromDependencyInjection() {
        var databaseName = $"web-request-auditlog-repo-di-{Guid.NewGuid():N}";
        var options = BuildOptions(databaseName);
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<SortingHubDbContext>>(new SortingHubTestDbContextFactory(options));
        services.AddScoped<IWebRequestAuditLogRepository, WebRequestAuditLogRepository>();
        services.AddScoped<WriteWebRequestAuditLogCommandService>();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        _ = scope.ServiceProvider.GetRequiredService<IWebRequestAuditLogRepository>();
        _ = scope.ServiceProvider.GetRequiredService<WriteWebRequestAuditLogCommandService>();
    }

    /// <summary>
    /// 验证场景：AddAsync_ShouldPersistHotAndColdInSingleWritePath。
    /// </summary>
    [Fact]
    public async Task AddAsync_ShouldPersistHotAndColdInSingleWritePath() {
        var databaseName = $"web-request-auditlog-repo-{Guid.NewGuid():N}";
        var repository = CreateRepository(databaseName);
        var startedAt = DateTime.Now;
        var auditLog = BuildAuditLog(startedAt, "trace-hot-cold");

        try {
            var result = await repository.AddAsync(auditLog, CancellationToken.None);

            Assert.True(result.IsSuccess, result.ErrorMessage);

            var options = BuildOptions(databaseName);
            await using var db = new SortingHubDbContext(options);
            var hot = await db.Set<WebRequestAuditLog>()
                .Include(x => x.Detail)
                .SingleAsync(x => x.TraceId == "trace-hot-cold");
            Assert.Equal(startedAt, hot.StartedAt);
            Assert.NotNull(hot.Detail);
            Assert.Equal(startedAt, hot.Detail!.StartedAt);
            Assert.Equal("https://localhost/api/parcels", hot.Detail.RequestUrl);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：WriteService_ShouldCallRepositoryAndPersist。
    /// </summary>
    [Fact]
    public async Task WriteService_ShouldCallRepositoryAndPersist() {
        var databaseName = $"web-request-auditlog-service-{Guid.NewGuid():N}";
        var options = BuildOptions(databaseName);
        var services = new ServiceCollection();
        services.AddSingleton<IDbContextFactory<SortingHubDbContext>>(new SortingHubTestDbContextFactory(options));
        services.AddScoped<IWebRequestAuditLogRepository, WebRequestAuditLogRepository>();
        services.AddScoped<WriteWebRequestAuditLogCommandService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<WriteWebRequestAuditLogCommandService>();
        var startedAt = DateTime.Now;
        var auditLog = BuildAuditLog(startedAt, "trace-write-service");

        try {
            var result = await service.WriteAsync(auditLog, CancellationToken.None);
            Assert.True(result.IsSuccess, result.ErrorMessage);

            await using var db = new SortingHubDbContext(options);
            var hotCount = await db.Set<WebRequestAuditLog>().CountAsync(x => x.TraceId == "trace-write-service");
            var coldCount = await db.Set<WebRequestAuditLogDetail>()
                .CountAsync(x => x.RequestUrl == "https://localhost/api/parcels");
            Assert.Equal(1, hotCount);
            Assert.Equal(1, coldCount);
        }
        finally {
            await CleanupDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// 验证场景：GetWebRequestAuditLogPerDayShardingEntityTypes_ShouldContainHotAndColdTypes。
    /// </summary>
    [Fact]
    public void GetWebRequestAuditLogPerDayShardingEntityTypes_ShouldContainHotAndColdTypes() {
        var entityTypes = PersistenceServiceCollectionExtensions.GetWebRequestAuditLogPerDayShardingEntityTypes();
        Assert.Contains(typeof(WebRequestAuditLog), entityTypes);
        Assert.Contains(typeof(WebRequestAuditLogDetail), entityTypes);
        Assert.Equal(2, entityTypes.Count);
    }

    /// <summary>
    /// 构建仓储实例。
    /// </summary>
    /// <param name="databaseName">数据库名。</param>
    /// <returns>仓储实例。</returns>
    private static WebRequestAuditLogRepository CreateRepository(string databaseName) {
        var options = BuildOptions(databaseName);
        var factory = new SortingHubTestDbContextFactory(options);
        return new WebRequestAuditLogRepository(factory);
    }

    /// <summary>
    /// 构建测试审计日志。
    /// </summary>
    /// <param name="startedAt">开始时间。</param>
    /// <param name="traceId">TraceId。</param>
    /// <returns>审计日志聚合。</returns>
    private static WebRequestAuditLog BuildAuditLog(DateTime startedAt, string traceId) {
        return new WebRequestAuditLog {
            TraceId = traceId,
            CorrelationId = "corr-001",
            SpanId = "span-001",
            OperationName = "Parcel.Create",
            RequestMethod = "POST",
            RequestScheme = "https",
            RequestHost = "localhost",
            RequestPort = 443,
            RequestPath = "/api/parcels",
            RequestRouteTemplate = "/api/parcels",
            UserId = 1001,
            UserName = "tester",
            IsAuthenticated = true,
            TenantId = 2001,
            RequestPayloadType = WebRequestPayloadType.Json,
            RequestSizeBytes = 256,
            HasRequestBody = true,
            IsRequestBodyTruncated = false,
            ResponsePayloadType = WebResponsePayloadType.Json,
            ResponseSizeBytes = 512,
            HasResponseBody = true,
            IsResponseBodyTruncated = false,
            StatusCode = 200,
            IsSuccess = true,
            HasException = false,
            AuditResourceType = AuditResourceType.Api,
            ResourceId = "parcel-1",
            StartedAt = startedAt,
            EndedAt = startedAt.AddMilliseconds(120),
            DurationMs = 120,
            CreatedAt = startedAt,
            Detail = new WebRequestAuditLogDetail {
                StartedAt = startedAt,
                RequestUrl = "https://localhost/api/parcels",
                RequestQueryString = string.Empty,
                RequestHeadersJson = "{}",
                ResponseHeadersJson = "{}",
                RequestContentType = "application/json",
                ResponseContentType = "application/json",
                Accept = "application/json",
                Referer = string.Empty,
                Origin = "https://localhost",
                AuthorizationType = "Bearer",
                UserAgent = "xunit",
                RequestBody = "{\"id\":1}",
                ResponseBody = "{\"ok\":true}",
                CurlCommand = "curl -X POST",
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
                DatabaseOperationSummary = "INSERT Parcels",
                HasDatabaseAccess = true,
                DatabaseAccessCount = 1,
                DatabaseDurationMs = 10,
                ResourceCode = "parcel.create",
                ResourceName = "包裹创建",
                ActionDurationMs = 80,
                MiddlewareDurationMs = 40,
                Tags = "api,parcel",
                ExtraPropertiesJson = "{}",
                Remark = "test"
            }
        };
    }

    /// <summary>
    /// 构建 InMemory DbContext 选项。
    /// </summary>
    /// <param name="databaseName">数据库名。</param>
    /// <returns>选项对象。</returns>
    private static DbContextOptions<SortingHubDbContext> BuildOptions(string databaseName) {
        return new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
    }

    /// <summary>
    /// 清理测试数据库。
    /// </summary>
    /// <param name="databaseName">数据库名。</param>
    /// <returns>异步任务。</returns>
    private static async Task CleanupDatabaseAsync(string databaseName) {
        var options = BuildOptions(databaseName);
        await using var db = new SortingHubDbContext(options);
        await db.Database.EnsureDeletedAsync();
    }
}
