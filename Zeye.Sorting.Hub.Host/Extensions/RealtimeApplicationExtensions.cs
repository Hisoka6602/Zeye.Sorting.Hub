using Microsoft.AspNetCore.Http.Connections;
using Zeye.Sorting.Hub.Host.Hubs;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Zeye.Sorting.Hub.Host.Extensions;

/// <summary>保护实时长连接来源，成功业务写入后唤醒只读订阅。</summary>
public static class RealtimeApplicationExtensions {
    /// <summary>注册同源认证的实时通道及有界消息、调用并发和握手限流。</summary>
    public static IServiceCollection AddSortingRealtime(this IServiceCollection services) {
        services.AddSingleton<RealtimeEndpointDispatcher>();
        services.AddSingleton<RealtimeResourceSignal>();
        services.AddSignalR(options => {
            options.MaximumReceiveMessageSize = 16 * 1024;
            options.MaximumParallelInvocationsPerClient = 4;
            options.EnableDetailedErrors = false;
        });
        services.AddRateLimiter(options => options.AddPolicy("realtime-connect", context => {
            var address = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            // 已协商的长轮询读取、消息提交与断线请求复用连接令牌，不计入新连接预算。
            var startsConnection = context.Request.Path.Value?.TrimEnd('/').EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase) == true
                || context.WebSockets.IsWebSocketRequest || string.IsNullOrEmpty(context.Request.Query["id"]);
            return startsConnection ? RateLimitPartition.GetFixedWindowLimiter(address, _ => new FixedWindowRateLimiterOptions {
                PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            }) : RateLimitPartition.GetNoLimiter("established-transport");
        }));
        return services;
    }

    /// <summary>检查浏览器长连接来源并发送变更通知，不改变写接口授权边界。</summary>
    public static IApplicationBuilder UseSortingRealtime(this IApplicationBuilder app) => app.Use(async (context, next) => {
        if (context.Request.Path.StartsWithSegments("/hubs/sorting")) {
            var origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0 && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase))) {
                context.Response.StatusCode = 403; return;
            }
        }
        await next();
        if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && context.Response.StatusCode is >= 200 and < 300) context.RequestServices.GetRequiredService<RealtimeResourceSignal>().Notify();
    });

    /// <summary>映射认证和限流的实时通道；长连接独立于短 HTTP 查询的超时预算。</summary>
    public static void MapSortingRealtime(this WebApplication app) {
        app.MapHub<SortingRealtimeHub>("/hubs/sorting", options => {
            options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;
            options.CloseOnAuthenticationExpiration = true;
            options.ApplicationMaxBufferSize = 64 * 1024;
            options.TransportMaxBufferSize = 256 * 1024;
        }).DisableRequestTimeout().RequireRateLimiting("realtime-connect");
        app.Services.GetRequiredService<RealtimeEndpointDispatcher>().Configure(app);
    }
}
