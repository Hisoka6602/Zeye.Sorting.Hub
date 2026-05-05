namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Diagnostics;

/// <summary>
/// 数据库连接诊断配置。
/// </summary>
public sealed class DatabaseConnectionDiagnosticsOptions {
    /// <summary>
    /// 配置节路径。
    /// </summary>
    public const string SectionPath = "Persistence:Diagnostics";

    /// <summary>
    /// 是否启用启动期连接预热。
    /// 可填写范围：true / false。
    /// </summary>
    public bool IsWarmupEnabled { get; set; } = true;

    /// <summary>
    /// 启动期预热连接数。
    /// 可填写范围：1~64。
    /// </summary>
    public int WarmupConnectionCount { get; set; } = 4;

    /// <summary>
    /// 单次探测超时时间（毫秒）。
    /// 可填写范围：100~60000。
    /// </summary>
    public int ProbeTimeoutMilliseconds { get; set; } = 3000;

    /// <summary>
    /// 进入 Unhealthy 所需的连续失败次数。
    /// 可填写范围：1~20。
    /// </summary>
    public int FailureThreshold { get; set; } = 3;

    /// <summary>
    /// 从恢复中回到 Healthy 所需的连续成功次数。
    /// 可填写范围：1~20。
    /// </summary>
    public int RecoveryThreshold { get; set; } = 2;
}
