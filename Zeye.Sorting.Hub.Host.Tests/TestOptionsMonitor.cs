using Microsoft.Extensions.Options;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 测试用 IOptionsMonitor 实现，允许在测试中直接注入配置值并模拟热加载变更通知。
/// </summary>
/// <typeparam name="T">配置类型。</typeparam>
internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> {
    /// <summary>
    /// 当前配置值。
    /// </summary>
    private T _value;

    /// <summary>
    /// 已注册的变更监听器列表。
    /// </summary>
    private readonly List<Action<T, string?>> _listeners = new();

    /// <summary>
    /// 初始化 <see cref="TestOptionsMonitor{T}"/>。
    /// </summary>
    /// <param name="value">初始配置值。</param>
    public TestOptionsMonitor(T value) {
        _value = value;
    }

    /// <inheritdoc />
    public T CurrentValue => _value;

    /// <inheritdoc />
    public T Get(string? name) => _value;

    /// <inheritdoc />
    public IDisposable? OnChange(Action<T, string?> listener) {
        _listeners.Add(listener);
        return new ListenerRegistration(_listeners, listener);
    }

    /// <summary>
    /// 更新当前配置值，并触发所有已注册的 OnChange 监听器（模拟热加载变更通知）。
    /// </summary>
    /// <param name="value">新配置值。</param>
    public void Update(T value) {
        _value = value;
        foreach (var listener in _listeners) {
            listener(value, null);
        }
    }

    /// <summary>
    /// 可取消订阅的监听器注册句柄，Dispose 时从列表移除对应 listener。
    /// </summary>
    private sealed class ListenerRegistration : IDisposable {
        /// <summary>
        /// 监听器列表引用。
        /// </summary>
        private readonly List<Action<T, string?>> _list;
        /// <summary>
        /// 待移除的监听器。
        /// </summary>
        private readonly Action<T, string?> _listener;

        /// <summary>
        /// 初始化 <see cref="ListenerRegistration"/>。
        /// </summary>
        /// <param name="list">监听器列表。</param>
        /// <param name="listener">待移除的监听器。</param>
        public ListenerRegistration(List<Action<T, string?>> list, Action<T, string?> listener) {
            _list = list;
            _listener = listener;
        }

        /// <inheritdoc />
        public void Dispose() {
            _list.Remove(_listener);
        }
    }
}
