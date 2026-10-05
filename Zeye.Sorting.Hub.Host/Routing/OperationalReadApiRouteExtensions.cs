using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

using Zeye.Sorting.Hub.Application.Abstractions.Storage;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>向运维页面提供经过白名单筛选的真实运行信息，不暴露连接字符串和备份命令。</summary>
public static class OperationalReadApiRouteExtensions {
    /// <summary>记录运维请求中的参数校验及执行异常。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>注册运维页面使用的只读 API 路由。</summary>
    public static IEndpointRouteBuilder MapOperationalReadApis(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/operations").WithTags("Operations");
        group.MapGet("/backup", async (BackupVerificationService service, Microsoft.Extensions.Options.IOptions<BackupOptions> options, OperationalPolicyService policies, CancellationToken ct) => {
            var record = service.GetLastExecutionRecord();
            var policy = await policies.ReadAsync(ct);
            return Results.Ok(new {
                status = record?.Status ?? "Unknown",
                summary = record?.Summary ?? "备份治理尚未生成执行记录。",
                provider = record?.ProviderName,
                database = record?.DatabaseName,
                recordedAtLocal = record?.RecordedAtLocal,
                verifiedBackupAtLocal = record?.VerifiedBackupAtLocal,
                hasBackupFile = record?.HasBackupFile ?? false,
                isBackupFileFresh = record?.IsBackupFileFresh ?? false,
                isEnabled = options.Value.IsEnabled,
                isDryRun = options.Value.DryRun,
                pollIntervalMinutes = options.Value.PollIntervalMinutes,
                maxAllowedBackupAgeHours = options.Value.MaxAllowedBackupAgeHours
                , policy.AutomaticBackups, policy.BackupIntervalMinutes
            });
        }).WithSummary("读取真实备份治理状态")
            .WithDescription("返回最近备份验证结果、文件新鲜度及自动备份策略，用于判断备份是否可用和是否需要补充备份。");
        group.MapGet("/backup/artifacts", async (IDatabaseBackupArtifactService service, CancellationToken ct) => Results.Ok(new { isSupported = service.IsSupported, artifacts = await service.ListAsync(ct) }))
            .WithSummary("查询数据库备份文件").WithDescription("列出可用备份及验证信息，用于下载、留存和隔离恢复；同时返回当前数据库是否支持此备份方式。");
        group.MapPost("/backup/artifacts", async (IDatabaseBackupArtifactService service, BackupVerificationService verification, HttpContext context, CancellationToken ct) => {
            try { var artifact = await service.CreateAsync(context.User.Identity?.Name ?? "操作员", ct); await verification.ExecuteAsync(ct); return Results.Ok(artifact); }
            catch (InvalidOperationException ex) { Logger.Warn(ex, "创建数据库备份失败。"); return Results.Problem(statusCode: 409, detail: ex.Message); }
        }).WithRequestTimeout(TimeSpan.FromMinutes(10)).WithSummary("创建并验证数据库备份")
            .WithDescription("为当前业务数据库生成备份文件，记录操作人并立即执行备份验证；返回备份信息，当前环境不支持或配置不满足时拒绝执行。");
        group.MapPost("/backup/artifacts/{id}/restore-isolated", async (string id, IDatabaseBackupArtifactService service, CancellationToken ct) => {
            try { return Results.Ok(await service.RestoreIsolatedAsync(id, ct)); }
            catch (ArgumentException ex) { Logger.Debug(ex, "隔离恢复请求的备份编号无效。"); return Results.Problem(statusCode: 400, detail: "备份编号无效。"); }
            catch (FileNotFoundException ex) { Logger.Debug(ex, "隔离恢复请求的备份文件不存在。"); return Results.NotFound(); }
            catch (InvalidOperationException ex) { Logger.Warn(ex, "数据库备份隔离恢复失败。"); return Results.Problem(statusCode: 409, detail: ex.Message); }
            catch (MySqlConnector.MySqlException ex) { Logger.Error(ex, "数据库备份隔离恢复发生数据库异常。"); return Results.Problem(statusCode: 409, detail: "隔离恢复失败，请检查 zeye_restore_ 数据库前缀的建库授权及服务器日志。当前业务库未被修改。"); }
        }).WithRequestTimeout(TimeSpan.FromMinutes(10)).WithSummary("将备份恢复到隔离数据库")
            .WithDescription("按备份编号恢复到独立验证数据库，核查备份可恢复性并返回验证结果；不覆盖当前业务库，缺失备份或未获建库授权时返回失败。");
        group.MapGet("/backup/artifacts/{id}/download", async (string id, IDatabaseBackupArtifactService service, HttpContext context, CancellationToken ct) => {
            try { var path = await service.DownloadPathAsync(id, ct); context.Response.Headers.CacheControl = "private, no-store"; return Results.File(path, "application/zip", "zeye-backup-" + id + ".zip"); }
            catch (ArgumentException ex) { Logger.Debug(ex, "备份下载请求的备份编号无效。"); return Results.Problem(statusCode: 400, detail: "备份编号无效。"); }
            catch (FileNotFoundException ex) { Logger.Debug(ex, "备份下载请求的文件不存在。"); return Results.NotFound(); }
            catch (InvalidOperationException ex) { Logger.Warn(ex, "读取数据库备份下载路径失败。"); return Results.Problem(statusCode: 409, detail: ex.Message); }
        }).WithSummary("下载指定数据库备份")
            .WithDescription("按备份编号下载 ZIP 备份文件，用于离线留存或恢复准备；只访问备份目录内已登记文件，文件缺失返回未找到。");

        group.MapGet("/partitions", async (OperationalPartitionReadService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(cancellationToken))).WithSummary("读取物理分表目录与预建计划")
            .WithDescription("查询包裹物理分表、索引状态和未来窗口的预建计划，用于分区管理及建表风险核查；本接口只读。");
        group.MapPost("/partitions/prebuild", async (Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.PartitionMaintenanceService service, OperationalPolicyService policy, CancellationToken ct) => {
            try { return Results.Ok(await service.ExecuteAsync(ct, (await policy.ReadAsync(ct)).PrebuildAheadHours)); }
            catch (InvalidOperationException ex) { Logger.Warn(ex, "包裹物理分表预建未完成。"); return Results.Problem(statusCode: 409, detail: "未允许实际建表，或已有表结构与模型不一致。请检查服务器建表配置和日志。"); }
        }).WithRequestTimeout(TimeSpan.FromMinutes(5)).WithSummary("实际预建当前及未来窗口的包裹分表")
            .WithDescription("按当前保存的预建窗口和服务端建表策略维护物理分表及索引；保留建表隔离和结构一致性校验，未允许实际建表或结构不一致时拒绝执行。");
        group.MapGet("/configuration/policy", async (OperationalPolicyService policy, CancellationToken ct) => Results.Ok(await policy.ReadAsync(ct)))
            .WithSummary("读取运维策略").WithDescription("返回自动备份开关、备份间隔、分表预建窗口及版本，用于系统配置展示和后续提交的并发校验。");
        group.MapPut("/configuration/policy", async (OperationalPolicy request, OperationalPolicyService policy, CancellationToken ct) => {
            try { var saved = await policy.WriteAsync(request, ct); return saved is null ? Results.Problem(statusCode: 409, detail: "运维策略已被更新，请刷新后重试。") : Results.Ok(saved); }
            catch (ArgumentException ex) { Logger.Debug(ex, "保存运维策略的请求参数无效。"); return Results.Problem(statusCode: 400, detail: ex.Message); }
        }).WithSummary("保存运维策略")
            .WithDescription("按策略版本保存自动备份开关、备份间隔和分表预建窗口，跨服务重启保留；参数无效或存在并发修改时拒绝覆盖。");

        group.MapGet("/configuration", (IConfiguration config, IHostEnvironment environment) => Results.Ok(new {
            environment = environment.EnvironmentName,
            settings = new[] {
                new { category = "运行", name = "环境", value = environment.EnvironmentName },
                new { category = "数据库", name = "数据库提供程序", value = config["Persistence:Provider"] ?? "未配置" },
                new { category = "数据库", name = "分表粒度", value = config["Persistence:Sharding:Strategy:Time:Granularity"] ?? "未配置" },
                new { category = "数据库", name = "允许物理分表创建", value = config.GetValue("Persistence:Sharding:WriteRouting:AllowTableCreation", false) ? "是" : "否" },
                new { category = "数据库", name = "分表创建预演", value = config.GetValue("Persistence:Sharding:WriteRouting:DryRun", true) ? "启用" : "关闭" },
                new { category = "审计", name = "只读审计 API", value = config.GetValue("AuditReadOnlyApi:Enabled", false) ? "启用" : "关闭" },
                new { category = "保留", name = "备份文件最长允许年龄", value = $"{config.GetValue("Persistence:Backup:MaxAllowedBackupAgeHours", 24)} 小时" },
                new { category = "诊断", name = "备份治理", value = config.GetValue("Persistence:Backup:IsEnabled", true) ? "启用" : "关闭" },
                new { category = "诊断", name = "备份轮询间隔", value = $"{config.GetValue("Persistence:Backup:PollIntervalMinutes", 60)} 分钟" },
                new { category = "诊断", name = "连接预热", value = config.GetValue("Persistence:Diagnostics:IsWarmupEnabled", true) ? "启用" : "关闭" }
            }
        })).WithSummary("读取安全的当前生效配置")
            .WithDescription("按白名单返回运行环境、数据库提供程序、分表和治理设置，用于查看服务器生效配置；不返回连接字符串、凭据或备份命令。");
        return routes;
    }
}
