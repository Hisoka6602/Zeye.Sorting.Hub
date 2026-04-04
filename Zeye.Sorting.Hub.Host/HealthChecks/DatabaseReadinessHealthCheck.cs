using Microsoft.Extensions.Diagnostics.HealthChecks;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 数据库就绪探针：验证数据库连接是否可用，用于 /health/ready 就绪端点。
/// 探针失败不影响存活探针（/health/live），仅表示实例暂不接受流量。
/// </summary>
public sealed class DatabaseReadinessHealthCheck : IHealthCheck {
    /// <summary>
    /// NLog 静态日志器实例，用于输出健康检查异常。
    /// </summary>
    private static readonly NLog.ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// DI 作用域工厂，用于按次解析 DbContext。
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// 初始化 <see cref="DatabaseReadinessHealthCheck"/>。
    /// </summary>
    /// <param name="scopeFactory">DI 作用域工厂。</param>
    public DatabaseReadinessHealthCheck(IServiceScopeFactory scopeFactory) {
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        try {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<SortingHubDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("数据库连接正常。")
                : HealthCheckResult.Unhealthy("数据库连接不可用。");
        }
        catch (Exception ex) {
            Logger.Error(ex, "数据库就绪探针检查失败");
            return HealthCheckResult.Unhealthy($"数据库就绪探针异常：{ex.Message}", ex);
        }
    }
}
