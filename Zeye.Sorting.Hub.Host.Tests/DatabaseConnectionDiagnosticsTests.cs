using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Diagnostics;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 数据库连接诊断回归测试。
/// </summary>
public sealed class DatabaseConnectionDiagnosticsTests {
    /// <summary>
    /// 验证场景：默认配置满足约束并保留规划默认值。
    /// </summary>
    [Fact]
    public void DiagnosticsOptions_ShouldExposeExpectedDefaults() {
        var options = new DatabaseConnectionDiagnosticsOptions();

        Assert.True(options.IsWarmupEnabled);
        Assert.Equal(4, options.WarmupConnectionCount);
        Assert.Equal(3000, options.ProbeTimeoutMilliseconds);
        Assert.Equal(3, options.FailureThreshold);
        Assert.Equal(2, options.RecoveryThreshold);
    }

    /// <summary>
    /// 验证场景：非法配置会在选项校验阶段被拒绝。
    /// </summary>
    [Fact]
    public void DiagnosticsOptions_ShouldRejectInvalidConfiguration() {
        var services = new ServiceCollection();
        services.AddOptions<DatabaseConnectionDiagnosticsOptions>()
            .Configure(options => {
                options.WarmupConnectionCount = 0;
                options.ProbeTimeoutMilliseconds = 50;
                options.FailureThreshold = 0;
                options.RecoveryThreshold = 0;
            })
            .Validate(static options => options.WarmupConnectionCount is >= 1 and <= 64, "WarmupConnectionCount 必须在 1~64 之间")
            .Validate(static options => options.ProbeTimeoutMilliseconds is >= 100 and <= 60000, "ProbeTimeoutMilliseconds 必须在 100~60000 之间")
            .Validate(static options => options.FailureThreshold is >= 1 and <= 20, "FailureThreshold 必须在 1~20 之间")
            .Validate(static options => options.RecoveryThreshold is >= 1 and <= 20, "RecoveryThreshold 必须在 1~20 之间")
            .ValidateOnStart();

        using var serviceProvider = services.BuildServiceProvider();
        var exception = Assert.Throws<OptionsValidationException>(() => serviceProvider.GetRequiredService<IOptions<DatabaseConnectionDiagnosticsOptions>>().Value);

        Assert.Contains("WarmupConnectionCount", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：连接探测失败时不会向外抛出异常，而是转为失败快照。
    /// </summary>
    [Fact]
    public async Task ProbeAsync_WhenConnectionFails_ShouldReturnFailureSnapshotWithoutThrowing() {
        var service = CreateDiagnosticsService(
            CreateSqlServerFailureFactory(),
            new DatabaseConnectionDiagnosticsOptions {
                ProbeTimeoutMilliseconds = 1000,
                FailureThreshold = 3,
                RecoveryThreshold = 2
            });

        var snapshot = await service.ProbeAsync(CancellationToken.None);

        Assert.False(snapshot.IsProbeSucceeded);
        Assert.Equal(1, snapshot.ConsecutiveFailureCount);
        Assert.Equal(0, snapshot.ConsecutiveSuccessCount);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.FailureMessage));
    }

    /// <summary>
    /// 验证场景：连续失败达到阈值后，详细健康检查返回 Unhealthy。
    /// </summary>
    [Fact]
    public async Task DetailedHealthCheck_WhenFailuresReachThreshold_ShouldReturnUnhealthy() {
        var options = new DatabaseConnectionDiagnosticsOptions {
            ProbeTimeoutMilliseconds = 1000,
            FailureThreshold = 2,
            RecoveryThreshold = 2
        };
        var diagnostics = CreateDiagnosticsService(CreateSqlServerFailureFactory(), options);
        var healthCheck = new DatabaseConnectionDetailedHealthCheck(diagnostics, Microsoft.Extensions.Options.Options.Create(options));

        await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(2, Assert.IsType<int>(result.Data["consecutiveFailureCount"]));
    }

    /// <summary>
    /// 验证场景：连续成功探测会维持 Healthy，并更新成功计数。
    /// </summary>
    [Fact]
    public async Task DetailedHealthCheck_WhenProbesKeepSucceeding_ShouldReturnHealthy() {
        var options = new DatabaseConnectionDiagnosticsOptions {
            ProbeTimeoutMilliseconds = 1000,
            FailureThreshold = 2,
            RecoveryThreshold = 2
        };
        var diagnostics = CreateDiagnosticsService(CreateInMemoryFactory($"diagnostics-recovery-{Guid.NewGuid():N}"), options);
        var healthCheck = new DatabaseConnectionDetailedHealthCheck(diagnostics, Microsoft.Extensions.Options.Options.Create(options));

        await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        var snapshot = diagnostics.GetLatestSnapshot();

        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsProbeSucceeded);
        Assert.False(snapshot.IsRecoveryPending);
        Assert.Equal(2, snapshot.ConsecutiveSuccessCount);
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    /// <summary>
    /// 验证场景：成功快照时间保持本地时间语义。
    /// </summary>
    [Fact]
    public async Task ProbeAsync_WhenProbeSucceeds_ShouldKeepLocalTimeSemantics() {
        var diagnostics = CreateDiagnosticsService(
            CreateInMemoryFactory($"diagnostics-local-time-{Guid.NewGuid():N}"),
            new DatabaseConnectionDiagnosticsOptions());

        var snapshot = await diagnostics.ProbeAsync(CancellationToken.None);

        Assert.True(snapshot.IsProbeSucceeded);
        LocalTimeTestConstraint.AssertIsLocalTime(snapshot.CheckedAtLocal);
    }

    /// <summary>
    /// 验证场景：详细健康检查附带关键诊断数据。
    /// </summary>
    [Fact]
    public async Task DetailedHealthCheck_ShouldContainKeyDataFields() {
        var options = new DatabaseConnectionDiagnosticsOptions();
        var diagnostics = CreateDiagnosticsService(CreateInMemoryFactory($"diagnostics-health-{Guid.NewGuid():N}"), options);
        var healthCheck = new DatabaseConnectionDetailedHealthCheck(diagnostics, Microsoft.Extensions.Options.Options.Create(options));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.True(result.Data.ContainsKey("provider"));
        Assert.True(result.Data.ContainsKey("database"));
        Assert.True(result.Data.ContainsKey("checkedAtLocal"));
        Assert.True(result.Data.ContainsKey("elapsedMilliseconds"));
        Assert.True(result.Data.ContainsKey("consecutiveFailureCount"));
        Assert.True(result.Data.ContainsKey("consecutiveSuccessCount"));
    }

    /// <summary>
    /// 创建数据库连接诊断服务。
    /// </summary>
    /// <param name="dbContextFactory">DbContext 工厂。</param>
    /// <param name="options">诊断配置。</param>
    /// <returns>数据库连接诊断服务。</returns>
    private static DatabaseConnectionDiagnosticsService CreateDiagnosticsService(
        IDbContextFactory<SortingHubDbContext> dbContextFactory,
        DatabaseConnectionDiagnosticsOptions options) {
        return new DatabaseConnectionDiagnosticsService(dbContextFactory, Microsoft.Extensions.Options.Options.Create(options));
    }

    /// <summary>
    /// 创建 InMemory 数据库工厂。
    /// </summary>
    /// <param name="databaseName">数据库名称。</param>
    /// <returns>InMemory 数据库工厂。</returns>
    private static IDbContextFactory<SortingHubDbContext> CreateInMemoryFactory(string databaseName) {
        var options = new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new SortingHubTestDbContextFactory(options);
    }

    /// <summary>
    /// 创建必然失败的 SQL Server 数据库工厂。
    /// </summary>
    /// <returns>失败数据库工厂。</returns>
    private static IDbContextFactory<SortingHubDbContext> CreateSqlServerFailureFactory() {
        var options = new DbContextOptionsBuilder<SortingHubDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=DiagnosticsFailure;User Id=sa;Password=Password123!;TrustServerCertificate=True;Connect Timeout=1")
            .Options;
        return new SortingHubTestDbContextFactory(options);
    }
}
