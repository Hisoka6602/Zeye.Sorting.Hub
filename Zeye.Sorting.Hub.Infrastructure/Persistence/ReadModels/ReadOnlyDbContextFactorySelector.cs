using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NLog;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>
/// 报表查询只读上下文选择器。
/// </summary>
public sealed class ReadOnlyDbContextFactorySelector {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 根服务容器。
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// 主库上下文工厂。
    /// </summary>
    private readonly IDbContextFactory<SortingHubDbContext> _primaryDbContextFactory;

    /// <summary>
    /// 配置根。
    /// </summary>
    private readonly IConfiguration _configuration;

    /// <summary>
    /// 只读数据库配置。
    /// </summary>
    private readonly ReadOnlyDatabaseOptions _options;

    /// <summary>
    /// 初始化报表查询只读上下文选择器。
    /// </summary>
    /// <param name="serviceProvider">根服务容器。</param>
    /// <param name="primaryDbContextFactory">主库上下文工厂。</param>
    /// <param name="configuration">配置根。</param>
    /// <param name="options">只读数据库配置。</param>
    public ReadOnlyDbContextFactorySelector(
        IServiceProvider serviceProvider,
        IDbContextFactory<SortingHubDbContext> primaryDbContextFactory,
        IConfiguration configuration,
        IOptions<ReadOnlyDatabaseOptions> options) {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _primaryDbContextFactory = primaryDbContextFactory ?? throw new ArgumentNullException(nameof(primaryDbContextFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// 创建报表查询上下文。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>数据库上下文。</returns>
    public async ValueTask<SortingHubDbContext> CreateDbContextAsync(CancellationToken cancellationToken) {
        var probe = await ProbeRouteAsync(cancellationToken);
        if (ShouldRejectQuery(probe)) {
            Logger.Error("只读数据库路由拒绝访问，Summary={Summary}", probe.Summary);
            throw new InvalidOperationException(probe.Summary);
        }

        if (ShouldUsePrimaryDatabase(probe)) {
            return await _primaryDbContextFactory.CreateDbContextAsync(cancellationToken);
        }

        var optionsBuilder = new DbContextOptionsBuilder<SortingHubDbContext>();
        PersistenceServiceCollectionExtensions.ConfigureConfiguredProviderDbContextOptions(
            _serviceProvider,
            optionsBuilder,
            ResolveConfiguredProviderName(),
            probe.ReadOnlyConnectionString!);
        return new SortingHubDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// 探测当前只读数据库路由状态。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>路由状态元组。</returns>
    public async Task<ReadOnlyRouteProbeResult> ProbeRouteAsync(CancellationToken cancellationToken) {
        if (!_options.IsEnabled) {
            return new ReadOnlyRouteProbeResult(false, false, false, true, "Primary", "只读数据库未启用，报表查询将保持主库路由。", null);
        }

        var configuredProviderName = ResolveConfiguredProviderName();
        var readOnlyConnectionStringKey = ResolveReadOnlyConnectionStringKey(configuredProviderName);
        var readOnlyConnectionString = _configuration.GetConnectionString(readOnlyConnectionStringKey);
        if (string.IsNullOrWhiteSpace(readOnlyConnectionString)) {
            var summary = $"缺少连接字符串：ConnectionStrings:{readOnlyConnectionStringKey}。";
            if (_options.FallbackToPrimaryWhenUnavailable) {
                Logger.Warn("只读数据库未配置，已回退主库，ConnectionStringKey={ConnectionStringKey}", readOnlyConnectionStringKey);
                return new ReadOnlyRouteProbeResult(true, false, false, true, "Primary", $"{summary} 当前已回退主库。", null);
            }

            Logger.Error("只读数据库未配置且未允许回退主库，ConnectionStringKey={ConnectionStringKey}", readOnlyConnectionStringKey);
            return new ReadOnlyRouteProbeResult(true, false, false, false, "Rejected", $"{summary} 当前已拒绝报表查询。", null);
        }

        try {
            await using var readOnlyDbContext = CreateReadOnlyDbContext(readOnlyConnectionString, configuredProviderName);
            var canConnect = await readOnlyDbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect) {
                return new ReadOnlyRouteProbeResult(true, true, true, false, "ReadOnly", "只读数据库连接正常，报表查询将使用只读路由。", readOnlyConnectionString);
            }

            return BuildUnavailableResult("只读数据库连接不可用。", readOnlyConnectionString);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            Logger.Warn("只读数据库探测收到取消信号。");
            throw;
        }
        catch (Exception exception) {
            Logger.Error(exception, "只读数据库探测失败。");
            return BuildUnavailableResult($"只读数据库探测失败：{exception.Message}", readOnlyConnectionString);
        }
    }

    /// <summary>
    /// 判断当前探测结果是否应拒绝报表查询。
    /// </summary>
    /// <param name="probe">探测结果。</param>
    /// <returns>是否应拒绝报表查询。</returns>
    private static bool ShouldRejectQuery(ReadOnlyRouteProbeResult probe) {
        return probe.IsEnabled && !probe.IsFallbackToPrimary && !probe.IsReadOnlyAvailable;
    }

    /// <summary>
    /// 判断当前探测结果是否应使用主库。
    /// </summary>
    /// <param name="probe">探测结果。</param>
    /// <returns>是否应使用主库。</returns>
    private static bool ShouldUsePrimaryDatabase(ReadOnlyRouteProbeResult probe) {
        return !probe.IsEnabled || probe.IsFallbackToPrimary;
    }

    /// <summary>
    /// 创建只读数据库上下文。
    /// </summary>
    /// <param name="readOnlyConnectionString">只读连接字符串。</param>
    /// <param name="configuredProviderName">配置层提供器名称。</param>
    /// <returns>数据库上下文。</returns>
    private SortingHubDbContext CreateReadOnlyDbContext(string readOnlyConnectionString, string configuredProviderName) {
        var optionsBuilder = new DbContextOptionsBuilder<SortingHubDbContext>();
        PersistenceServiceCollectionExtensions.ConfigureConfiguredProviderDbContextOptions(
            _serviceProvider,
            optionsBuilder,
            configuredProviderName,
            readOnlyConnectionString);
        return new SortingHubDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// 解析配置层提供器名称。
    /// </summary>
    /// <returns>配置层提供器名称。</returns>
    private string ResolveConfiguredProviderName() {
        var configuredProviderName = _configuration["Persistence:Provider"];
        if (string.Equals(configuredProviderName, ConfiguredProviderNames.MySql, StringComparison.OrdinalIgnoreCase)) {
            return ConfiguredProviderNames.MySql;
        }

        if (string.Equals(configuredProviderName, ConfiguredProviderNames.SqlServer, StringComparison.OrdinalIgnoreCase)) {
            return ConfiguredProviderNames.SqlServer;
        }

        Logger.Error("读取报表只读数据库配置时发现不支持的 Provider，Provider={Provider}", configuredProviderName);
        throw new InvalidOperationException($"不支持的数据库类型：{configuredProviderName}。");
    }

    /// <summary>
    /// 解析只读连接字符串键名。
    /// </summary>
    /// <param name="configuredProviderName">配置层提供器名称。</param>
    /// <returns>连接字符串键名。</returns>
    private static string ResolveReadOnlyConnectionStringKey(string configuredProviderName) {
        return configuredProviderName switch {
            ConfiguredProviderNames.MySql => "MySqlReadOnly",
            ConfiguredProviderNames.SqlServer => "SqlServerReadOnly",
            _ => throw new InvalidOperationException($"不支持的数据库类型：{configuredProviderName}。")
        };
    }

    /// <summary>
    /// 构建只读数据库不可用时的路由结果。
    /// </summary>
    /// <param name="summary">摘要信息。</param>
    /// <param name="readOnlyConnectionString">只读连接字符串。</param>
    /// <returns>路由状态元组。</returns>
    private ReadOnlyRouteProbeResult BuildUnavailableResult(string summary, string? readOnlyConnectionString) {
        if (_options.FallbackToPrimaryWhenUnavailable) {
            Logger.Warn("只读数据库不可用，已回退主库，Summary={Summary}", summary);
            return new ReadOnlyRouteProbeResult(true, true, false, true, "Primary", $"{summary} 当前已回退主库。", readOnlyConnectionString);
        }

        Logger.Error("只读数据库不可用且未允许回退主库，Summary={Summary}", summary);
        return new ReadOnlyRouteProbeResult(true, true, false, false, "Rejected", $"{summary} 当前已拒绝报表查询。", readOnlyConnectionString);
    }
}
