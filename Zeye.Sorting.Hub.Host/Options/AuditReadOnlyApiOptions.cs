namespace Zeye.Sorting.Hub.Host.Options;

/// <summary>
/// 审计日志只读 API 开关配置。
/// </summary>
public sealed class AuditReadOnlyApiOptions {

    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "AuditReadOnlyApi";

    /// <summary>
    /// 是否启用审计日志只读 API。
    /// </summary>
    public bool Enabled { get; init; } = false;

    /// <summary>
    /// 是否要求审计日志只读 API 执行授权校验。
    /// </summary>
    /// <remarks>
    /// 默认 false：便于在未接入真实认证方案时联调查询链路；
    /// 生产环境建议显式开启并接入真实认证方案。
    /// </remarks>
    public bool RequireAuthorization { get; init; } = false;
}
