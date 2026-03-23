namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DesignTime;

/// <summary>
/// 无操作日志作用域。
/// </summary>
internal sealed class NoopScope : IDisposable {
    /// <summary>
    /// 单例实例。
    /// </summary>
    public static readonly NoopScope Instance = new();

    /// <inheritdoc />
    public void Dispose() { }
}
