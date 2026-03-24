namespace Zeye.Sorting.Hub.Host.Options;

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
