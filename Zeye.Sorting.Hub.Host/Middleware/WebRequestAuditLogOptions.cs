namespace Zeye.Sorting.Hub.Host.Middleware;

/// <summary>
/// Web 请求审计日志中间件配置。
/// </summary>
public sealed class WebRequestAuditLogOptions {
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "WebRequestAuditLog";

    /// <summary>
    /// 是否启用中间件。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 采样率（0~1）。
    /// </summary>
    public double SampleRate { get; set; } = 1D;

    /// <summary>
    /// 是否采集请求体。
    /// </summary>
    public bool IncludeRequestBody { get; set; }

    /// <summary>
    /// 是否采集响应体。
    /// </summary>
    public bool IncludeResponseBody { get; set; }

    /// <summary>
    /// 请求体最大采集长度（字符）。
    /// </summary>
    public int MaxRequestBodyLength { get; set; } = 4096;

    /// <summary>
    /// 响应体最大采集长度（字符）。
    /// </summary>
    public int MaxResponseBodyLength { get; set; } = 4096;
}
