using NLog;
using Zeye.Sorting.Hub.Host;
using NLog.Extensions.Logging;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

// ──────────────────────────────────────────────────────────
// 启动期引导日志：在 DI 容器就绪之前捕获启动异常
// ──────────────────────────────────────────────────────────
var bootstrapLogger = LogManager.GetCurrentClassLogger();

try {
    var builder = Host.CreateApplicationBuilder(args);

    // ──────────────────────────────────────────────────────
    // NLog：替换默认日志提供器，双路落盘（详见 nlog.config）
    //   - logs/app-<日期>.log      全量应用日志（按天归档，保留 30 天）
    //   - logs/database-<日期>.log 数据库专属日志（按天归档，保留 30 天）
    //
    // 低开销设计：异步队列（targets async="true"）+ keepFileOpen + optimizeBufferReuse
    // ──────────────────────────────────────────────────────
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();
    builder.Services.Configure<LogCleanupSettings>(
        builder.Configuration.GetSection("LogCleanup"));
    builder.Services.AddHostedService<LogCleanupService>();
    builder.Services.AddHostedService<Worker>();
    builder.Services.AddSortingHubPersistence(builder.Configuration);
    builder.Services.AddSingleton<IAutoTuningObservability, AutoTuningLoggerObservability>();

    // Host 启动时执行持久化初始化
    builder.Services.AddHostedService<DatabaseInitializerHostedService>();
    builder.Services.AddHostedService<DatabaseAutoTuningHostedService>();

    var host = builder.Build();

    host.Run();
}
catch (Exception ex) {
    // 捕获启动期间的顶层异常，确保日志落盘后再退出
    bootstrapLogger.Fatal(ex, "宿主启动失败，程序即将退出");
}
finally {
    // 强制刷新所有 NLog 缓冲，确保所有日志写入磁盘
    LogManager.Shutdown();
}
