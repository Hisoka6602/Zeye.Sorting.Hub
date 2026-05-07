namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>
/// 恢复演练规划器。
/// </summary>
public sealed class RestoreDrillPlanner {
    /// <summary>
    /// 查找最近一次演练记录。
    /// </summary>
    /// <param name="drillDirectoryPath">演练目录。</param>
    /// <returns>最近一次演练记录路径。</returns>
    public string? FindLatestDrillRecordPath(string drillDirectoryPath) {
        if (string.IsNullOrWhiteSpace(drillDirectoryPath) || !Directory.Exists(drillDirectoryPath)) {
            return null;
        }

        return Directory.EnumerateFiles(drillDirectoryPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(static file => file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .Select(file => new FileInfo(file))
            .OrderByDescending(static file => file.LastWriteTime)
            .Select(static file => file.FullName)
            .FirstOrDefault();
    }

    /// <summary>
    /// 构建恢复演练 Runbook。
    /// </summary>
    /// <param name="latestDrillRecordPath">最近演练记录路径。</param>
    /// <param name="providerRunbook">Provider 恢复 Runbook。</param>
    /// <returns>恢复演练 Runbook。</returns>
    public string BuildRestoreRunbook(string? latestDrillRecordPath, string providerRunbook) {
        if (string.IsNullOrWhiteSpace(latestDrillRecordPath)) {
            return $"当前未发现恢复演练记录，请先在 drill-records 中补充季度/年度演练记录。\n{providerRunbook}";
        }

        return $"最近演练记录：{latestDrillRecordPath}\n{providerRunbook}";
    }
}
