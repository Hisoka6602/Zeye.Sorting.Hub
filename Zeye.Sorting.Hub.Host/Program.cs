using NLog;
using Microsoft.AspNetCore.Diagnostics;
using System.Globalization;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
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
    var builder = WebApplication.CreateBuilder(args);

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
    builder.Services.AddProblemDetails();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddScoped<GetParcelPagedQueryService>();
    builder.Services.AddScoped<GetParcelByIdQueryService>();
    builder.Services.AddScoped<GetAdjacentParcelsQueryService>();

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
    if (bool.TryParse(app.Configuration["Hosting:EnableHttpsRedirection"], out var enableHttpsRedirection)
        && enableHttpsRedirection) {
        app.UseHttpsRedirection();
    }
    if (app.Environment.IsDevelopment()) {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // 最小探活端点：用于容器/网关健康检查，不承载业务语义
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));
    // Parcel 只读查询端点：统一走 Application 查询服务，不直接暴露领域模型。
    app.MapParcelReadOnlyApis();

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
internal static class ParcelReadOnlyApiRouteExtensions {
    /// <summary>
    /// 邻近查询支持的本地时间格式（不允许 UTC/offset 表达）。
    /// </summary>
    private static readonly string[] LocalDateTimeFormats = [
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.fff"
    ];

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
            .Produces<ParcelListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:long}", GetParcelByIdAsync)
            .WithName("GetParcelById")
            .WithSummary("按 Id 查询 Parcel 详情")
            .Produces<ParcelDetailResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/adjacent", GetAdjacentParcelsAsync)
            .WithName("GetAdjacentParcels")
            .WithSummary("按扫描时间查询 Parcel 邻近记录")
            .Produces<ParcelAdjacentResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

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
        if (!IsLocalOrUnspecifiedTime(query.ScannedTimeStart)
            || !IsLocalOrUnspecifiedTime(query.ScannedTimeEnd)) {
            return CreateBadRequestProblem("请求参数无效", "scannedTimeStart/scannedTimeEnd 必须使用本地时间语义，不允许 UTC 或时区偏移。");
        }

        try {
            var request = new ParcelListRequest {
                PageNumber = query.PageNumber,
                PageSize = query.PageSize,
                BarCodeKeyword = query.BarCodeKeyword,
                BagCode = query.BagCode,
                WorkstationName = query.WorkstationName,
                Status = query.Status,
                ActualChuteId = query.ActualChuteId,
                TargetChuteId = query.TargetChuteId,
                ScannedTimeStart = query.ScannedTimeStart,
                ScannedTimeEnd = query.ScannedTimeEnd
            };
            var response = await queryService.ExecuteAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException exception) {
            Logger.Warn(exception, "Parcel 列表查询参数校验失败。");
            return CreateBadRequestProblem("请求参数无效", exception.Message);
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
                ? Results.Problem(
                    title: "资源不存在",
                    detail: $"未找到 Id 为 {id} 的包裹。",
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(response);
        }
        catch (ArgumentException exception) {
            Logger.Warn(exception, "Parcel 详情查询参数校验失败，Id={ParcelId}", id);
            return CreateBadRequestProblem("请求参数无效", exception.Message);
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
        if (!TryParseLocalDateTime(query.ScannedTime, out var scannedTime)) {
            return CreateBadRequestProblem("请求参数无效", "scannedTime 必须是本地时间格式，且不允许包含 UTC 或时区偏移。");
        }

        try {
            var request = new ParcelAdjacentRequest {
                ScannedTime = scannedTime,
                BeforeCount = query.BeforeCount,
                AfterCount = query.AfterCount
            };
            var response = await queryService.ExecuteAsync(request, cancellationToken);
            return Results.Ok(response);
        }
        catch (ArgumentException exception) {
            Logger.Warn(
                exception,
                "Parcel 邻近查询参数校验失败，ScannedTime={ScannedTime}, BeforeCount={BeforeCount}, AfterCount={AfterCount}",
                query.ScannedTime,
                query.BeforeCount,
                query.AfterCount);
            return CreateBadRequestProblem("请求参数无效", exception.Message);
        }
    }

    /// <summary>
    /// 尝试按本地时间语义解析时间字符串。
    /// </summary>
    /// <param name="input">输入时间字符串。</param>
    /// <param name="parsedTime">解析结果。</param>
    /// <returns>是否解析成功。</returns>
    private static bool TryParseLocalDateTime(string? input, out DateTime parsedTime) {
        if (string.IsNullOrWhiteSpace(input)
            || input.Contains('Z', StringComparison.OrdinalIgnoreCase)
            || input.Contains('+', StringComparison.Ordinal)
            || input.LastIndexOf('-') > "yyyy-MM-dd".Length - 1) {
            parsedTime = default;
            return false;
        }

        return DateTime.TryParseExact(
            input,
            LocalDateTimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
            out parsedTime);
    }

    /// <summary>
    /// 校验时间值是否为本地时间语义（允许空值、Local、Unspecified）。
    /// </summary>
    /// <param name="dateTime">待校验时间。</param>
    /// <returns>是否满足本地时间语义。</returns>
    private static bool IsLocalOrUnspecifiedTime(DateTime? dateTime) {
        return !dateTime.HasValue
            || dateTime.Value.Kind == DateTimeKind.Local
            || dateTime.Value.Kind == DateTimeKind.Unspecified;
    }

    /// <summary>
    /// 创建统一的 400 ProblemDetails 响应。
    /// </summary>
    /// <param name="title">问题标题。</param>
    /// <param name="detail">问题详情。</param>
    /// <returns>统一错误响应。</returns>
    private static IResult CreateBadRequestProblem(string title, string detail) {
        return Results.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
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
        /// 条码关键字。
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
        public DateTime? ScannedTimeStart { get; init; }

        /// <summary>
        /// 扫码结束时间。
        /// </summary>
        public DateTime? ScannedTimeEnd { get; init; }
    }

    /// <summary>
    /// Parcel 邻近查询参数模型。
    /// </summary>
    private sealed record ParcelAdjacentQueryParameters {
        /// <summary>
        /// 基准扫码时间（本地时间字符串）。
        /// </summary>
        public string? ScannedTime { get; init; }

        /// <summary>
        /// 基准时间前查询条数。
        /// </summary>
        public int BeforeCount { get; init; } = 5;

        /// <summary>
        /// 基准时间后查询条数。
        /// </summary>
        public int AfterCount { get; init; } = 5;
    }
}
