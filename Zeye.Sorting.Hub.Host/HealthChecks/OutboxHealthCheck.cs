using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Application.Services.Events;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// Outbox 健康检查。
/// </summary>
public sealed class OutboxHealthCheck : IHealthCheck {
    /// <summary>
    /// 活动消息允许的最大积压时长。
    /// </summary>
    private static readonly TimeSpan MaxActiveAge = TimeSpan.FromMinutes(15);

    /// <summary>
    /// 服务作用域工厂。
    /// </summary>
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// 初始化 Outbox 健康检查。
    /// </summary>
    /// <param name="serviceScopeFactory">服务作用域工厂。</param>
    public OutboxHealthCheck(IServiceScopeFactory serviceScopeFactory) {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var queryService = scope.ServiceProvider.GetRequiredService<GetOutboxMessagePagedQueryService>();
        var snapshot = await queryService.GetHealthSnapshotAsync(cancellationToken);
        var data = BuildHealthData(snapshot);
        if (snapshot.DeadLetteredCount > 0) {
            return HealthCheckResult.Unhealthy("Outbox 队列存在死信消息。", data: data);
        }

        if (snapshot.FailedCount > 0 || snapshot.ProcessingCount > 0) {
            return HealthCheckResult.Degraded("Outbox 队列存在失败重试或处理中消息。", data: data);
        }

        // `OldestActiveCreatedAt` 与 `DateTime.Now` 均为本地时间语义，可直接比较积压时长。
        if (snapshot.OldestActiveCreatedAt.HasValue && DateTime.Now - snapshot.OldestActiveCreatedAt.Value > MaxActiveAge) {
            return HealthCheckResult.Degraded("Outbox 队列存在长时间未处理积压。", data: data);
        }

        return HealthCheckResult.Healthy("Outbox 队列正常。", data: data);
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="snapshot">健康快照。</param>
    /// <returns>附加数据字典。</returns>
    private static IReadOnlyDictionary<string, object> BuildHealthData(Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels.OutboxMessageHealthSnapshotReadModel snapshot) {
        var data = new Dictionary<string, object> {
            ["pendingCount"] = snapshot.PendingCount,
            ["processingCount"] = snapshot.ProcessingCount,
            ["failedCount"] = snapshot.FailedCount,
            ["deadLetteredCount"] = snapshot.DeadLetteredCount
        };
        if (snapshot.OldestActiveCreatedAt.HasValue) {
            data["oldestActiveCreatedAt"] = snapshot.OldestActiveCreatedAt.Value.ToString(HealthCheckResponseWriter.LocalDateTimeFormat);
        }

        return data;
    }
}
