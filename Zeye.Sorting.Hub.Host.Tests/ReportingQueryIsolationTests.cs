using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 报表查询隔离测试。
/// </summary>
public sealed class ReportingQueryIsolationTests {
    /// <summary>
    /// 报表查询时间范围超限时应拒绝执行。
    /// </summary>
    [Fact]
    public void ReportingQueryGuard_WhenTimeRangeExceeded_ShouldThrow() {
        var guard = CreateGuard(maxReportTimeRangeDays: 31, maxReportRows: 10000);

        Assert.Throws<InvalidOperationException>(() => guard.BuildBudget(
            rangeStartLocal: LocalTimeTestConstraint.CreateLocalTime(2026, 4, 1, 8, 0, 0),
            rangeEndLocal: LocalTimeTestConstraint.CreateLocalTime(2026, 5, 5, 8, 0, 0),
            requestedRows: 100,
            includeTotalCount: false));
    }

    /// <summary>
    /// 报表查询预算应裁剪行数并关闭总数统计。
    /// </summary>
    [Fact]
    public void ReportingQueryGuard_WhenRequestedRowsExceedBudget_ShouldClampRowsAndDisableTotalCount() {
        var guard = CreateGuard(maxReportTimeRangeDays: 31, maxReportRows: 5000);

        var budget = guard.BuildBudget(
            rangeStartLocal: LocalTimeTestConstraint.CreateLocalTime(2026, 5, 1, 8, 0, 0),
            rangeEndLocal: LocalTimeTestConstraint.CreateLocalTime(2026, 5, 7, 8, 0, 0),
            requestedRows: 8000,
            includeTotalCount: true);

        Assert.Equal(5000, budget.RowLimit);
        Assert.False(budget.IncludeTotalCount);
    }

    /// <summary>
    /// 未启用只读数据库时应保持健康并使用主库路由。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task ReadOnlyDatabaseHealthCheck_WhenDisabled_ShouldReturnHealthy() {
        var serviceProvider = BuildServiceProvider(isEnabled: false, fallbackToPrimaryWhenUnavailable: false, includeReadOnlyConnectionString: false);
        var selector = serviceProvider.GetRequiredService<ReadOnlyDbContextFactorySelector>();
        var healthCheck = serviceProvider.GetRequiredService<ReadOnlyDatabaseHealthCheck>();

        var probe = await selector.ProbeRouteAsync(CancellationToken.None);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.False(probe.IsEnabled);
        Assert.Equal("Primary", probe.RouteTarget);
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    /// <summary>
    /// 只读连接字符串缺失且允许回退时应降级并回退主库。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task ReadOnlyDatabaseHealthCheck_WhenConnectionStringMissingAndFallbackEnabled_ShouldReturnDegraded() {
        var serviceProvider = BuildServiceProvider(isEnabled: true, fallbackToPrimaryWhenUnavailable: true, includeReadOnlyConnectionString: false);
        var selector = serviceProvider.GetRequiredService<ReadOnlyDbContextFactorySelector>();
        var healthCheck = serviceProvider.GetRequiredService<ReadOnlyDatabaseHealthCheck>();

        var probe = await selector.ProbeRouteAsync(CancellationToken.None);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.True(probe.IsFallbackToPrimary);
        Assert.Equal("Primary", probe.RouteTarget);
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    /// <summary>
    /// 只读连接字符串缺失且禁止回退时应拒绝报表查询。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task ReadOnlyDbContextFactorySelector_WhenConnectionStringMissingAndFallbackDisabled_ShouldThrow() {
        var serviceProvider = BuildServiceProvider(isEnabled: true, fallbackToPrimaryWhenUnavailable: false, includeReadOnlyConnectionString: false);
        var selector = serviceProvider.GetRequiredService<ReadOnlyDbContextFactorySelector>();
        var healthCheck = serviceProvider.GetRequiredService<ReadOnlyDatabaseHealthCheck>();

        var probe = await selector.ProbeRouteAsync(CancellationToken.None);
        var healthResult = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => {
            await using var _ = await selector.CreateDbContextAsync(CancellationToken.None);
        });
        Assert.Equal("Rejected", probe.RouteTarget);
        Assert.Equal(HealthStatus.Unhealthy, healthResult.Status);
    }

    /// <summary>
    /// 创建报表查询预算守卫。
    /// </summary>
    /// <param name="maxReportTimeRangeDays">最大时间范围天数。</param>
    /// <param name="maxReportRows">最大返回行数。</param>
    /// <returns>预算守卫。</returns>
    private static ReportingQueryGuard CreateGuard(int maxReportTimeRangeDays, int maxReportRows) {
        return new ReportingQueryGuard(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions {
            MaxReportTimeRangeDays = maxReportTimeRangeDays,
            MaxReportRows = maxReportRows
        }));
    }

    /// <summary>
    /// 构建测试服务容器。
    /// </summary>
    /// <param name="isEnabled">是否启用只读数据库。</param>
    /// <param name="fallbackToPrimaryWhenUnavailable">是否允许回退主库。</param>
    /// <param name="includeReadOnlyConnectionString">是否包含只读连接字符串。</param>
    /// <returns>服务容器。</returns>
    private static ServiceProvider BuildServiceProvider(bool isEnabled, bool fallbackToPrimaryWhenUnavailable, bool includeReadOnlyConnectionString) {
        var settings = new Dictionary<string, string?> {
            ["Persistence:Provider"] = "MySql",
            ["Persistence:ReadOnlyDatabase:IsEnabled"] = isEnabled.ToString(),
            ["Persistence:ReadOnlyDatabase:FallbackToPrimaryWhenUnavailable"] = fallbackToPrimaryWhenUnavailable.ToString(),
            ["Persistence:ReadOnlyDatabase:MaxReportTimeRangeDays"] = "31",
            ["Persistence:ReadOnlyDatabase:MaxReportRows"] = "10000"
        };
        if (includeReadOnlyConnectionString) {
            settings["ConnectionStrings:MySqlReadOnly"] = "server=127.0.0.1;port=3306;database=zeye_sorting_hub_ro;uid=reader;pwd=<readonly-password>;SslMode=None;";
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDbContextFactory<SortingHubDbContext>(options =>
            options.UseInMemoryDatabase($"reporting-query-isolation-tests-{Guid.NewGuid():N}"));
        services.AddSingleton<IOptions<ReadOnlyDatabaseOptions>>(Microsoft.Extensions.Options.Options.Create(new ReadOnlyDatabaseOptions {
            IsEnabled = isEnabled,
            FallbackToPrimaryWhenUnavailable = fallbackToPrimaryWhenUnavailable,
            MaxReportTimeRangeDays = 31,
            MaxReportRows = 10000
        }));
        services.AddSingleton<ReportingQueryGuard>();
        services.AddSingleton<ReadOnlyDbContextFactorySelector>();
        services.AddSingleton<ReadOnlyDatabaseHealthCheck>();
        return services.BuildServiceProvider();
    }
}
