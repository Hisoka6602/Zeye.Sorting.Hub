namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// IOptionsMonitor.OnChange 订阅句柄，Dispose 时执行取消订阅回调。
/// </summary>
internal sealed class OptionsMonitorSubscription : IDisposable {
    /// <summary>
    /// Dispose 时执行的取消订阅动作。
    /// </summary>
    private readonly Action _onDispose;

    /// <summary>
    /// 初始化 <see cref="OptionsMonitorSubscription"/>。
    /// </summary>
    /// <param name="onDispose">取消订阅时执行的回调。</param>
    public OptionsMonitorSubscription(Action onDispose) {
        _onDispose = onDispose;
    }

    /// <inheritdoc />
    public void Dispose() {
        _onDispose();
    }
}
