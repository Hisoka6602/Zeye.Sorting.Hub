using NLog;
using NLog.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Host.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Mvc;
using Zeye.Sorting.Hub.Host.Options;
using Zeye.Sorting.Hub.Host.Routing;
using Zeye.Sorting.Hub.Host.Extensions;
using Zeye.Sorting.Hub.Host.Queries;
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
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Archiving;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;
using Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Retention;
using Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;
using Zeye.Sorting.Hub.Application.Abstractions.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Zeye.Sorting.Hub.Host.Serialization;
using Zeye.Sorting.Hub.Host.Configuration;

// ──────────────────────────────────────────────────────────
// 启动期引导日志：在 DI 容器就绪之前捕获启动异常
// ──────────────────────────────────────────────────────────
var logger = LogManager.GetCurrentClassLogger();
ExceptionLoggingLifetime? loggingLifetime = null;
const string UrlsConfigKey = "urls";

try {
    // 先加载发布目录中的落盘配置，覆盖 WebApplication 构建之前的启动异常。
    LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(
        Path.Combine(AppContext.BaseDirectory, "nlog.config"), LogManager.LogFactory);
    loggingLifetime = new ExceptionLoggingLifetime(LogManager.LogFactory);
    var startupLogger = LogManager.GetLogger($"{nameof(Program)}.Startup");
    var verifyLatestBackup = args.Contains("--verify-latest-backup", StringComparer.Ordinal);
    // 发布程序始终从自身目录读取配置和前端，不依赖启动时的工作目录。
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
        Args = args.Where(arg => arg != "--verify-latest-backup").ToArray(),
        ContentRootPath = AppContext.BaseDirectory
    });
    ConfigurationBootstrapper.Configure(builder);
    builder.AddNativeServiceLifetime();
    builder.WebHost.ConfigureKestrel(static options => {
        // 请求体硬上限用于在 JSON 反序列化前阻断异常大批次。
        options.Limits.MaxRequestBodySize = 8L * 1024L * 1024L;
        options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    });
    var hostingOptions = builder.Configuration.GetSection("Hosting").Get<HostingOptions>() ?? new HostingOptions();
    var urlsFromConfiguration = builder.Configuration[UrlsConfigKey];
    if (string.IsNullOrWhiteSpace(urlsFromConfiguration)) {
        var bindingUrls = hostingOptions.GetUrlBindings();
        if (bindingUrls.Count > 0) {
            builder.WebHost.UseUrls(bindingUrls.ToArray());
        }
    }

    // ──────────────────────────────────────────────────────
    // NLog：应用、数据库与异常独立落盘，异步批量写入且队列不丢弃日志。
    // 完整堆栈写入 logs/exceptions-<日期>.log；保留窗口由轮转及清理配置决定。
    // ──────────────────────────────────────────────────────
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog(new NLogProviderOptions {
        RemoveLoggerFactoryFilter = false
    });
    var enableQuerySqlLogging = builder.Configuration.GetValue<bool>("Persistence:SqlLogging:EnableQuerySqlLogging");
    if (!enableQuerySqlLogging) {
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", Microsoft.Extensions.Logging.LogLevel.Warning);
    }
    builder.Services.AddSingleton<IPostConfigureOptions<LoggerFilterOptions>, ExceptionLoggingFilter>();
    builder.Services.AddSingleton(LogManager.LogFactory);
    builder.Services.AddSingleton<ExceptionLoggingHubFilter>();
    builder.Services.Configure<HubOptions>(static options => options.AddFilter<ExceptionLoggingHubFilter>());
    builder.Services.AddSingleton<SlowQueryHubFilter>();
    builder.Services.Configure<HubOptions>(static options => options.AddFilter<SlowQueryHubFilter>());

    builder.Services.Configure<LogCleanupSettings>(
        builder.Configuration.GetSection("LogCleanup"));
    builder.Services.Configure<HostingOptions>(builder.Configuration.GetSection("Hosting"));
    builder.Services.Configure<AuditReadOnlyApiOptions>(builder.Configuration.GetSection(AuditReadOnlyApiOptions.SectionName));
    builder.Services.AddOptions<ResourceThresholdsOptions>().Bind(builder.Configuration.GetSection(ResourceThresholdsOptions.SectionName))
        .Validate(static options => options.MaxConnectionPoolSize is >= 1 and <= 10000 && options.MemoryWarningThresholdMB is >= 0 and <= 1048576, "连接池阈值为 1~10000，内存阈值为 0~1048576 MiB")
        .Validate(static options => options.HandleWarningThreshold is >= 0 and <= 1000000 && options.MinimumDiskFreeMB is >= 0 and <= 1048576, "句柄阈值为 0~1000000，磁盘剩余空间阈值为 0~1048576 MiB")
        .Validate(static options => options.SampleIntervalSeconds is >= 10 and <= 3600, "资源采样间隔为 10~3600 秒")
        .ValidateOnStart();
    builder.Services.Configure<HostOptions>(static options => {
        options.ServicesStopConcurrently = true;
        options.ShutdownTimeout = TimeSpan.FromSeconds(30);
        options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost;
    });
    builder.Services.AddObjectStorageOptions(builder.Configuration);
    builder.Services.AddHostedService<LogCleanupService>();
    builder.Services.AddHostedService<DevelopmentBrowserLauncherHostedService>();
    builder.Services.AddSingleton<MigrationGovernanceHostedService>();
    builder.Services.AddHostedService(static serviceProvider =>
        serviceProvider.GetRequiredService<MigrationGovernanceHostedService>());
    builder.Services.AddSingleton<SafeExecutor>();
    builder.Services.AddSingleton<ConfigChangeHistoryStore<LogCleanupSettings>>();
    builder.Services.AddSortingHubPersistence(builder.Configuration);
    builder.Services.AddMinioObjectStorage(builder.Configuration);
    // 显式替换 IAutoTuningObservability：无论 AddSortingHubPersistence 内是否已注册占位空实现，
    // Replace 均保证最终容器中只存在真实的日志观测实现，与注册顺序无关。
    builder.Services.Replace(ServiceDescriptor.Singleton<IAutoTuningObservability, AutoTuningLoggerObservability>());
    // 数据库启动链路严格按“迁移治理 -> 初始化 -> 预热/后台任务”顺序注册，避免后台查询抢跑迁移。
    builder.Services.AddHostedService<DatabaseInitializerHostedService>();
    builder.Services.AddHostedService<LegacyConfigurationMigrationHostedService>();
    builder.Services.AddHostedService<BuiltInAccountHostedService>();
    builder.Services.AddHostedService<Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels.PersistenceReadSnapshotRefreshService>();
    builder.Services.AddHostedService<DatabaseConnectionWarmupHostedService>();
    builder.Services.AddHostedService<ParcelDurationBackfillHostedService>();
    builder.Services.AddHostedService<ParcelDwsMeasurementBackfillHostedService>();
    builder.Services.AddHostedService<ParcelBatchWriteFlushHostedService>();
    builder.Services.AddHostedService<ShardingPrebuildHostedService>();
    builder.Services.AddHostedService<ShardingInspectionHostedService>();
    builder.Services.AddHostedService<DataArchiveHostedService>();
    builder.Services.AddHostedService<BackupHostedService>();
    builder.Services.AddHostedService<DataRetentionHostedService>();
    builder.Services.AddHostedService<BaselineDataValidationHostedService>();
    builder.Services.AddHostedService<QueryGovernanceReportHostedService>();
    builder.Services.AddHostedService<DatabaseAutoTuningHostedService>();
    builder.Services.AddHostedService<RuntimeResourceMonitorHostedService>();
    builder.Services.AddSingleton(TimeProvider.System);
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
            tags: ["deep"])
        .AddCheck<BackupHealthCheck>(
            name: "backup",
            tags: ["deep"])
        .AddCheck<ReadOnlyDatabaseHealthCheck>(
            name: "read-only-database",
            tags: ["deep"])
        .AddCheck<DataRetentionHealthCheck>(
            name: "data-retention",
            tags: ["deep"])
        .AddCheck<MigrationGovernanceHealthCheck>(
            name: "migration-governance",
            tags: ["deep"])
        .AddCheck<ShardingGovernanceHealthCheck>(
            name: "sharding-governance",
            tags: ["deep"])
        .AddCheck<RuntimeResourceHealthCheck>(
            name: "runtime-resources",
            tags: ["deep"], timeout: TimeSpan.FromSeconds(5));
    builder.Services.AddProblemDetails();
    builder.Services.ConfigureHttpJsonOptions(static options => {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, SortingHubJsonSerializerContext.Default);
    });
    builder.Services.AddResponseCompression(options => {
        options.EnableForHttps = true;
        options.Providers.Add<BrotliCompressionProvider>();
        options.Providers.Add<GzipCompressionProvider>();
        options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(static options => {
        options.Level = CompressionLevel.Fastest;
    });
    builder.Services.Configure<GzipCompressionProviderOptions>(static options => {
        options.Level = CompressionLevel.Fastest;
    });
    builder.Services.AddRequestTimeouts(options => {
        options.DefaultPolicy = new RequestTimeoutPolicy {
            Timeout = TimeSpan.FromSeconds(15),
            TimeoutStatusCode = StatusCodes.Status504GatewayTimeout
        };
    });
    builder.Services.AddRateLimiter(options => {
        options.AddPolicy("account-login", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
                PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            }));
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(static _ =>
            RateLimitPartition.GetConcurrencyLimiter(
                "global-http-concurrency",
                static _ => new ConcurrencyLimiterOptions {
                    PermitLimit = 200,
                    QueueLimit = 100,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }));
    });
    builder.Services.AddOutputCache(options => {
        options.AddPolicy("short-diagnostics", policy => policy.Expire(TimeSpan.FromSeconds(3)));
    });
    builder.Services
        .AddAuthentication(GuardedAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, GuardedAuthenticationHandler>(GuardedAuthenticationHandler.SchemeName, static _ => { });
    builder.Services.AddAuthorization();
    var configurationDatabasePath = ((IConfigurationRoot)builder.Configuration).Providers
        .OfType<Zeye.Sorting.Hub.Infrastructure.Configuration.RuntimeConfigurationProvider>().Single().StoragePath;
    builder.Services.AddSortingHubAccess(builder.Environment.ContentRootPath, configurationDatabasePath);
    builder.Services.AddSortingRealtime();
    builder.Services.AddFusionIngestion(builder.Configuration, builder.Environment.ContentRootPath);
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
    builder.Services.AddScoped<OperationalPartitionReadService>();
    builder.Services.AddSingleton<IDatabaseBackupArtifactService, DatabaseBackupArtifactService>();
    builder.Services.AddSingleton<OperationalPolicyService>();
    builder.Services.AddSingleton<Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.PartitionMaintenanceService>();
    builder.Services.AddScoped<ManagedDocumentService>();
    builder.Services.AddScoped<ParcelCleanupHistoryService>();
    builder.Services.AddScoped<GetParcelCursorPagedQueryService>();
    builder.Services.AddScoped<GetParcelByIdQueryService>();
    builder.Services.AddScoped<ParcelProcessingApplicationService>();
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
    builder.Services.AddWebRequestAuditLogging(builder.Configuration);

    // 网页和本机配置入口不依赖业务数据库；业务任务由统一启动链进行就绪隔离。
    DatabaseStartupHostedService.Register(builder.Services);
    var app = builder.Build();
    app.Services.GetRequiredService<Zeye.Sorting.Hub.Infrastructure.Configuration.ConfigurationHistoryStore>()
        .AttachQueryDiagnostics(app.Services.GetRequiredService<SlowQueryAutoTuningPipeline>());
    if (verifyLatestBackup) {
        var artifacts = app.Services.GetRequiredService<IDatabaseBackupArtifactService>();
        var latest = (await artifacts.ListAsync(CancellationToken.None)).FirstOrDefault() ?? throw new InvalidOperationException("没有可用于隔离恢复核验的实际备份。");
        var verified = await artifacts.RestoreIsolatedAsync(latest.Id, CancellationToken.None);
        Console.WriteLine($"备份隔离恢复核验完成：Id={verified.Id}, Tables={verified.TableRows.Count}, Rows={verified.TableRows.Values.Sum()}, Database={verified.RestoredDatabase}");
        await app.DisposeAsync();
        return;
    }
    app.UseResponseCompression();

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
    app.UseBundledWebUi();
    app.UseRouting();
    // 必须位于业务认证、审计和实时接口之前，避免配置入口再次依赖账号数据库。
    app.UseMiddleware<DatabaseSetupMiddleware>();
    app.UseSortingRealtime();
    // 路由解析后再进入审计，使中间件可以按端点元数据和路径排除探针流量。
    app.UseWebRequestAuditLogging();
    app.UseRequestTimeouts();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseSortingHubAccess();
    app.UseOutputCache();
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
    .DisableRateLimiting()
    .WithSummary("存活探针")
    .WithDescription("进程级存活探测，不包含依赖检查。容器重启策略依据此端点决策。");

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
        // 就绪探针：检查 ready 标签下的所有项（含数据库连接）
        Predicate = static check => check.Tags.Contains("ready"),
        ResponseWriter = HealthCheckResponseWriter.WriteJsonResponseAsync
    })
    .WithName("ReadinessProbe")
    .DisableRateLimiting()
    .WithSummary("就绪探针")
    .WithDescription("包含数据库连接探测，表示实例可接受流量。流量切入决策依据此端点。");

    app.MapHealthChecks("/health/deep", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions {
        // 深度诊断同时执行流量关键项与后台治理项，不应作为高频容器探针。
        Predicate = static check => check.Tags.Contains("ready") || check.Tags.Contains("deep"),
        ResponseWriter = HealthCheckResponseWriter.WriteJsonResponseAsync
    })
    .WithName("DeepHealthDiagnostics")
    .DisableRateLimiting()
    .WithSummary("深度健康诊断")
    .WithDescription("包含备份、归档、迁移与分片治理，仅用于低频运维诊断。");

    // 兼容端点：保持旧版 /health 接入链路可用
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
        .WithName("HealthCheck")
        .DisableRateLimiting()
        .WithSummary("服务健康检查（兼容端点）")
        .WithDescription("兼容历史接入路径，等价于存活探针。建议新接入方改用 /health/live 或 /health/ready。");
    // Parcel 只读查询端点：统一走 Application 查询服务，不直接暴露领域模型。
    app.MapParcelReadOnlyApis();
    app.MapParcelAnalyticsApis();
    app.MapParcelWorkbenchApis();
    app.MapParcelProcessingApis();
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
    app.MapOperationalReadApis();
    app.MapRuntimeConfigurationApis();
    app.MapRuleManagementApis();
    app.MapAccessApis();
    app.MapFusionIngestion();
    app.MapFusionConfiguration();
    // 实时读取分发固定正式路由数据源，必须在所有接口组注册完成后初始化。
    app.MapSortingRealtime();

    app.Run();
}
catch (Microsoft.Extensions.Hosting.HostAbortedException) {
    // EF Core 设计时工具有意终止宿主解析，不属于启动故障。
    throw;
}
catch (Exception ex) {
    // 捕获启动期间的顶层异常，确保日志落盘后再退出
    logger.Fatal(ex, "宿主启动失败，程序即将退出");
    throw;
}
finally {
    // 退出前刷新异步队列，再释放文件句柄；初始化失败也执行刷新。
    if (loggingLifetime is null) LogManager.Flush(TimeSpan.FromSeconds(10));
    else loggingLifetime.Dispose();
    LogManager.Shutdown();
}
