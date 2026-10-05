using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Connections;
using Zeye.Sorting.Hub.Application.Abstractions.Integrations;
using Zeye.Sorting.Hub.Application.Services.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Host.Authentication;
using Zeye.Sorting.Hub.Host.Hubs;
using Zeye.Sorting.Hub.Host.HostedServices;

namespace Zeye.Sorting.Hub.Host.Extensions;

/// <summary>组装融合来源入口与恢复服务，网页实时通道保持原有独立限额。</summary>
public static class FusionIngestionExtensions {
    /// <summary>接收端映射及查询输入校验诊断日志。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    /// <summary>注册机器认证、耐久接收实现及有界投影任务。</summary>
    public static IServiceCollection AddFusionIngestion(this IServiceCollection services, IConfiguration configuration, string contentRoot) {
        services.Configure<FusionIngestionOptions>(configuration.GetSection("FusionIngestion"));
        services.AddSingleton<IFusionIngestionGateway>(provider => new FusionIngestionService(
            provider.GetRequiredService<IDbContextFactory<SortingHubDbContext>>(),
            provider.GetRequiredService<IOptions<FusionIngestionOptions>>(), contentRoot));
        services.AddSingleton<IFusionDiscoveryService, FusionDiscoveryService>();
        services.AddScoped<FusionProjectionService>();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, FusionMachineAuthenticationHandler>(FusionMachineAuthenticationHandler.SchemeName, _ => { });
        services.AddSignalR().AddHubOptions<FusionIngestionHub>(options => {
            options.MaximumReceiveMessageSize = 1024 * 1024;
            options.MaximumParallelInvocationsPerClient = 1;
            options.EnableDetailedErrors = false;
        });
        services.AddHostedService<FusionProjectionHostedService>();
        services.AddHostedService<FusionDiscoveryHostedService>();
        return services;
    }

    /// <summary>映射六方法机器通道并共用握手限流；前端 Cookie 无法进入此 Hub。</summary>
    public static void MapFusionIngestion(this WebApplication app) {
        app.MapHub<FusionIngestionHub>(FusionProtocol.HubPath, options => {
            options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
            options.ApplicationMaxBufferSize = 2 * 1024 * 1024; options.TransportMaxBufferSize = 2 * 1024 * 1024;
        }).DisableRequestTimeout().RequireRateLimiting("realtime-connect")
            .WithSummary("Fusion 工作台实时接收通道")
            .WithDescription("通过机器认证建立 SignalR 连接，接收来源登记、处理事实、心跳及分块图片上传；协商端点确定连接和传输方式，耐久接收后确认，浏览器登录会话不能替代机器认证。");
        app.MapGet("/api/parcels/fusion/sources", async (IFusionIngestionGateway gateway, CancellationToken token) =>
            Results.Ok(await gateway.GetSourcesAsync(token))).WithTags("Fusion").WithSummary("读取已登记分拣工作台及心跳状态")
            .WithDescription("返回登记的来源工作台身份、最近心跳、在线状态、待确认及舍弃数量，用于多工作台监控；不返回机器认证密钥。");
        app.MapGet("/api/diagnostics/fusion/facts", async (string sourceInstanceId, string? journalId, int? limit, IFusionIngestionGateway gateway, CancellationToken token) => {
            try { return Results.Ok(await gateway.GetFactsAsync(sourceInstanceId, journalId, limit ?? 50, token)); }
            catch (ArgumentException exception) { Logger.Debug(exception, "Fusion 原始事实查询参数无效。"); return Results.Problem(statusCode: 400, detail: exception.Message); }
        }).WithTags("Fusion").WithSummary("追溯来源原始事实及业务投影结果")
            .WithDescription("按来源实例和可选日志编号查询耐久保存的原始处理事实、接收时间及投影结果，供超级管理员排查上报、去重与包裹投影；查询数量受服务端限制。");
        app.MapGet("/api/parcels/fusion/images/{key}/content", async (string key, HttpContext context, IFusionIngestionGateway gateway, CancellationToken token) => {
            var image = await gateway.ReadImageAsync(key, token);
            if (image is null) return Results.NotFound();
            context.Response.Headers.XContentTypeOptions = "nosniff"; context.Response.Headers.CacheControl = "private, no-store";
            return Results.Stream(image.Value.Content, image.Value.ContentType);
        }).WithTags("Fusion").WithSummary("读取已经完整落盘的来源图片")
            .WithDescription("按图片标识读取来源工作台上传且已完成校验的包裹图片，返回实际图片内容；未完成、不存在或已不可访问的图片返回未找到。");
    }
}
