using NLog;
using Zeye.Sorting.Hub.Host;
using Microsoft.OpenApi.Models;
using NLog.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Diagnostics;
using Zeye.Sorting.Hub.Host.HostedServices;
using Zeye.Sorting.Hub.Host.Swagger;
using Zeye.Sorting.Hub.SharedKernel.Utilities;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Application.Services.AuditLogs;
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

// ──────────────────────────────────────────────────────────
// 启动期引导日志：在 DI 容器就绪之前捕获启动异常
// ──────────────────────────────────────────────────────────
var NLogLogger = LogManager.GetCurrentClassLogger();
const string UrlsConfigKey = "urls";

try {
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
    builder.Logging.AddNLog();
    using var startupLoggerFactory = LoggerFactory.Create(logging => logging.AddNLog());
    var startupLogger = startupLoggerFactory.CreateLogger("Startup");
    builder.Services.Configure<LogCleanupSettings>(
        builder.Configuration.GetSection("LogCleanup"));
    builder.Services.Configure<HostingOptions>(builder.Configuration.GetSection("Hosting"));
    builder.Services.AddHostedService<LogCleanupService>();
    builder.Services.AddHostedService<Worker>();
    builder.Services.AddHostedService<DevelopmentBrowserLauncherHostedService>();
    builder.Services.AddSingleton<SafeExecutor>();
    builder.Services.AddSortingHubPersistence(builder.Configuration);
    builder.Services.AddSingleton<IAutoTuningObservability, AutoTuningLoggerObservability>();
    builder.Services.AddProblemDetails();
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
                startupLogger.LogWarning("Swagger XML 注释文件未找到：{XmlPath}", xmlPath);
            }
        }

        options.SchemaFilter<EnumDescriptionSchemaFilter>();
    });
    builder.Services.AddScoped<GetParcelPagedQueryService>();
    builder.Services.AddScoped<GetParcelByIdQueryService>();
    builder.Services.AddScoped<GetAdjacentParcelsQueryService>();
    builder.Services.AddScoped<CreateParcelCommandService>();
    builder.Services.AddScoped<UpdateParcelStatusCommandService>();
    builder.Services.AddScoped<DeleteParcelCommandService>();
    builder.Services.AddScoped<CleanupExpiredParcelsCommandService>();
    builder.Services.AddScoped<WriteWebRequestAuditLogCommandService>();

    // Host 启动时执行持久化初始化
    builder.Services.AddHostedService<DatabaseInitializerHostedService>();
    builder.Services.AddHostedService<DatabaseAutoTuningHostedService>();

    var app = builder.Build();

    // ──────────────────────────────────────────────────────
    // 全局异常出口：统一 ProblemDetails + 异常日志落盘
    // ──────────────────────────────────────────────────────
    app.UseExceptionHandler(exceptionHandlerApp => {
        exceptionHandlerApp.Run(async context => {
            // 步骤 1：提取当前请求异常
            var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = exceptionFeature?.Error;

            // 步骤 2：所有异常必须记录日志
            var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("GlobalExceptionHandler");
            if (exception is not null) {
                logger.LogError(exception, "处理 HTTP 请求时发生未处理异常，路径：{Path}", context.Request.Path);
            }
            else {
                logger.LogError("处理 HTTP 请求时发生未知异常，路径：{Path}", context.Request.Path);
            }

            // 步骤 3：若响应已开始写出，则避免再次写入响应导致连接异常
            if (context.Response.HasStarted) {
                logger.LogError("响应已开始写出，无法输出统一 ProblemDetails，路径：{Path}", context.Request.Path);
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

    // 最小探活端点：用于容器/网关健康检查，不承载业务语义
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
        .WithName("HealthCheck")
        .WithSummary("服务健康检查")
        .WithDescription("用于容器与网关探活，仅返回服务存活状态，不承载任何业务语义。");
    // Parcel 只读查询端点：统一走 Application 查询服务，不直接暴露领域模型。
    app.MapParcelReadOnlyApis();
    // Parcel 管理端写接口：普通写操作 + 危险治理接口（cleanup-expired）分开治理。
    app.MapParcelAdminApis();

    app.Run();
}
catch (Exception ex) {
    // 捕获启动期间的顶层异常，确保日志落盘后再退出
    NLogLogger.Fatal(ex, "宿主启动失败，程序即将退出");
    throw;
}
finally {
    // 强制刷新所有 NLog 缓冲，确保所有日志写入磁盘
    LogManager.Shutdown();
}
