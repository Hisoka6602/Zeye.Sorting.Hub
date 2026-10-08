using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Application.Services.WriteBuffers;
using Zeye.Sorting.Hub.Host.Middleware;
using Zeye.Sorting.Hub.Host.Options;
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Baseline;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Diagnostics;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Host.Configuration;

/// <summary>在发布热更新之前校验；失败不改变持久化和当前运行值。</summary>
public static class HostConfigurationValidator {
    /// <summary>发布之前校验完整的有效配置。</summary>
    public static void Validate(IConfiguration configuration) {
        var logs = configuration.GetSection("LogCleanup").Get<LogCleanupSettings>() ?? new();
        if (logs.RetentionDays < 1 || logs.CheckIntervalHours < 1 || string.IsNullOrWhiteSpace(logs.LogDirectory)) throw new ArgumentException("日志保留天数和巡检间隔必须为正数，目录不能为空。");
        var resources = configuration.GetSection(ResourceThresholdsOptions.SectionName).Get<ResourceThresholdsOptions>() ?? new();
        if (resources.MaxConnectionPoolSize is < 1 or > 10000 || resources.MemoryWarningThresholdMB is < 0 or > 1048576
            || resources.HandleWarningThreshold is < 0 or > 1000000 || resources.MinimumDiskFreeMB is < 0 or > 1048576
            || resources.SampleIntervalSeconds is < 10 or > 3600) throw new ArgumentException("资源阈值或采样周期超出允许范围。");
        var audit = configuration.GetSection(WebRequestAuditLogOptions.SectionName).Get<WebRequestAuditLogOptions>() ?? new();
        if (audit.SampleRate is < 0 or > 1 || audit.SlowRequestThresholdMs < 0 || audit.MaxRequestBodyLength is < 0 or > 1048576
            || audit.MaxResponseBodyLength is < 0 or > 1048576 || audit.BackgroundQueueCapacity is < 1 or > 1048576
            || audit.BackgroundBatchSize is < 1 or > 10000 || audit.BackgroundBatchDelayMs is < 0 or > 60000
            || audit.DropLogIntervalSeconds is < 1 or > 3600 || audit.ExcludedPathPrefixes.Any(x => string.IsNullOrEmpty(x) || !x.StartsWith('/')))
            throw new ArgumentException("审计采样率、正文长度、队列或排除路径无效。");
        _ = configuration.GetValue<bool>("Access:EnforceAuthorization");
        foreach (var level in configuration.GetSection("Logging:LogLevel").GetChildren())
            if (!Enum.TryParse<LogLevel>(level.Value, true, out var parsed) || !Enum.IsDefined(parsed)) throw new ArgumentException("日志级别无效：" + level.Key);
        var sharding = ParcelShardingStrategyEvaluator.Evaluate(configuration);
        if (sharding.ValidationErrors.Count > 0) throw new ArgumentException(string.Join("；", sharding.ValidationErrors));
        _ = ConfiguredProviderNames.Normalize(configuration["Persistence:Provider"]);
        // 仅注册并解析 options，复用范围校验；不解析 EF 工厂或其他会连接数据库的服务。
        var services = new ServiceCollection(); services.AddObjectStorageOptions(configuration); services.AddSortingHubPersistence(configuration);
        using var provider = services.BuildServiceProvider();
        ValidateOptions<ObjectStorageOptions>(provider); ValidateOptions<BackupOptions>(provider);
        ValidateOptions<DataRetentionOptions>(provider); ValidateOptions<DataArchiveOptions>(provider);
        ValidateOptions<BufferedWriteOptions>(provider); ValidateOptions<ReadOnlyDatabaseOptions>(provider);
        ValidateOptions<DatabaseConnectionDiagnosticsOptions>(provider); ValidateOptions<BaselineDataOptions>(provider);
        ValidateOptions<ShardingPrebuildOptions>(provider); ValidateOptions<ShardingRuntimeInspectionOptions>(provider);
    }
    /// <summary>在配置提交前执行既有选项校验，不启动任何后台组件。</summary>
    private static void ValidateOptions<T>(IServiceProvider provider) where T : class => _ = provider.GetRequiredService<IOptions<T>>().Value;
}
