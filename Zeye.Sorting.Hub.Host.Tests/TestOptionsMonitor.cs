using Microsoft.Extensions.Options;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 测试用 IOptionsMonitor 简单实现，允许在测试中直接注入配置值。
/// </summary>
/// <typeparam name="T">配置类型。</typeparam>
internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> {
    /// <summary>
    /// 当前配置值。
    /// </summary>
    private T _value;

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
    public IDisposable? OnChange(Action<T, string?> listener) => null;

    /// <summary>
    /// 更新当前配置值（仅用于测试目的）。
    /// </summary>
    /// <param name="value">新配置值。</param>
    public void Update(T value) {
        _value = value;
    }
}
