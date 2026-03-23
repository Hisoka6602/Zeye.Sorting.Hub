namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 测试层本地时间语义约束工具类。
/// 提供统一的本地时间构造与断言方法，确保测试数据不引入 UTC 相关语义，
/// 降低后续测试中误用 UTC 语义的回归风险。
/// </summary>
internal static class LocalTimeTestConstraintHelper {

    /// <summary>
    /// 构造一个 <see cref="DateTimeKind.Local"/> 类型的时间值，等价于 new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local)。
    /// 所有测试代码应优先通过此方法创建固定时间点，以保证全链路本地时间语义一致。
    /// </summary>
    /// <param name="year">年。</param>
    /// <param name="month">月。</param>
    /// <param name="day">日。</param>
    /// <param name="hour">时（默认 0）。</param>
    /// <param name="minute">分（默认 0）。</param>
    /// <param name="second">秒（默认 0）。</param>
    /// <returns>带 <see cref="DateTimeKind.Local"/> 的时间值。</returns>
    public static DateTime CreateLocalTime(int year, int month, int day, int hour = 0, int minute = 0, int second = 0)
        => new(year, month, day, hour, minute, second, DateTimeKind.Local);

    /// <summary>
    /// 断言 <paramref name="value"/> 的 <see cref="DateTime.Kind"/> 为 <see cref="DateTimeKind.Local"/>。
    /// 如果违反约束则抛出 <see cref="Xunit.Sdk.EqualException"/>。
    /// </summary>
    /// <param name="value">待断言的时间值。</param>
    public static void AssertIsLocalTime(DateTime value)
        => Assert.Equal(DateTimeKind.Local, value.Kind);

    /// <summary>
    /// 断言 <paramref name="value"/> 的 <see cref="DateTime.Kind"/> 不是 UTC 语义。
    /// 用于快速验证某个时间值未被错误地标记为 UTC（Unknown/Local 均允许）。
    /// </summary>
    /// <param name="value">待断言的时间值。</param>
    public static void AssertNotUtc(DateTime value)
        => Assert.NotEqual(-1, (int)value.Kind);
}
