using System.Data.Common;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份连接字符串解析工具。
/// </summary>
internal static class BackupConnectionStringParser {
    /// <summary>
    /// 解析连接字符串为键值字典。
    /// </summary>
    /// <param name="connectionString">连接字符串。</param>
    /// <returns>键值字典。</returns>
    public static IReadOnlyDictionary<string, string> Parse(string connectionString) {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new DbConnectionStringBuilder {
            ConnectionString = connectionString
        };
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in builder.Keys) {
            values[key] = builder[key]?.ToString() ?? string.Empty;
        }

        return values;
    }

    /// <summary>
    /// 读取必填值。
    /// </summary>
    /// <param name="values">连接字符串字典。</param>
    /// <param name="keys">候选键。</param>
    /// <returns>命中值。</returns>
    public static string GetRequiredValue(IReadOnlyDictionary<string, string> values, params string[] keys) {
        foreach (var key in keys) {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) {
                return value.Trim();
            }
        }

        throw new InvalidOperationException($"连接字符串缺少必要字段：{string.Join(" / ", keys)}。");
    }

    /// <summary>
    /// 读取可选值。
    /// </summary>
    /// <param name="values">连接字符串字典。</param>
    /// <param name="defaultValue">默认值。</param>
    /// <param name="keys">候选键。</param>
    /// <returns>命中值或默认值。</returns>
    public static string GetOptionalValue(IReadOnlyDictionary<string, string> values, string defaultValue, params string[] keys) {
        foreach (var key in keys) {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) {
                return value.Trim();
            }
        }

        return defaultValue;
    }
}
