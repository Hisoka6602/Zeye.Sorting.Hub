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
using Zeye.Sorting.Hub.Domain.Options.LogCleanup;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

// ──────────────────────────────────────────────────────────
// 启动期引导日志：在 DI 容器就绪之前捕获启动异常
// ──────────────────────────────────────────────────────────
var bootstrapLogger = LogManager.GetCurrentClassLogger();
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
    bootstrapLogger.Fatal(ex, "宿主启动失败，程序即将退出");
}
finally {
    // 强制刷新所有 NLog 缓冲，确保所有日志写入磁盘
    LogManager.Shutdown();
}

/// <summary>
/// Parcel 只读 API 路由扩展。
/// </summary>
public static class ParcelReadOnlyApiRouteExtensions {

    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 注册 Parcel 只读 API。
    /// </summary>
    /// <param name="routeBuilder">路由构建器。</param>
    /// <returns>路由构建器。</returns>
    public static IEndpointRouteBuilder MapParcelReadOnlyApis(this IEndpointRouteBuilder routeBuilder) {
        var group = routeBuilder.MapGroup("/api/parcels").WithTags("Parcels");

        group.MapGet(string.Empty, GetParcelListAsync)
            .WithName("GetParcelList")
            .WithSummary("分页查询 Parcel 列表")
            .WithDescription("按页码、分页大小与可选过滤条件（条码检索词、集包号、状态、异常类型、格口、扫码时间范围）查询包裹摘要列表。时间参数必须为本地时间字符串，不允许 UTC 或时区偏移。\n\nBarCodeKeyword Provider 语义：MySQL 使用 FULLTEXT Boolean 模式，其他 Provider 使用 Contains 子串匹配。")
            .Produces<ParcelListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:long}", GetParcelByIdAsync)
            .WithName("GetParcelById")
            .WithSummary("按 Id 查询 Parcel 详情")
            .WithDescription("按包裹主键查询完整详情；当资源不存在时返回 404。")
            .Produces<ParcelDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/adjacent", GetAdjacentParcelsAsync)
            .WithName("GetAdjacentParcels")
            .WithSummary("按包裹 Id 查询 Parcel 邻近记录")
            .WithDescription("以指定包裹 Id 为锚点，基于稳定排序键 (ScannedTime, Id) 查询前后邻近记录数量。锚点不存在返回 404。")
            .Produces<ParcelAdjacentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return routeBuilder;
    }

    /// <summary>
    /// 处理列表查询请求。
    /// </summary>
    /// <param name="query">列表查询参数。</param>
    /// <param name="queryService">应用层查询服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询结果。</returns>
    private static async Task<IResult> GetParcelListAsync(
        [AsParameters] ParcelListQueryParameters query,
        GetParcelPagedQueryService queryService,
        CancellationToken cancellationToken) {
        if (!LocalDateTimeParsing.TryParseOptionalLocalDateTime(query.ScannedTimeStart, out var scannedTimeStart)
            || !LocalDateTimeParsing.TryParseOptionalLocalDateTime(query.ScannedTimeEnd, out var scannedTimeEnd)) {
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "scannedTimeStart/scannedTimeEnd 必须是本地时间格式，且不允许包含 UTC 或时区偏移。");
        }

        try {
            var request = new ParcelListRequest {
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                BarCodeKeyword = query.BarCodeKeyword,
                BagCode = query.BagCode,
                WorkstationName = query.WorkstationName,
                Status = query.Status,
                ExceptionType = query.ExceptionType,
                ActualChuteId = query.ActualChuteId,
                TargetChuteId = query.TargetChuteId,
                ScannedTimeStart = scannedTimeStart,
                ScannedTimeEnd = scannedTimeEnd
            };
            var response = await queryService.ExecuteAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException exception) {
            Logger.Warn(exception, "Parcel 列表查询参数校验失败。");
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }

    /// <summary>
    /// 处理详情查询请求。
    /// </summary>
    /// <param name="id">包裹 Id。</param>
    /// <param name="queryService">应用层查询服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询结果。</returns>
    private static async Task<IResult> GetParcelByIdAsync(
        long id,
        GetParcelByIdQueryService queryService,
        CancellationToken cancellationToken) {
        try {
            var response = await queryService.ExecuteAsync(id, cancellationToken);
            return response is null
                ? LocalDateTimeParsing.CreateNotFoundProblem(id)
                : Results.Ok(response);
        }
        catch (ArgumentException exception) {
            Logger.Warn(exception, "Parcel 详情查询参数校验失败，Id={ParcelId}", id);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }

    /// <summary>
    /// 处理邻近查询请求。
    /// </summary>
    /// <param name="query">邻近查询参数。</param>
    /// <param name="queryService">应用层查询服务。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>查询结果。</returns>
    private static async Task<IResult> GetAdjacentParcelsAsync(
        [AsParameters] ParcelAdjacentQueryParameters query,
        GetAdjacentParcelsQueryService queryService,
        CancellationToken cancellationToken) {
        if (!query.Id.HasValue) {
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", "id 为必填参数。");
        }

        try {
            var request = new ParcelAdjacentRequest {
                Id = query.Id.Value,
                BeforeCount = query.BeforeCount,
                AfterCount = query.AfterCount
            };
            var response = await queryService.ExecuteAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (KeyNotFoundException exception) {
            Logger.Warn(exception, "Parcel 邻近查询锚点不存在，Id={ParcelId}", query.Id);
            return LocalDateTimeParsing.CreateNotFoundProblem(query.Id.Value);
        }
        catch (ArgumentException exception) {
            Logger.Warn(
                exception,
                "Parcel 邻近查询参数校验失败，Id={ParcelId}, BeforeCount={BeforeCount}, AfterCount={AfterCount}",
                query.Id,
                query.BeforeCount,
                query.AfterCount);
            return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }

    /// <summary>
    /// Parcel 列表查询参数模型。
    /// </summary>
    private sealed record ParcelListQueryParameters {
        /// <summary>
        /// 页码（从 1 开始）。
        /// </summary>
        public int PageNumber { get; init; } = 1;

        /// <summary>
        /// 页大小。
        /// </summary>
        public int PageSize { get; init; } = 20;

        /// <summary>
        /// 条码检索词（MySQL 走 FULLTEXT Boolean 模式，其他 Provider 走 Contains 子串匹配）。
        /// </summary>
        public string? BarCodeKeyword { get; init; }

        /// <summary>
        /// 集包号。
        /// </summary>
        public string? BagCode { get; init; }

        /// <summary>
        /// 工作台名称。
        /// </summary>
        public string? WorkstationName { get; init; }

        /// <summary>
        /// 包裹状态。
        /// </summary>
        public int? Status { get; init; }

        /// <summary>
        /// 包裹异常类型（对应 ParcelExceptionType 枚举数值，null 表示不限异常类型）。
        /// </summary>
        public int? ExceptionType { get; init; }

        /// <summary>
        /// 实际格口 Id。
        /// </summary>
        public long? ActualChuteId { get; init; }

        /// <summary>
        /// 目标格口 Id。
        /// </summary>
        public long? TargetChuteId { get; init; }

        /// <summary>
        /// 扫码开始时间。
        /// </summary>
        public string? ScannedTimeStart { get; init; }

        /// <summary>
        /// 扫码结束时间。
        /// </summary>
        public string? ScannedTimeEnd { get; init; }
    }

    /// <summary>
    /// Parcel 邻近查询参数模型。
    /// </summary>
    private sealed record ParcelAdjacentQueryParameters {
        /// <summary>
        /// 锚点包裹 Id。
        /// </summary>
        public long? Id { get; init; }

        /// <summary>
        /// 锚点记录前查询条数。
        /// </summary>
        public int BeforeCount { get; init; } = 5;

        /// <summary>
        /// 锚点记录后查询条数。
        /// </summary>
        public int AfterCount { get; init; } = 5;
    }
}
