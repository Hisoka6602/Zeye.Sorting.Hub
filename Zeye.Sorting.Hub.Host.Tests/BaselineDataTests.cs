using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Baseline;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 基线数据测试。
/// </summary>
public sealed class BaselineDataTests {
    /// <summary>
    /// 有效配置应通过基线校验。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BaselineDataValidator_WhenConfigurationValid_ShouldReturnValid() {
        var validator = CreateValidator(CreateValidConfiguration(), CreateOptions());

        var result = await validator.ValidateAsync(CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// 缺少连接字符串时应返回失败。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BaselineDataValidator_WhenConnectionStringMissing_ShouldReturnInvalid() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Provider"] = "MySql",
                ["Persistence:Sharding:ParcelStartTime"] = "2026-01-01T00:00:00",
                [BaselineDataOptions.FailureModeConfigKey] = "Degraded"
            })
            .Build();
        var validator = CreateValidator(configuration, CreateOptions());

        var result = await validator.ValidateAsync(CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, static item => item.Contains("ConnectionStrings:MySql", StringComparison.Ordinal));
    }

    /// <summary>
    /// 带时区后缀的分表起始时间应返回失败。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BaselineDataValidator_WhenParcelStartTimeContainsTimeZone_ShouldReturnInvalid() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Provider"] = "MySql",
                ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Port=3306;Database=zeye_sorting_hub;User Id=baseline_user;Password=placeholder;",
                ["Persistence:Sharding:ParcelStartTime"] = "2026-01-01T00:00:00Z",
                [BaselineDataOptions.FailureModeConfigKey] = "Degraded"
            })
            .Build();
        var validator = CreateValidator(configuration, CreateOptions());

        var result = await validator.ValidateAsync(CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, static item => item.Contains("禁止使用 Z 或 offset", StringComparison.Ordinal));
    }

    /// <summary>
    /// Degraded 模式下校验失败应返回 Degraded。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BaselineDataHealthCheck_WhenValidationFailedInDegradedMode_ShouldReturnDegraded() {
        var validator = CreateValidator(CreateValidConfiguration(), CreateOptions());
        validator.SetLatestResult(new BaselineDataValidationResult {
            ValidatedAtLocal = DateTime.Now,
            IsValidationEnabled = true,
            IsSeedEnabled = false,
            FailureMode = MigrationFailureMode.Degraded,
            IsValid = false,
            ShouldBlockStartup = false,
            WasSeedAttempted = false,
            SeededRecordCount = 0,
            Summary = "基线数据校验失败。",
            Errors = ["缺少必要配置"],
            Warnings = [],
            SeedMessages = []
        });

        var healthCheck = new BaselineDataHealthCheck(validator);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    /// <summary>
    /// FailFast 模式下校验失败应返回 Unhealthy。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BaselineDataHealthCheck_WhenValidationFailedInFailFastMode_ShouldReturnUnhealthy() {
        var validator = CreateValidator(CreateValidConfiguration(), CreateOptions());
        validator.SetLatestResult(new BaselineDataValidationResult {
            ValidatedAtLocal = DateTime.Now,
            IsValidationEnabled = true,
            IsSeedEnabled = false,
            FailureMode = MigrationFailureMode.FailFast,
            IsValid = false,
            ShouldBlockStartup = true,
            WasSeedAttempted = false,
            SeededRecordCount = 0,
            Summary = "基线数据校验失败。",
            Errors = ["缺少必要配置"],
            Warnings = [],
            SeedMessages = []
        });

        var healthCheck = new BaselineDataHealthCheck(validator);
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    /// <summary>
    /// 种子入口应保持幂等 no-op。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BaselineDataSeeder_ShouldReturnIdempotentNoOpResult() {
        var seeder = new BaselineDataSeeder();
        var initialResult = new BaselineDataValidationResult {
            ValidatedAtLocal = DateTime.Now,
            IsValidationEnabled = true,
            IsSeedEnabled = true,
            FailureMode = MigrationFailureMode.Degraded,
            IsValid = true,
            ShouldBlockStartup = false,
            WasSeedAttempted = false,
            SeededRecordCount = 0,
            Summary = "基线数据校验通过。",
            Errors = [],
            Warnings = [],
            SeedMessages = []
        };

        var seededResult = await seeder.SeedAsync(initialResult, CancellationToken.None);

        Assert.True(seededResult.WasSeedAttempted);
        Assert.Equal(0, seededResult.SeededRecordCount);
        Assert.Contains(seededResult.SeedMessages, static item => item.Contains("已跳过自动写入", StringComparison.Ordinal));
    }

    /// <summary>
    /// Degraded 模式下校验失败不应抛出异常。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task BaselineDataValidationHostedService_WhenValidationFailsInDegradedMode_ShouldNotThrow() {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Provider"] = "MySql",
                ["Persistence:Sharding:ParcelStartTime"] = "2026-01-01T00:00:00",
                [BaselineDataOptions.FailureModeConfigKey] = "Degraded"
            })
            .Build();
        var options = CreateOptions();
        var validator = CreateValidator(configuration, options);
        var service = new BaselineDataValidationHostedService(
            validator,
            new BaselineDataSeeder(),
            Microsoft.Extensions.Options.Options.Create(options));

        var exception = await Record.ExceptionAsync(() => service.StartAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.NotNull(validator.GetLatestResult());
        Assert.False(validator.GetLatestResult()!.IsValid);
    }

    /// <summary>
    /// 创建有效配置。
    /// </summary>
    /// <returns>配置对象。</returns>
    private static IConfiguration CreateValidConfiguration() {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["Persistence:Provider"] = "MySql",
                ["ConnectionStrings:MySql"] = "Server=127.0.0.1;Port=3306;Database=zeye_sorting_hub;User Id=baseline_user;Password=placeholder;",
                ["Persistence:Sharding:ParcelStartTime"] = "2026-01-01T00:00:00",
                [BaselineDataOptions.FailureModeConfigKey] = "Degraded"
            })
            .Build();
    }

    /// <summary>
    /// 创建默认选项。
    /// </summary>
    /// <returns>配置选项。</returns>
    private static BaselineDataOptions CreateOptions() {
        return new BaselineDataOptions {
            IsValidationEnabled = true,
            IsSeedEnabled = false,
            FailureMode = nameof(MigrationFailureMode.Degraded)
        };
    }

    /// <summary>
    /// 创建校验器。
    /// </summary>
    /// <param name="configuration">配置源。</param>
    /// <param name="options">配置项。</param>
    /// <returns>校验器。</returns>
    private static BaselineDataValidator CreateValidator(IConfiguration configuration, BaselineDataOptions options) {
        return new BaselineDataValidator(configuration, Microsoft.Extensions.Options.Options.Create(options));
    }
}
