namespace Zeye.Sorting.Hub.Host.Options;

/// <summary>
/// 运行时资源阈值配置，用于启动期审计与运行期降级决策。
/// 所有阈值均为"告警级别"边界，触发后优先限流与降级而非崩溃。
/// </summary>
public sealed class ResourceThresholdsOptions {
    /// <summary>
    /// 配置节名称。
    /// </summary>
    public const string SectionName = "ResourceThresholds";

    /// <summary>
    /// 数据库连接池最大连接数硬上限（超过时触发告警）。
    /// 可填写范围：正整数，建议值 100，生产推荐 ≤ 200。
    /// </summary>
    public int MaxConnectionPoolSize { get; init; } = 100;

    /// <summary>
    /// 进程内存告警阈值（MB）。超过该值触发告警，用于提示容量扩容或内存泄漏排查。
    /// 可填写范围：正整数（MB），建议值 1024（1 GB），0 表示不启用内存阈值告警。
    /// </summary>
    public int MemoryWarningThresholdMB { get; init; } = 1024;
}
