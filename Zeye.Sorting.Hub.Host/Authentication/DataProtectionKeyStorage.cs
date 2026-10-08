using NLog;

namespace Zeye.Sorting.Hub.Host.Authentication;

/// <summary>配置凭据和浏览器会话共用的持久化密钥目录，随配置卷保留。</summary>
public static class DataProtectionKeyStorage {
    /// <summary>密钥迁移失败只记录文件路径和异常，不记录文件内容。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>启动时将旧日志目录内的密钥复制到配置目录，保留原文件并容许并发升级。</summary>
    public static DirectoryInfo Prepare(string contentRoot, string? configurationDatabasePath = null) {
        var database = Path.GetFullPath(configurationDatabasePath ?? "data/configuration/settings.db", contentRoot);
        var destination = Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(database)!, "data-protection"));
        var legacy = Path.Combine(contentRoot, "logs", "data-protection");
        if (!Directory.Exists(legacy)) return destination;
        try {
            foreach (var source in Directory.EnumerateFiles(legacy, "*.xml", SearchOption.TopDirectoryOnly)) {
                var target = Path.Combine(destination.FullName, Path.GetFileName(source));
                if (File.Exists(target)) continue;
                try { File.Copy(source, target, overwrite: false); }
                catch (IOException exception) when (File.Exists(target)) { Logger.Debug(exception, "会话密钥已由并发启动迁移，File={File}", target); }
            }
            return destination;
        }
        catch (Exception exception) { Logger.Error(exception, "持久化配置密钥迁移失败，Directory={Directory}", destination.FullName); throw; }
    }
}
