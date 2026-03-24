using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace Zeye.Sorting.Hub.Host.Utilities;

/// <summary>
/// 本地时间字符串解析工具与 API 层统一错误响应工厂（供所有 API 路由层共用）。
/// 全项目禁止 UTC 语义，所有时间字符串均按本地时间解析。
/// </summary>
internal static class LocalDateTimeParsing {
    /// <summary>
    /// 纯日期格式长度（yyyy-MM-dd）。
    /// </summary>
    private const int DateOnlyFormatLength = 10;

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
            || input.LastIndexOf('-') > DateOnlyFormatLength - 1) {
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
    /// 创建统一的 400 ProblemDetails 响应（供所有 API 路由扩展共用，避免重复实现）。
    /// </summary>
    /// <param name="title">问题标题。</param>
    /// <param name="detail">问题详情。</param>
    /// <returns>统一 400 错误响应。</returns>
    internal static IResult CreateBadRequestProblem(string title, string detail) {
        return Results.Json(
            new ProblemDetails {
                Title = title,
                Detail = detail,
                Status = StatusCodes.Status400BadRequest
            },
            contentType: "application/problem+json",
            statusCode: StatusCodes.Status400BadRequest);
    }

    /// <summary>
    /// 创建统一的“包裹不存在”404 问题详情响应（供所有 API 路由扩展共用）。
    /// </summary>
    /// <param name="id">未找到的包裹主键。</param>
    /// <returns>统一 404 响应。</returns>
    internal static IResult CreateParcelMissingProblem(long id) {
        return Results.Problem(
            title: "包裹不存在",
            detail: $"未找到 Id 为 {id} 的包裹。",
            statusCode: StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// 创建统一的 404 ProblemDetails 响应（供所有 API 路由扩展共用）。
    /// </summary>
    /// <param name="title">问题标题。</param>
    /// <param name="detail">问题详情。</param>
    /// <returns>统一 404 错误响应。</returns>
    internal static IResult CreateNotFoundProblem(string title, string detail) {
        return Results.Problem(
            title: title,
            detail: detail,
            statusCode: StatusCodes.Status404NotFound);
    }
}
