namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>
/// Web 请求审计日志中间件扩展。
/// </summary>
public static class WebRequestAuditLogMiddlewareExtensions {
    /// <summary>
    /// 注册 Web 请求审计日志中间件配置。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置根。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddWebRequestAuditLogging(
        this IServiceCollection services,
        IConfiguration configuration) {
        services.AddOptions<WebRequestAuditLogOptions>()
            .Bind(configuration.GetSection(WebRequestAuditLogOptions.SectionName))
            .Validate(static options => options.SampleRate >= 0D && options.SampleRate <= 1D, "SampleRate 必须在 0~1 之间")
            .Validate(static options => options.MaxRequestBodyLength >= 0, "MaxRequestBodyLength 不能小于 0")
            .Validate(static options => options.MaxResponseBodyLength >= 0, "MaxResponseBodyLength 不能小于 0")
            .Validate(static options => options.BackgroundQueueCapacity > 0, "BackgroundQueueCapacity 必须大于 0")
            .ValidateOnStart();
        var queueCapacity = configuration.GetValue<int?>($"{WebRequestAuditLogOptions.SectionName}:BackgroundQueueCapacity") ?? 1024;
        services.AddSingleton(new WebRequestAuditBackgroundQueue(queueCapacity));
        services.AddHostedService<WebRequestAuditBackgroundWorkerHostedService>();

        return services;
    }

    /// <summary>
    /// 启用 Web 请求审计日志中间件。
    /// </summary>
    /// <param name="app">应用构建器。</param>
    /// <returns>应用构建器。</returns>
    public static IApplicationBuilder UseWebRequestAuditLogging(this IApplicationBuilder app) {
        return app.UseMiddleware<WebRequestAuditLogMiddleware>();
    }
}
