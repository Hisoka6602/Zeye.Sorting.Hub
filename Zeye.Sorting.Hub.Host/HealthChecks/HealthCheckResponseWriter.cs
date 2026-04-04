using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 健康检查 JSON 响应写出工具，输出结构化 JSON 包含所有检查项状态。
/// </summary>
internal static class HealthCheckResponseWriter {
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
    /// </summary>
    /// <param name="context">当前 HTTP 上下文。</param>
    /// <param name="report">健康检查报告。</param>
    /// <returns>异步任务。</returns>
    public static Task WriteJsonResponseAsync(HttpContext context, HealthReport report) {
        context.Response.ContentType = "application/json; charset=utf-8";

        var options = new JsonWriterOptions { Indented = false };
        using var memoryStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(memoryStream, options)) {
            writer.WriteStartObject();
            writer.WriteString("status", StatusText.GetValueOrDefault(report.Status, "Unknown"));
            writer.WriteString("generatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
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
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return context.Response.WriteAsync(Encoding.UTF8.GetString(memoryStream.ToArray()));
    }
}
