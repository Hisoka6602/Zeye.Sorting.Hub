using System.Data.Common;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份连接字符串值读取工具。
/// </summary>
internal static class BackupConnectionStringValueReader {
    /// <summary>
    /// 尝试读取首个非空键值。
    /// </summary>
    /// <param name="builder">连接字符串构建器。</param>
    /// <param name="value">解析值。</param>
    /// <param name="keys">键名集合。</param>
    /// <returns>是否读取成功。</returns>
    internal static bool TryGetFirstNonEmptyValue(DbConnectionStringBuilder builder, out string value, params string[] keys) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(keys);

        foreach (var key in keys) {
            if (builder.TryGetValue(key, out var rawValue) && rawValue is string stringValue && !string.IsNullOrWhiteSpace(stringValue)) {
                value = stringValue.Trim();
                return true;
            }
        }

        value = string.Empty;
        return false;
    }
}
