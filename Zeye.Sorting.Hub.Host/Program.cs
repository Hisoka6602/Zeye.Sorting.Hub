using Serilog;
using Serilog.Events;
using Zeye.Sorting.Hub.Host;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

// ──────────────────────────────────────────────────────────
// 启动期引导日志：在 DI 容器就绪之前捕获启动异常
// ──────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try {
    var builder = Host.CreateApplicationBuilder(args);

    // ──────────────────────────────────────────────────────
    // Serilog：替换默认日志提供器，配置双路落盘
    //   - logs/app-.log        全量应用日志（按天滚动）
    //   - logs/database-.log   数据库专属日志（按天滚动，仅含 DB 相关分类）
    // ──────────────────────────────────────────────────────
    builder.Services.AddSerilog((_, loggerConfig) =>
        loggerConfig
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            // 全量应用日志（控制台 + 文件）
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/app-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate:
                    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            // 数据库专属日志文件：仅记录与数据库初始化、迁移、EF Core、AutoTuning 相关的日志
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(logEvent =>
                    logEvent.Properties.TryGetValue("SourceContext", out var sc) &&
                    sc.ToString() is { } ctx &&
                    (ctx.Contains("DatabaseInitializerHostedService") ||
                     ctx.Contains("DatabaseAutoTuningHostedService") ||
                     ctx.Contains("Microsoft.EntityFrameworkCore") ||
                     ctx.Contains("Zeye.Sorting.Hub.Infrastructure.Persistence")))
                .WriteTo.File(
                    path: "logs/database-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate:
                        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")));

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
    Log.Fatal(ex, "宿主启动失败，程序即将退出");
}
finally {
    // 强制刷新所有 Serilog 缓冲，确保所有日志写入磁盘
    await Log.CloseAndFlushAsync();
}
