namespace Zeye.Sorting.Hub.Host;

/// <summary>
/// Host 运行与 Swagger 暴露配置。
/// </summary>
public sealed class HostingOptions {
    /// <summary>
    /// 需要注入 Swagger 的 XML 注释程序集名称清单。
    /// </summary>
    public static readonly IReadOnlyList<string> XmlCommentAssemblyNames = [
        "Zeye.Sorting.Hub.Host",
        "Zeye.Sorting.Hub.Contracts",
        "Zeye.Sorting.Hub.Application",
        "Zeye.Sorting.Hub.Domain"
    ];

    /// <summary>
    /// 应用监听地址（分号分隔），例如 <c>http://0.0.0.0:5078;https://0.0.0.0:7078</c>。
    /// </summary>
    public string? Urls { get; init; }

    /// <summary>
    /// 是否启用 HTTPS 重定向。
    /// </summary>
    public bool EnableHttpsRedirection { get; init; } = false;

    /// <summary>
    /// Swagger 配置。
    /// </summary>
    public SwaggerOptions Swagger { get; init; } = new();

    /// <summary>
    /// Development 浏览器自动打开配置。
    /// </summary>
    public BrowserAutoOpenOptions BrowserAutoOpen { get; init; } = new();

    /// <summary>
    /// 解析监听地址列表。
    /// </summary>
    /// <returns>去重后的监听地址集合。</returns>
    public IReadOnlyList<string> GetUrlBindings() {
        if (string.IsNullOrWhiteSpace(Urls)) {
            return [];
        }

        return Urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// 获取 Swagger 路由前缀。
    /// </summary>
    /// <returns>清理后的路由前缀；为空时表示根路径。</returns>
    public string GetSwaggerRoutePrefix() {
        return (Swagger.RoutePrefix ?? string.Empty).Trim().Trim('/');
    }

    /// <summary>
    /// 获取 Swagger 文档名称。
    /// </summary>
    /// <returns>文档名称。</returns>
    public string GetSwaggerDocumentName() {
        return string.IsNullOrWhiteSpace(Swagger.DocumentName) ? "v1" : Swagger.DocumentName.Trim();
    }

    /// <summary>
    /// 获取 Swagger 文档标题。
    /// </summary>
    /// <returns>文档标题。</returns>
    public string GetSwaggerDocumentTitle() {
        return string.IsNullOrWhiteSpace(Swagger.DocumentTitle)
            ? "Zeye.Sorting.Hub API 文档"
            : Swagger.DocumentTitle.Trim();
    }

    /// <summary>
    /// 构建 Swagger JSON 路由模板。
    /// </summary>
    /// <returns>Swagger JSON 路由模板。</returns>
    public string BuildSwaggerJsonRouteTemplate() {
        return string.IsNullOrWhiteSpace(Swagger.JsonEndpoint)
            ? "swagger/{documentName}/swagger.json"
            : Swagger.JsonEndpoint.Trim().TrimStart('/');
    }

    /// <summary>
    /// 构建 Swagger UI 使用的 JSON 端点地址。
    /// </summary>
    /// <returns>Swagger JSON 端点地址。</returns>
    public string BuildSwaggerJsonEndpoint() {
        var documentName = GetSwaggerDocumentName();
        var endpoint = string.IsNullOrWhiteSpace(Swagger.JsonEndpoint)
            ? "/swagger/{documentName}/swagger.json"
            : Swagger.JsonEndpoint.Trim();
        if (!endpoint.StartsWith("/", StringComparison.Ordinal)) {
            endpoint = $"/{endpoint}";
        }

        return endpoint.Replace("{documentName}", documentName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 构建开发期浏览器自动打开地址。
    /// </summary>
    /// <returns>可访问的 Swagger 地址；若无法构建则返回 null。</returns>
    public string? BuildBrowserAutoOpenUrl() {
        if (!string.IsNullOrWhiteSpace(BrowserAutoOpen.Url)) {
            return BrowserAutoOpen.Url.Trim();
        }

        var firstBinding = GetUrlBindings().FirstOrDefault();
        if (string.IsNullOrWhiteSpace(firstBinding)
            || !Uri.TryCreate(firstBinding, UriKind.Absolute, out var uri)) {
            return null;
        }

        var host = uri.Host;
        if (host is "0.0.0.0" or "::" or "[::]" or "*" or "+") {
            host = "localhost";
        }

        var routePrefix = GetSwaggerRoutePrefix();
        var basePath = string.IsNullOrWhiteSpace(routePrefix)
            ? "/"
            : $"/{routePrefix}/";
        var builder = new UriBuilder(uri.Scheme, host, uri.Port) {
            Path = basePath
        };

        return builder.Uri.ToString();
    }
}

/// <summary>
/// Swagger 公开配置。
/// </summary>
public sealed class SwaggerOptions {
    /// <summary>
    /// 是否启用 Swagger。
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Swagger UI 路由前缀（例如 <c>swagger</c> 或 <c>docs/swagger</c>）。
    /// </summary>
    public string? RoutePrefix { get; init; } = "swagger";

    /// <summary>
    /// Swagger 文档页标题。
    /// </summary>
    public string? DocumentTitle { get; init; } = "Zeye.Sorting.Hub API 文档";

    /// <summary>
    /// Swagger 文档名称（例如 <c>v1</c>）。
    /// </summary>
    public string? DocumentName { get; init; } = "v1";

    /// <summary>
    /// Swagger JSON 路由模板（支持 <c>{documentName}</c> 占位）。
    /// </summary>
    public string? JsonEndpoint { get; init; } = "/swagger/{documentName}/swagger.json";
}

/// <summary>
/// 开发期浏览器自动打开配置。
/// </summary>
public sealed class BrowserAutoOpenOptions {
    /// <summary>
    /// 是否启用自动打开浏览器。
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// 自动打开地址；为空时会按监听地址与 Swagger 前缀自动拼装。
    /// </summary>
    public string? Url { get; init; }
}
