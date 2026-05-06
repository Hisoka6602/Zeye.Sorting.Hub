using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Application.Services.DataGovernance;
using Zeye.Sorting.Hub.Application.Services.Events;
using Zeye.Sorting.Hub.Contracts.Models.Events;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;
using Zeye.Sorting.Hub.Domain.Enums.Events;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Repositories;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Outbox 事件底座回归测试。
/// </summary>
public sealed class OutboxMessageTests {
    /// <summary>
    /// 验证场景：创建 Outbox 消息成功并返回 Pending。
    /// </summary>
    [Fact]
    public async Task AppendOutboxMessage_WithValidRequest_ShouldReturnCreated() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/api/data-governance/outbox-messages", new OutboxMessageCreateRequest {
            EventType = "ParcelCreated",
            PayloadJson = "{\"parcelId\":1001,\"occurredAt\":\"2026-05-06 08:00:00\"}"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<OutboxMessageResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Pending", payload.Status);
        Assert.Equal("ParcelCreated", payload.EventType);
    }

    /// <summary>
    /// 验证场景：分页接口可以返回刚创建的 Outbox 消息。
    /// </summary>
    [Fact]
    public async Task GetOutboxMessageList_ShouldReturnCreatedMessage() {
        await using var app = await BuildTestAppAsync();
        using var client = app.GetTestClient();
        await client.PostAsJsonAsync("/api/data-governance/outbox-messages", new OutboxMessageCreateRequest {
            EventType = "ParcelCreated",
            PayloadJson = "{\"parcelId\":1002}"
        });

        using var response = await client.GetAsync("/api/data-governance/outbox-messages?pageNumber=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<OutboxMessageListResponse>();
        Assert.NotNull(payload);
        Assert.True(payload.TotalCount >= 1);
        Assert.Contains(payload.Items, item => item.EventType == "ParcelCreated");
    }

    /// <summary>
    /// 验证场景：后台派发单轮执行后会把消息推进到成功态。
    /// </summary>
    [Fact]
    public async Task OutboxDispatchHostedService_RunOnce_ShouldMarkMessageSucceeded() {
        await using var app = await BuildTestAppAsync();
        using var scope = app.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var message = OutboxMessage.CreatePending("ParcelCreated", "{\"parcelId\":1003}");
        await repository.AddAsync(message, CancellationToken.None);
        var hostedService = scope.ServiceProvider.GetRequiredService<OutboxDispatchHostedService>();

        var handledCount = await hostedService.RunOnceAsync(CancellationToken.None);
        var reloaded = await repository.GetByIdAsync(message.Id, CancellationToken.None);

        Assert.Equal(1, handledCount);
        Assert.NotNull(reloaded);
        Assert.Equal(OutboxMessageStatus.Succeeded, reloaded!.Status);
        Assert.NotNull(reloaded.CompletedAt);
        LocalTimeTestConstraint.AssertIsLocalTime(reloaded.CompletedAt!.Value);
    }

    /// <summary>
    /// 验证场景：非法 JSON 载荷在多次派发失败后会进入死信。
    /// </summary>
    [Fact]
    public async Task OutboxDispatchHostedService_WithInvalidJson_ShouldDeadLetterAfterMaxRetryCount() {
        await using var app = await BuildTestAppAsync();
        using var scope = app.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var hostedService = scope.ServiceProvider.GetRequiredService<OutboxDispatchHostedService>();
        var message = OutboxMessage.CreatePending("ParcelCreated", "not-json");
        await repository.AddAsync(message, CancellationToken.None);

        await hostedService.RunOnceAsync(CancellationToken.None);
        await hostedService.RunOnceAsync(CancellationToken.None);
        var reloaded = await repository.GetByIdAsync(message.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(OutboxMessageStatus.DeadLettered, reloaded!.Status);
        Assert.Equal(2, reloaded.RetryCount);
        Assert.Contains("合法 JSON", reloaded.FailureMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：存在死信消息时健康检查返回 Unhealthy。
    /// </summary>
    [Fact]
    public async Task OutboxHealthCheck_WhenDeadLetterExists_ShouldReturnUnhealthy() {
        await using var app = await BuildTestAppAsync();
        using var scope = app.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxMessageRepository>();
        var healthCheck = scope.ServiceProvider.GetRequiredService<OutboxHealthCheck>();
        var message = OutboxMessage.CreatePending("ParcelCreated", "not-json");
        await repository.AddAsync(message, CancellationToken.None);
        var hostedService = scope.ServiceProvider.GetRequiredService<OutboxDispatchHostedService>();
        await hostedService.RunOnceAsync(CancellationToken.None);
        await hostedService.RunOnceAsync(CancellationToken.None);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.True(result.Data.ContainsKey("deadLetteredCount"));
    }

    /// <summary>
    /// 构建测试应用。
    /// </summary>
    /// <returns>已启动应用。</returns>
    private static async Task<WebApplication> BuildTestAppAsync() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Outbox:Dispatch:BatchSize"] = "1",
            ["Outbox:Dispatch:MaxRetryCount"] = "2",
            ["Outbox:Dispatch:PollIntervalSeconds"] = "1"
        });
        builder.Services.AddProblemDetails();
        builder.Services.AddAuthorization();
        builder.Services.AddDbContextFactory<SortingHubDbContext>(options =>
            options.UseInMemoryDatabase($"outbox-tests-{Guid.NewGuid():N}"));
        builder.Services.AddScoped<CreateArchiveTaskCommandService>();
        builder.Services.AddScoped<GetArchiveTaskPagedQueryService>();
        builder.Services.AddScoped<RetryArchiveTaskCommandService>();
        builder.Services.AddScoped<IOutboxMessageRepository, OutboxMessageRepository>();
        builder.Services.AddScoped<AppendOutboxMessageCommandService>();
        builder.Services.AddScoped<GetOutboxMessagePagedQueryService>();
        builder.Services.AddScoped<DispatchOutboxMessageCommandService>();
        builder.Services.AddSingleton<OutboxDispatchHostedService>();
        builder.Services.AddHostedService(static serviceProvider => serviceProvider.GetRequiredService<OutboxDispatchHostedService>());
        builder.Services.AddSingleton<OutboxHealthCheck>();
        var app = builder.Build();
        app.UseAuthorization();
        app.MapDataGovernanceApis();
        await app.StartAsync();
        return app;
    }
}
