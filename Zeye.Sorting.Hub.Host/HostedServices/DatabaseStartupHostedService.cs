using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Host.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>数据库不可用时保留网页与配置服务，依序启动或停止全部业务托管服务。</summary>
public sealed class DatabaseStartupHostedService(IServiceProvider services, DatabaseStartupState state,
    IReadOnlyList<ServiceDescriptor> registrations) : BackgroundService {
    /// <summary>已经开始启动的业务服务，停止时按相反顺序释放。</summary>
    private readonly List<IHostedService> _started = [];
    /// <summary>数据库启动与业务生命周期日志。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>集中接管业务服务生命周期，保留配置重载、日志清理和网页启动入口。</summary>
    public static void Register(IServiceCollection services) {
        var independent = new[] { typeof(ConfigurationReloadHostedService), typeof(LogCleanupService), typeof(DevelopmentBrowserLauncherHostedService) };
        var registrations = services.Where(item => item.ServiceType == typeof(IHostedService)
            && !item.IsKeyedService && !independent.Contains(item.ImplementationType)).ToArray();
        foreach (var registration in registrations) {
            services.Remove(registration);
            // 使用具名的容器单例，保留原工厂语义及容器的释放责任，配置模式不实例化业务服务。
            services.AddKeyedSingleton<IHostedService>(registration, (provider, _) => (IHostedService)(registration.ImplementationInstance
                ?? registration.ImplementationFactory?.Invoke(provider)
                ?? ActivatorUtilities.CreateInstance(provider, registration.ImplementationType!)));
        }
        services.AddSingleton(provider => new DatabaseStartupState(provider.GetRequiredService<RuntimeConfigurationProvider>().StoragePath));
        services.AddSingleton<IHostedService>(provider => new DatabaseStartupHostedService(provider,
            provider.GetRequiredService<DatabaseStartupState>(), registrations));
    }

    /// <summary>先验证连接，再启动原有业务链；失败时停止已启动服务而保留配置网页。</summary>
    public override async Task StartAsync(CancellationToken cancellationToken) {
        var failureSummary = "无法连接数据库。请检查数据库服务、地址、账号、密码和连接权限；Oracle 首次建用户还需提供初始化管理连接。";
        try {
            using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            probeTimeout.CancelAfter(TimeSpan.FromSeconds(15));
            await using (var scope = services.CreateAsyncScope()) {
                await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<SortingHubDbContext>>().CreateDbContextAsync(probeTimeout.Token);
                // 只验证连接；首次 SQLite 的目录和文件必须由后续建库守卫创建，不能在此提前打开。
                await DatabaseConnectionOpenCoordinator.ProbeAdministrationConnectionAsync(
                    scope.ServiceProvider.GetRequiredService<IDatabaseDialect>(), db.Database.GetConnectionString()!, probeTimeout.Token);
            }
            foreach (var registration in registrations) {
                var service = services.GetRequiredKeyedService<IHostedService>(registration);
                failureSummary = service is MigrationGovernanceHostedService or DatabaseInitializerHostedService
                    ? "数据库结构初始化未完成。请检查建库权限、初始化预演开关及服务日志。"
                    : "数据库连接已通过，但业务服务启动失败。请检查服务日志后重试。";
                _started.Add(service);
                await service.StartAsync(cancellationToken);
                if (service is DatabaseInitializerHostedService initializer && !initializer.IsInitialized) {
                    throw new InvalidOperationException("数据库初始化尚未完成，业务服务不能接受请求。");
                }
            }
            state.MarkReady();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) {
            Logger.Error(exception, "数据库或业务初始化失败，保留网页供本机完成数据库配置；业务接口返回 503。");
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await StopAsync(stopTimeout.Token);
            var migration = services.GetService<MigrationGovernanceStateStore>()?.GetLatestExecutionRecord();
            if (migration is { Status: MigrationExecutionRecord.SkippedStatus, IsDryRun: true })
                failureSummary = "数据库初始化处于仅预演模式，表结构尚未创建或更新。请关闭“仅预演数据库初始化”，保存后重启 Host。";
            else if (migration is { Status: MigrationExecutionRecord.SkippedStatus, ShouldApplyMigrations: false })
                failureSummary = "数据库迁移被保护规则阻止。请检查迁移脚本与服务日志，完成受控迁移后再重启。";
            state.RequireConfiguration(failureSummary);
        }
        await base.StartAsync(cancellationToken);
    }

    /// <summary>把业务后台任务的运行异常交回宿主，保留既有 StopHost 异常策略。</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        var tasks = _started.OfType<BackgroundService>().Select(service => service.ExecuteTask).OfType<Task>().ToList();
        while (tasks.Count > 0) {
            var completed = await Task.WhenAny(tasks).WaitAsync(stoppingToken);
            await completed;
            tasks.Remove(completed);
        }
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>关闭业务任务，逐项记录停止异常，配置模式不操作未启动的服务。</summary>
    public override async Task StopAsync(CancellationToken cancellationToken) {
        foreach (var service in _started.AsEnumerable().Reverse()) {
            try { await service.StopAsync(cancellationToken); }
            catch (Exception exception) { Logger.Error(exception, "业务服务停止失败，Service={Service}", service.GetType().Name); }
        }
        _started.Clear();
        await base.StopAsync(cancellationToken);
    }
}
