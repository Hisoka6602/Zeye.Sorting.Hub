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
        }).DisableRequestTimeout().RequireRateLimiting("realtime-connect");
        app.MapGet("/api/parcels/fusion/sources", async (IFusionIngestionGateway gateway, CancellationToken token) =>
            Results.Ok(await gateway.GetSourcesAsync(token))).WithTags("Fusion").WithSummary("读取已登记分拣工作台及心跳状态");
        app.MapGet("/api/diagnostics/fusion/facts", async (string sourceInstanceId, string? journalId, int? limit, IFusionIngestionGateway gateway, CancellationToken token) => {
            try { return Results.Ok(await gateway.GetFactsAsync(sourceInstanceId, journalId, limit ?? 50, token)); }
            catch (ArgumentException exception) { return Results.Problem(statusCode: 400, detail: exception.Message); }
        }).WithTags("Fusion").WithSummary("追溯来源原始事实及业务投影结果");
        app.MapGet("/api/parcels/fusion/images/{key}/content", async (string key, HttpContext context, IFusionIngestionGateway gateway, CancellationToken token) => {
            var image = await gateway.ReadImageAsync(key, token);
            if (image is null) return Results.NotFound();
            context.Response.Headers.XContentTypeOptions = "nosniff"; context.Response.Headers.CacheControl = "private, no-store";
            return Results.Stream(image.Value.Content, image.Value.ContentType);
        }).WithTags("Fusion").WithSummary("读取已经完整落盘的来源图片");
    }
}
