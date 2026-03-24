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
}
