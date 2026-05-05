using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Services.DataGovernance;
using Zeye.Sorting.Hub.Contracts.Models.DataGovernance;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;
using Zeye.Sorting.Hub.Domain.Enums.AuditLogs;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 数据归档 dry-run 回归测试。
/// </summary>
public sealed class DataArchiveTaskTests {
    /// <summary>
    /// 验证场景：创建任务后由 Worker 完成 dry-run 计划。
    /// </summary>
    [Fact]
    public async Task CreateArchiveTask_AndWorkerRun_ShouldCompleteDryRunPlan() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        using var createResponse = await client.PostAsJsonAsync("/api/data-governance/archive-tasks", new ArchiveTaskCreateRequest {
            TaskType = "WebRequestAuditLogHistory",
            RetentionDays = 30,
            RequestedBy = "archive-test",
            Remark = "首轮 dry-run"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ArchiveTaskResponse>();
        Assert.NotNull(created);
        Assert.Equal("Pending", created.Status);

        using var scope = app.Services.CreateScope();
        var worker = scope.ServiceProvider.GetRequiredService<DataArchiveHostedWorker>();
        var handled = await worker.RunOnceAsync(CancellationToken.None);

        Assert.True(handled);
        var repository = scope.ServiceProvider.GetRequiredService<IArchiveTaskRepository>();
        var reloaded = await repository.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(reloaded);
        Assert.Equal(Zeye.Sorting.Hub.Domain.Enums.DataGovernance.ArchiveTaskStatus.Completed, reloaded!.Status);
        Assert.True(reloaded.PlannedItemCount >= 1);
        Assert.Contains("dry-run 计划完成", reloaded.PlanSummary, StringComparison.Ordinal);
        Assert.NotEmpty(reloaded.CheckpointPayload);
        LocalTimeTestConstraint.AssertIsLocalTime(reloaded.CreatedAt);
        LocalTimeTestConstraint.AssertIsLocalTime(reloaded.UpdatedAt);
        Assert.NotNull(reloaded.CompletedAt);
        LocalTimeTestConstraint.AssertIsLocalTime(reloaded.CompletedAt!.Value);
    }

    /// <summary>
    /// 验证场景：分页接口返回创建后的任务列表。
    /// </summary>
    [Fact]
    public async Task GetArchiveTaskList_ShouldReturnCreatedTasks() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        await client.PostAsJsonAsync("/api/data-governance/archive-tasks", new ArchiveTaskCreateRequest {
            TaskType = "WebRequestAuditLogHistory",
            RetentionDays = 15,
            RequestedBy = "list-test"
        });

        using var response = await client.GetAsync("/api/data-governance/archive-tasks?pageNumber=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ArchiveTaskListResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.TotalCount >= 1);
        Assert.Contains(payload.Items, item => item.TaskType == "WebRequestAuditLogHistory");
    }

    /// <summary>
    /// 验证场景：已完成任务允许重试并回到 Pending。
    /// </summary>
    [Fact]
    public async Task RetryArchiveTask_WhenCompleted_ShouldReturnPending() {
        await using var app = await BuildTestAppAsync();
        using var scope = app.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IArchiveTaskRepository>();
        var task = ArchiveTask.CreateDryRun(Zeye.Sorting.Hub.Domain.Enums.DataGovernance.ArchiveTaskType.WebRequestAuditLogHistory, 7, "retry-test", null);
        await repository.AddAsync(task, CancellationToken.None);
        task.MarkCompleted(3, "done", "{}");
        await repository.UpdateAsync(task, CancellationToken.None);
        using var client = app.GetTestClient();

        using var response = await client.PostAsync($"/api/data-governance/archive-tasks/{task.Id}/retry", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ArchiveTaskResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Pending", payload.Status);
        Assert.Equal(1, payload.RetryCount);
    }

    /// <summary>
    /// 验证场景：保留天数超过合同范围时返回 400。
    /// </summary>
    [Fact]
    public async Task CreateArchiveTask_WithTooLargeRetentionDays_ShouldReturnBadRequest() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/api/data-governance/archive-tasks", new ArchiveTaskCreateRequest {
            TaskType = "WebRequestAuditLogHistory",
            RetentionDays = ArchiveTask.MaxRetentionDays + 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：非法任务类型返回 400。
    /// </summary>
    [Fact]
    public async Task CreateArchiveTask_WithInvalidTaskType_ShouldReturnBadRequest() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/api/data-governance/archive-tasks", new ArchiveTaskCreateRequest {
            TaskType = "UnknownType",
            RetentionDays = 30
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// 验证场景：同一待执行任务只能被领取一次。
    /// </summary>
    [Fact]
    public async Task TryAcquireNextPendingAsync_WhenCalledTwice_ShouldOnlyAcquireOnce() {
        await using var app = await BuildTestAppAsync();
        using var scope = app.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IArchiveTaskRepository>();
        var archiveTask = ArchiveTask.CreateDryRun(
            Zeye.Sorting.Hub.Domain.Enums.DataGovernance.ArchiveTaskType.WebRequestAuditLogHistory,
            7,
            "acquire-test",
            null);
        await repository.AddAsync(archiveTask, CancellationToken.None);

        var firstAcquire = await repository.TryAcquireNextPendingAsync(CancellationToken.None);
        var secondAcquire = await repository.TryAcquireNextPendingAsync(CancellationToken.None);

        Assert.NotNull(firstAcquire);
        Assert.Equal(Zeye.Sorting.Hub.Domain.Enums.DataGovernance.ArchiveTaskStatus.Running, firstAcquire!.Status);
        Assert.Null(secondAcquire);
    }

    /// <summary>
    /// 验证场景：任务失败后仍记录终态完成时间。
    /// </summary>
    [Fact]
    public void ArchiveTask_MarkFailed_ShouldSetTerminalCompletedAt() {
        var archiveTask = ArchiveTask.CreateDryRun(
            Zeye.Sorting.Hub.Domain.Enums.DataGovernance.ArchiveTaskType.WebRequestAuditLogHistory,
            7,
            "failure-test",
            null);

        archiveTask.MarkFailed("dry-run 失败");

        Assert.NotNull(archiveTask.CompletedAt);
        LocalTimeTestConstraint.AssertIsLocalTime(archiveTask.CompletedAt!.Value);
        Assert.Equal(archiveTask.CompletedAt, archiveTask.UpdatedAt);
    }

    /// <summary>
    /// 构建测试应用。
    /// </summary>
    /// <returns>已启动应用。</returns>
    private static async Task<WebApplication> BuildTestAppAsync() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddAuthorization();
        builder.Services.AddDbContextFactory<SortingHubDbContext>(options =>
            options.UseInMemoryDatabase($"archive-tests-{Guid.NewGuid():N}"));
        builder.Services.AddOptions<DataArchiveOptions>()
            .Configure(options => {
                options.IsEnabled = true;
                options.WorkerPollIntervalSeconds = 1;
                options.SampleItemLimit = 3;
            });
        builder.Services.AddScoped<IArchiveTaskRepository, ArchiveTaskRepository>();
        builder.Services.AddScoped<DataArchivePlanner>();
        builder.Services.AddScoped<DataArchiveCheckpointStore>();
        builder.Services.AddScoped<DataArchiveExecutor>();
        builder.Services.AddScoped<DataArchiveHostedWorker>();
        builder.Services.AddScoped<CreateArchiveTaskCommandService>();
        builder.Services.AddScoped<GetArchiveTaskPagedQueryService>();
        builder.Services.AddScoped<RetryArchiveTaskCommandService>();
        var app = builder.Build();
        app.UseAuthorization();
        app.MapDataGovernanceApis();
        await app.StartAsync();

        await SeedAuditLogsAsync(app.Services, CancellationToken.None);
        return app;
    }

    /// <summary>
    /// 预置可用于 dry-run 计划的数据。
    /// </summary>
    /// <param name="serviceProvider">服务提供器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    private static async Task SeedAuditLogsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken) {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<SortingHubDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.Set<WebRequestAuditLog>().AddRange(
            CreateAuditLog(7001, LocalTimeTestConstraint.CreateLocalTime(2026, 1, 1, 8, 0, 0)),
            CreateAuditLog(7002, LocalTimeTestConstraint.CreateLocalTime(2026, 2, 1, 8, 0, 0)),
            CreateAuditLog(7003, DateTime.Now.AddDays(-2)));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// 构建审计日志实体。
    /// </summary>
    /// <param name="id">主键。</param>
    /// <param name="startedAt">开始时间。</param>
    /// <returns>审计日志实体。</returns>
    private static WebRequestAuditLog CreateAuditLog(long id, DateTime startedAt) {
        return new WebRequestAuditLog {
            Id = id,
            TraceId = $"trace-{id}",
            CorrelationId = $"corr-{id}",
            SpanId = $"span-{id}",
            OperationName = "Archive.Test",
            RequestMethod = "GET",
            RequestScheme = "http",
            RequestHost = "localhost",
            RequestPath = "/api/archive-test",
            RequestRouteTemplate = "/api/archive-test",
            UserName = "tester",
            RequestPayloadType = WebRequestPayloadType.Json,
            ResponsePayloadType = WebResponsePayloadType.Json,
            ResourceId = id.ToString(),
            AuditResourceType = AuditResourceType.BusinessObject,
            StartedAt = startedAt,
            EndedAt = startedAt.AddSeconds(1),
            CreatedAt = startedAt,
            DurationMs = 100,
            StatusCode = 200,
            IsSuccess = true
        };
    }
}
