namespace Zeye.Sorting.Hub.Host.HostedServices;

/// <summary>
/// 分表治理守卫异常。
/// </summary>
internal sealed class ShardingGovernanceGuardException : InvalidOperationException {
    /// <summary>
    /// 初始化分表治理守卫异常。
    /// </summary>
    /// <param name="message">异常消息。</param>
    public ShardingGovernanceGuardException(string message)
        : base(message) {
    }
}
