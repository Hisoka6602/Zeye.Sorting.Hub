using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 健康检查 JSON 响应写出工具，输出结构化 JSON 包含所有检查项状态。
/// </summary>
internal static class HealthCheckResponseWriter {
    /// <summary>
    /// 健康检查统一本地时间格式。
    /// </summary>
    internal const string LocalDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// 健康状态文本映射（避免反射 Enum.GetName 开销）。
    /// </summary>
    private static readonly IReadOnlyDictionary<HealthStatus, string> StatusText = new Dictionary<HealthStatus, string> {
        [HealthStatus.Healthy] = "Healthy",
        [HealthStatus.Degraded] = "Degraded",
        [HealthStatus.Unhealthy] = "Unhealthy"
    };

    /// <summary>
    /// 将健康检查结果序列化为 JSON 并写入 HttpResponse。
    /// 直接写入 <see cref="HttpResponse.BodyWriter"/>（PipeWriter），
    /// 避免 MemoryStream 中间分配与字节→字符串双重编码开销。
    /// </summary>
    /// <param name="context">当前 HTTP 上下文。</param>
    /// <param name="report">健康检查报告。</param>
    /// <returns>异步任务。</returns>
    public static async Task WriteJsonResponseAsync(HttpContext context, HealthReport report) {
        context.Response.ContentType = "application/json; charset=utf-8";

        var options = new JsonWriterOptions { Indented = false };
        // 直接写入 BodyWriter，无中间缓冲区与字符串分配
        await using var writer = new Utf8JsonWriter(context.Response.BodyWriter, options);
        writer.WriteStartObject();
        writer.WriteString("status", StatusText.GetValueOrDefault(report.Status, "Unknown"));
        writer.WriteString("generatedAt", DateTime.Now.ToString(LocalDateTimeFormat)); // 本地时间语义
        writer.WriteStartObject("entries");
        foreach (var (key, value) in report.Entries) {
            writer.WriteStartObject(key);
            writer.WriteString("status", StatusText.GetValueOrDefault(value.Status, "Unknown"));
            if (value.Description is not null) {
                writer.WriteString("description", value.Description);
            }
            if (value.Duration != TimeSpan.Zero) {
                writer.WriteNumber("durationMs", (long)value.Duration.TotalMilliseconds);
            }
            if (value.Data.Count > 0) {
                writer.WriteStartObject("data");
                foreach (var (dataKey, dataValue) in value.Data) {
                    WriteHealthData(writer, dataKey, dataValue);
                }

                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>
    /// 写入健康检查附加数据，优先按常见标量类型输出，其他类型退回 JsonSerializer。
    /// </summary>
    /// <param name="writer">JSON 写入器。</param>
    /// <param name="key">数据键。</param>
    /// <param name="value">数据值。</param>
    private static void WriteHealthData(Utf8JsonWriter writer, string key, object? value) {
        switch (value) {
            case null:
                writer.WriteNull(key);
                return;
            case string text:
                writer.WriteString(key, text);
                return;
            case bool boolValue:
                writer.WriteBoolean(key, boolValue);
                return;
            case int intValue:
                writer.WriteNumber(key, intValue);
                return;
            case long longValue:
                writer.WriteNumber(key, longValue);
                return;
            case double doubleValue:
                writer.WriteNumber(key, doubleValue);
                return;
            case float floatValue:
                writer.WriteNumber(key, floatValue);
                return;
            case decimal decimalValue:
                writer.WriteNumber(key, decimalValue);
                return;
            default:
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, value, value.GetType());
                return;
        }
    }
}
