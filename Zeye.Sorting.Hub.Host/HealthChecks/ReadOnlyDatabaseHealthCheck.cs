using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 报表查询只读数据库健康检查。
/// </summary>
public sealed class ReadOnlyDatabaseHealthCheck : IHealthCheck {
    /// <summary>
    /// 只读上下文选择器。
    /// </summary>
    private readonly ReadOnlyDbContextFactorySelector _readOnlyDbContextFactorySelector;

    /// <summary>
    /// 初始化报表查询只读数据库健康检查。
    /// </summary>
    /// <param name="readOnlyDbContextFactorySelector">只读上下文选择器。</param>
    public ReadOnlyDatabaseHealthCheck(ReadOnlyDbContextFactorySelector readOnlyDbContextFactorySelector) {
        _readOnlyDbContextFactorySelector = readOnlyDbContextFactorySelector ?? throw new ArgumentNullException(nameof(readOnlyDbContextFactorySelector));
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var probe = await _readOnlyDbContextFactorySelector.ProbeRouteAsync(cancellationToken);
        var data = BuildHealthData(probe);
        if (!probe.IsEnabled) {
            return HealthCheckResult.Healthy(probe.Summary, data: data);
        }

        if (probe.IsReadOnlyAvailable) {
            return HealthCheckResult.Healthy(probe.Summary, data: data);
        }

        return probe.IsFallbackToPrimary
            ? HealthCheckResult.Degraded(probe.Summary, data: data)
            : HealthCheckResult.Unhealthy(probe.Summary, data: data);
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="probe">探测结果。</param>
    /// <returns>附加数据。</returns>
    private static IReadOnlyDictionary<string, object> BuildHealthData((bool IsEnabled, bool IsReadOnlyConfigured, bool IsReadOnlyAvailable, bool IsFallbackToPrimary, string RouteTarget, string Summary, string? ReadOnlyConnectionString) probe) {
        return new Dictionary<string, object> {
            ["isEnabled"] = probe.IsEnabled,
            ["isReadOnlyConfigured"] = probe.IsReadOnlyConfigured,
            ["isReadOnlyAvailable"] = probe.IsReadOnlyAvailable,
            ["isFallbackToPrimary"] = probe.IsFallbackToPrimary,
            ["routeTarget"] = probe.RouteTarget
        };
    }
}
