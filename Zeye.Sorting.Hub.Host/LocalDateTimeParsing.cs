using System.Globalization;

/// <summary>
/// 本地时间字符串解析工具（供 API 路由层共用，保证所有端点统一使用同一套本地时间验证规则）。
/// 全项目禁止 UTC 语义，所有时间字符串均按本地时间解析。
/// </summary>
internal static class LocalDateTimeParsing {
    /// <summary>
    /// 允许的本地时间格式列表（禁止 Z/offset 表达）。
    /// </summary>
    private static readonly string[] LocalDateTimeFormats = [
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.fff"
    ];

    /// <summary>
    /// 尝试按本地时间语义解析时间字符串；空字符串、UTC 时间、含 offset 的时间均返回 false。
    /// </summary>
    /// <param name="input">输入时间字符串。</param>
    /// <param name="parsedTime">解析结果（失败时为 default）。</param>
    /// <returns>是否解析成功。</returns>
    internal static bool TryParseLocalDateTime(string? input, out DateTime parsedTime) {
        if (string.IsNullOrWhiteSpace(input)
            || input.Contains('Z', StringComparison.OrdinalIgnoreCase)
            || input.Contains('+', StringComparison.Ordinal)
            || input.LastIndexOf('-') > "yyyy-MM-dd".Length - 1) {
            parsedTime = default;
            return false;
        }

        return DateTime.TryParseExact(
            input,
            LocalDateTimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal | DateTimeStyles.AllowWhiteSpaces,
            out parsedTime);
    }

    /// <summary>
    /// 尝试解析可空本地时间字符串（空值视为合法，解析为 null）。
    /// </summary>
    /// <param name="input">可空输入字符串。</param>
    /// <param name="parsedTime">解析结果（空输入时为 null，失败时为 null）。</param>
    /// <returns>是否解析成功（空输入也视为成功）。</returns>
    internal static bool TryParseOptionalLocalDateTime(string? input, out DateTime? parsedTime) {
        if (string.IsNullOrWhiteSpace(input)) {
            parsedTime = null;
            return true;
        }

        if (!TryParseLocalDateTime(input, out var localTime)) {
            parsedTime = null;
            return false;
        }

        parsedTime = localTime;
        return true;
    }

    /// <summary>
    /// 判断 DateTime 是否包含 UTC Kind（用于验证 JSON body 反序列化的时间字段）。
    /// </summary>
    /// <param name="dateTime">目标 DateTime。</param>
    /// <returns>是否为 UTC 时间。</returns>
    internal static bool IsUtcKind(DateTime dateTime) {
        return dateTime.Kind == DateTimeKind.Utc;
    }
}
