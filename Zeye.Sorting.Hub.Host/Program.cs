using NLog;
using NLog.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using Zeye.Sorting.Hub.Host.Options;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Host.Swagger;
using Microsoft.AspNetCore.Diagnostics;
using Zeye.Sorting.Hub.Host.Middleware;
using Microsoft.AspNetCore.Authentication;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Host.Authentication;
using Zeye.Sorting.Hub.Host.HealthChecks;
using Zeye.Sorting.Hub.SharedKernel.Utilities;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Application.Services.AuditLogs;
using Zeye.Sorting.Hub.Application.Services.DataGovernance;
using Zeye.Sorting.Hub.Application.Services.Diagnostics;
using Zeye.Sorting.Hub.Application.Services.Events;
using Zeye.Sorting.Hub.Application.Services.Idempotency;
using Zeye.Sorting.Hub.Application.Services.WriteBuffers;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;
using Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;
using Microsoft.Extensions.DependencyInjection.Extensions;

// ──────────────────────────────────────────────────────────
// 启动期引导日志：在 DI 容器就绪之前捕获启动异常
// ──────────────────────────────────────────────────────────
var logger = LogManager.GetCurrentClassLogger();
const string UrlsConfigKey = "urls";

try {
    var startupLogger = LogManager.GetLogger($"{nameof(Program)}.Startup");
    var builder = WebApplication.CreateBuilder(args);
    var hostingOptions = builder.Configuration.GetSection("Hosting").Get<HostingOptions>() ?? new HostingOptions();
    var urlsFromConfiguration = builder.Configuration[UrlsConfigKey];
    if (string.IsNullOrWhiteSpace(urlsFromConfiguration)) {
        var bindingUrls = hostingOptions.GetUrlBindings();
        if (bindingUrls.Count > 0) {
            builder.WebHost.UseUrls(bindingUrls.ToArray());
        }
    }

    // ──────────────────────────────────────────────────────
    // NLog：替换默认日志提供器，双路落盘（详见 nlog.config）
    //   - logs/app-<日期>.log      全量应用日志（按天归档，保留 30 天）
    //   - logs/database-<日期>.log 数据库专属日志（按天归档，保留 30 天）
    //
    // 低开销设计：异步队列（targets async="true"）+ keepFileOpen + optimizeBufferReuse
    // ──────────────────────────────────────────────────────
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog(new NLogProviderOptions {
        RemoveLoggerFactoryFilter = false
    });
    var enableQuerySqlLogging = builder.Configuration.GetValue<bool>("Persistence:SqlLogging:EnableQuerySqlLogging");
    if (!enableQuerySqlLogging) {
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    builder.Services.Configure<LogCleanupSettings>(
        builder.Configuration.GetSection("LogCleanup"));
    builder.Services.Configure<HostingOptions>(builder.Configuration.GetSection("Hosting"));
    builder.Services.Configure<AuditReadOnlyApiOptions>(builder.Configuration.GetSection(AuditReadOnlyApiOptions.SectionName));
    builder.Services.Configure<ResourceThresholdsOptions>(builder.Configuration.GetSection(ResourceThresholdsOptions.SectionName));
    builder.Services.AddHostedService<LogCleanupService>();
    builder.Services.AddHostedService<DevelopmentBrowserLauncherHostedService>();
    builder.Services.AddHostedService<DatabaseConnectionWarmupHostedService>();
    builder.Services.AddHostedService<ParcelBatchWriteFlushHostedService>();
    builder.Services.AddHostedService<ShardingPrebuildHostedService>();
    builder.Services.AddHostedService<ShardingInspectionHostedService>();
    builder.Services.AddHostedService<DataArchiveHostedService>();
    builder.Services.AddHostedService<DataRetentionHostedService>();
    builder.Services.AddHostedService<BaselineDataValidationHostedService>();
    builder.Services.AddHostedService<QueryGovernanceReportHostedService>();
    builder.Services.AddHostedService<OutboxDispatchHostedService>();
    builder.Services.AddSingleton<MigrationGovernanceHostedService>();
    builder.Services.AddHostedService(static serviceProvider =>
        serviceProvider.GetRequiredService<MigrationGovernanceHostedService>());
    builder.Services.AddSingleton<SafeExecutor>();
    builder.Services.AddSingleton<ConfigChangeHistoryStore<LogCleanupSettings>>();
    builder.Services.AddSortingHubPersistence(builder.Configuration);
    // 显式替换 IAutoTuningObservability：无论 AddSortingHubPersistence 内是否已注册占位空实现，
    // Replace 均保证最终容器中只存在真实的日志观测实现，与注册顺序无关。
    builder.Services.Replace(ServiceDescriptor.Singleton<IAutoTuningObservability, AutoTuningLoggerObservability>());
    // ──────────────────────────────────────────────────────
    // 健康检查：存活探针（/health/live）+ 就绪探针（/health/ready）
    //   - /health/live   仅判断进程健康（无依赖检查），用于容器重启决策
    //   - /health/ready  包含数据库可用性探测，用于流量接入决策
    // ──────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddCheck<DatabaseConnectionDetailedHealthCheck>(
            name: "database",
            tags: ["ready"])
        .AddCheck<BufferedWriteQueueHealthCheck>(
            name: "parcel-buffered-write",
            tags: ["ready"])
        .AddCheck<BaselineDataHealthCheck>(
            name: "baseline-data",
            tags: ["ready"])
        .AddCheck<OutboxHealthCheck>(
            name: "outbox",
            tags: ["ready"])
        .AddCheck<DataRetentionHealthCheck>(
            name: "data-retention",
            tags: ["ready"])
        .AddCheck<MigrationGovernanceHealthCheck>(
            name: "migration-governance",
            tags: ["ready"])
        .AddCheck<ShardingGovernanceHealthCheck>(
            name: "sharding-governance",
            tags: ["ready"]);
    builder.Services.AddProblemDetails();
    builder.Services
        .AddAuthentication(GuardedAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, GuardedAuthenticationHandler>(GuardedAuthenticationHandler.SchemeName, static _ => { });
    builder.Services.AddAuthorization();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options => {
        var documentName = hostingOptions.GetSwaggerDocumentName();
        options.SwaggerDoc(documentName, new OpenApiInfo {
            Title = hostingOptions.GetSwaggerDocumentTitle(),
            Version = documentName,
            Description = "Zeye.Sorting.Hub API 文档（本地时间语义，禁止 UTC/时区偏移输入）。"
        });

        foreach (var assemblyName in HostingOptions.XmlCommentAssemblyNames) {
            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml");
            if (File.Exists(xmlPath)) {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
                continue;
            }

            if (builder.Environment.IsDevelopment()) {
                startupLogger.Warn("Swagger XML 注释文件未找到：{XmlPath}", xmlPath);
            }
        }

        options.SchemaFilter<EnumDescriptionSchemaFilter>();
    });
    builder.Services.AddScoped<GetParcelPagedQueryService>();
    builder.Services.AddScoped<GetParcelCursorPagedQueryService>();
    builder.Services.AddScoped<GetParcelByIdQueryService>();
    builder.Services.AddScoped<GetAdjacentParcelsQueryService>();
    builder.Services.AddScoped<IdempotencyGuardService>();
    builder.Services.AddScoped<CreateParcelCommandService>();
    builder.Services.AddScoped<UpdateParcelStatusCommandService>();
    builder.Services.AddScoped<DeleteParcelCommandService>();
    builder.Services.AddScoped<CleanupExpiredParcelsCommandService>();
    builder.Services.AddScoped<WriteWebRequestAuditLogCommandService>();
    builder.Services.AddScoped<GetWebRequestAuditLogPagedQueryService>();
    builder.Services.AddScoped<GetWebRequestAuditLogByIdQueryService>();
    builder.Services.AddScoped<CreateArchiveTaskCommandService>();
    builder.Services.AddScoped<GetArchiveTaskPagedQueryService>();
    builder.Services.AddScoped<RetryArchiveTaskCommandService>();
    builder.Services.AddScoped<GetSlowQueryProfileQueryService>();
    builder.Services.AddScoped<InboxMessageGuardService>();
    builder.Services.AddScoped<AppendOutboxMessageCommandService>();
    builder.Services.AddScoped<GetOutboxMessagePagedQueryService>();
    builder.Services.AddScoped<DispatchOutboxMessageCommandService>();
    builder.Services.AddWebRequestAuditLogging(builder.Configuration);

    // Host 启动时执行持久化初始化
    builder.Services.AddHostedService<DatabaseInitializerHostedService>();
    builder.Services.AddHostedService<DatabaseAutoTuningHostedService>();

    var app = builder.Build();
    app.UseWebRequestAuditLogging();

    // ──────────────────────────────────────────────────────
    // 全局异常出口：统一 ProblemDetails + 异常日志落盘
    // ──────────────────────────────────────────────────────
    var globalExceptionLogger = LogManager.GetLogger($"{nameof(Program)}.GlobalExceptionHandler");
    app.UseExceptionHandler(exceptionHandlerApp => {
        exceptionHandlerApp.Run(async context => {
            // 步骤 1：提取当前请求异常
            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = exceptionFeature?.Error;
            var rawPath = context.Request.Path.HasValue ? context.Request.Path.Value : "/";
            var trimmedPath = string.IsNullOrWhiteSpace(rawPath)
                ? string.Empty
                : LineBreakNormalizer.ReplaceLineBreaksToSpace(rawPath).Trim();
            var normalizedPath = string.IsNullOrWhiteSpace(trimmedPath) ? "/" : trimmedPath;

            const int maxPathLength = 256;
            if (normalizedPath.Length > maxPathLength) {
                normalizedPath = normalizedPath[..maxPathLength];
            }

            // 步骤 2：所有异常必须记录日志
            if (exception is not null) {
                globalExceptionLogger.Error(exception, "处理 HTTP 请求时发生未处理异常，Path: {Path}, TraceId: {TraceId}", normalizedPath, context.TraceIdentifier);
            }
            else {
                globalExceptionLogger.Error("处理 HTTP 请求时发生未知异常，Path: {Path}, TraceId: {TraceId}", normalizedPath, context.TraceIdentifier);
            }

            // 步骤 3：若响应已开始写出，则避免再次写入响应导致连接异常
            if (context.Response.HasStarted) {
                globalExceptionLogger.Error("响应已开始写出，无法输出统一 ProblemDetails，Path: {Path}, TraceId: {TraceId}", normalizedPath, context.TraceIdentifier);
                return;
            }

            // 步骤 4：清理已有响应状态并返回统一问题详情响应
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await Results.Problem(
                title: "服务器内部错误",
                detail: "请求处理失败，请联系系统管理员。",
                statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(context);
        });
    });

    // 仅在显式开启时启用 HTTPS 重定向，避免纯 HTTP / 反向代理终止 TLS 场景影响探活
    if (hostingOptions.EnableHttpsRedirection) {
        app.UseHttpsRedirection();
    }
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    var isSwaggerEnabled = app.Environment.IsDevelopment() && hostingOptions.Swagger.Enabled;
    if (isSwaggerEnabled) {
        app.UseSwagger(options => {
            options.RouteTemplate = hostingOptions.BuildSwaggerJsonRouteTemplate();
        });

        app.UseSwaggerUI(options => {
            options.RoutePrefix = hostingOptions.GetSwaggerRoutePrefix();
            options.DocumentTitle = hostingOptions.GetSwaggerDocumentTitle();
            options.SwaggerEndpoint(
                hostingOptions.BuildSwaggerJsonEndpoint(),
                $"{hostingOptions.GetSwaggerDocumentTitle()} ({hostingOptions.GetSwaggerDocumentName()})");
        });
    }

    // ──────────────────────────────────────────────────────
    // 分层健康探针（探针分离，避免误判导致抖动重启）：
    //   /health/live    存活探针：进程级存活（只判断进程是否在运行，无依赖检查）
    //   /health/ready   就绪探针：包含数据库连接探测（表示实例可接受流量）
    //   /health         兼容端点：保持旧版接入链路不变（等价于存活探针）
    // ──────────────────────────────────────────────────────
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
        // 存活探针：不检查任何 tag，仅进程级响应
        Predicate = static _ => false,
        ResponseWriter = HealthCheckResponseWriter.WriteJsonResponseAsync
    })
    .WithName("LivenessProbe")
    .WithSummary("存活探针")
    .WithDescription("进程级存活探测，不包含依赖检查。容器重启策略依据此端点决策。");

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
        // 就绪探针：检查 ready 标签下的所有项（含数据库连接）
        Predicate = static check => check.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteJsonResponseAsync
    })
    .WithName("ReadinessProbe")
    .WithSummary("就绪探针")
    .WithDescription("包含数据库连接探测，表示实例可接受流量。流量切入决策依据此端点。");

    // 兼容端点：保持旧版 /health 接入链路可用
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
        .WithName("HealthCheck")
        .WithSummary("服务健康检查（兼容端点）")
        .WithDescription("兼容历史接入路径，等价于存活探针。建议新接入方改用 /health/live 或 /health/ready。");
    // Parcel 只读查询端点：统一走 Application 查询服务，不直接暴露领域模型。
    app.MapParcelReadOnlyApis();
    // Parcel 管理端写接口：普通写操作 + 危险治理接口（cleanup-expired）分开治理。
    app.MapParcelAdminApis();
    // 审计日志只读查询端点：默认关闭，需显式开启配置后再接线。
    var auditSection = builder.Configuration.GetSection(AuditReadOnlyApiOptions.SectionName);
    var auditReadOnlyApiOptions = auditSection.Exists()
        ? (auditSection.Get<AuditReadOnlyApiOptions>() ?? new AuditReadOnlyApiOptions())
        : new AuditReadOnlyApiOptions();
    if (auditReadOnlyApiOptions.Enabled) {
        app.MapAuditReadOnlyApis(auditReadOnlyApiOptions.RequireAuthorization);
    }

    app.MapDataGovernanceApis();
    app.MapDiagnosticsApis();

    app.Run();
}
catch (Exception ex) {
    // 捕获启动期间的顶层异常，确保日志落盘后再退出
    logger.Fatal(ex, "宿主启动失败，程序即将退出");
    throw;
}
finally {
    // 强制刷新所有 NLog 缓冲，确保所有日志写入磁盘
    LogManager.Shutdown();
}
