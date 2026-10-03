using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

using Zeye.Sorting.Hub.Application.Abstractions.Storage;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>向运维页面提供经过白名单筛选的真实运行信息，不暴露连接字符串和备份命令。</summary>
public static class OperationalReadApiRouteExtensions {
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
        }).WithSummary("读取真实备份治理状态");
        group.MapGet("/backup/artifacts", async (IDatabaseBackupArtifactService service, CancellationToken ct) => Results.Ok(new { isSupported = service.IsSupported, artifacts = await service.ListAsync(ct) }));
        group.MapPost("/backup/artifacts", async (IDatabaseBackupArtifactService service, BackupVerificationService verification, HttpContext context, CancellationToken ct) => {
            try { var artifact = await service.CreateAsync(context.User.Identity?.Name ?? "操作员", ct); await verification.ExecuteAsync(ct); return Results.Ok(artifact); }
            catch (InvalidOperationException ex) { return Results.Problem(statusCode: 409, detail: ex.Message); }
        }).WithRequestTimeout(TimeSpan.FromMinutes(10));
        group.MapPost("/backup/artifacts/{id}/restore-isolated", async (string id, IDatabaseBackupArtifactService service, CancellationToken ct) => {
            try { return Results.Ok(await service.RestoreIsolatedAsync(id, ct)); }
            catch (ArgumentException) { return Results.Problem(statusCode: 400, detail: "备份编号无效。"); }
            catch (FileNotFoundException) { return Results.NotFound(); }
            catch (InvalidOperationException ex) { return Results.Problem(statusCode: 409, detail: ex.Message); }
            catch (MySqlConnector.MySqlException) { return Results.Problem(statusCode: 409, detail: "隔离恢复失败，请检查 zeye_restore_ 数据库前缀的建库授权及服务器日志。当前业务库未被修改。"); }
        }).WithRequestTimeout(TimeSpan.FromMinutes(10));
        group.MapGet("/backup/artifacts/{id}/download", async (string id, IDatabaseBackupArtifactService service, HttpContext context, CancellationToken ct) => {
            try { var path = await service.DownloadPathAsync(id, ct); context.Response.Headers.CacheControl = "private, no-store"; return Results.File(path, "application/zip", "zeye-backup-" + id + ".zip"); }
            catch (ArgumentException) { return Results.Problem(statusCode: 400, detail: "备份编号无效。"); }
            catch (FileNotFoundException) { return Results.NotFound(); }
            catch (InvalidOperationException ex) { return Results.Problem(statusCode: 409, detail: ex.Message); }
        });

        group.MapGet("/partitions", async (OperationalPartitionReadService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(cancellationToken))).WithSummary("读取物理分表目录与预建计划");
        group.MapPost("/partitions/prebuild", async (Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding.PartitionMaintenanceService service, OperationalPolicyService policy, CancellationToken ct) => {
            try { return Results.Ok(await service.ExecuteAsync(ct, (await policy.ReadAsync(ct)).PrebuildAheadHours)); }
            catch (InvalidOperationException) { return Results.Problem(statusCode: 409, detail: "未允许实际建表，或已有表结构与模型不一致。请检查服务器建表配置和日志。"); }
        }).WithRequestTimeout(TimeSpan.FromMinutes(5)).WithSummary("实际预建当前及未来窗口的包裹分表");
        group.MapGet("/configuration/policy", async (OperationalPolicyService policy, CancellationToken ct) => Results.Ok(await policy.ReadAsync(ct)));
        group.MapPut("/configuration/policy", async (OperationalPolicy request, OperationalPolicyService policy, CancellationToken ct) => {
            try { var saved = await policy.WriteAsync(request, ct); return saved is null ? Results.Problem(statusCode: 409, detail: "运维策略已被更新，请刷新后重试。") : Results.Ok(saved); }
            catch (ArgumentException ex) { return Results.Problem(statusCode: 400, detail: ex.Message); }
        });

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
        })).WithSummary("读取安全的当前生效配置");
        return routes;
    }
}
