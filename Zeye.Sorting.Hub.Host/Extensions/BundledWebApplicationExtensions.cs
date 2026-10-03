using Microsoft.AspNetCore.StaticFiles;

namespace Zeye.Sorting.Hub.Host.Extensions;

/// <summary>将发布包内的前端与业务 API 交给同一个宿主运行。</summary>
public static class BundledWebApplicationExtensions {
    /// <summary>提供公开页面与静态资源，保留 API、探针和其他服务路由的真实响应。</summary>
    public static WebApplication UseBundledWebUi(this WebApplication app) {
        // 启动时检查发布资源；只发布 API 的场景继续使用既有管线。
        if (!app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists) return app;
        var options = new StaticFileOptions {
            OnPrepareResponse = static context => context.Context.Response.Headers.CacheControl =
                context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ? "no-cache" : "public,max-age=3600"
        };
        app.UseDefaultFiles();
        app.UseStaticFiles(options);
        // 已有具体端点优先；缺失的服务路径不能回退为前端 HTML。
        foreach (var prefix in new[] { "api", "health", "swagger", "hubs" }) {
            app.MapMethods($"/{prefix}/{{**path}}", ["GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS"], static () => Results.NotFound())
                .ExcludeFromDescription();
        }
        app.MapFallbackToFile("index.html", options).ExcludeFromDescription();
        return app;
    }
}
