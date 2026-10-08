namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>模拟提供器错误码而不连接外部数据库，用于异常包装分类测试。</summary>
internal sealed class SlowQueryNumberedException(int number) : Exception("模拟数据库错误") {
    /// <summary>提供器的原始错误码。</summary>
    public int Number { get; } = number;
}
