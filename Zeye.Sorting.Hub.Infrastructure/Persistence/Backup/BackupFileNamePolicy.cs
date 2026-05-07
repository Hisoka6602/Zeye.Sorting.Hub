namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 备份文件名策略。
/// </summary>
internal static class BackupFileNamePolicy {
    /// <summary>
    /// 清洗路径片段。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <returns>安全片段。</returns>
    internal static string SanitizeSegment(string value) {
        var sanitized = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars()) {
            sanitized = sanitized.Replace(invalidCharacter, '-');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
