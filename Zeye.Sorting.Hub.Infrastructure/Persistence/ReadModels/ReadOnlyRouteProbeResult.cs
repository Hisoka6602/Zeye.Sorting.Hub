namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>
/// 只读数据库路由探测结果。
/// </summary>
public readonly record struct ReadOnlyRouteProbeResult {
    /// <summary>
    /// 初始化只读数据库路由探测结果。
    /// </summary>
    /// <param name="isEnabled">是否启用只读数据库。</param>
    /// <param name="isReadOnlyConfigured">是否已配置只读副本连接字符串。</param>
    /// <param name="isReadOnlyAvailable">只读副本是否可用。</param>
    /// <param name="isFallbackToPrimary">是否已回退主库。</param>
    /// <param name="routeTarget">当前路由目标。</param>
    /// <param name="summary">摘要信息。</param>
    public ReadOnlyRouteProbeResult(
        bool isEnabled,
        bool isReadOnlyConfigured,
        bool isReadOnlyAvailable,
        bool isFallbackToPrimary,
        string routeTarget,
        string summary) {
        IsEnabled = isEnabled;
        IsReadOnlyConfigured = isReadOnlyConfigured;
        IsReadOnlyAvailable = isReadOnlyAvailable;
        IsFallbackToPrimary = isFallbackToPrimary;
        RouteTarget = routeTarget;
        Summary = summary;
    }

    /// <summary>
    /// 是否启用只读数据库。
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// 是否已配置只读副本连接字符串。
    /// </summary>
    public bool IsReadOnlyConfigured { get; init; }

    /// <summary>
    /// 只读副本是否可用。
    /// </summary>
    public bool IsReadOnlyAvailable { get; init; }

    /// <summary>
    /// 是否已回退主库。
    /// </summary>
    public bool IsFallbackToPrimary { get; init; }

    /// <summary>
    /// 当前路由目标。
    /// </summary>
    public string RouteTarget { get; init; }

    /// <summary>
    /// 探测摘要。
    /// </summary>
    public string Summary { get; init; }
}
