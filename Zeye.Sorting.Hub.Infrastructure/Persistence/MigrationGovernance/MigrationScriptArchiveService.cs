using Microsoft.Extensions.Hosting;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

/// <summary>
/// 迁移脚本归档服务。
/// </summary>
public sealed class MigrationScriptArchiveService {
    /// <summary>
    /// 内容根环境信息。
    /// </summary>
    private readonly IHostEnvironment _hostEnvironment;

    /// <summary>
    /// 初始化迁移脚本归档服务。
    /// </summary>
    /// <param name="hostEnvironment">宿主环境信息。</param>
    public MigrationScriptArchiveService(IHostEnvironment hostEnvironment) {
        _hostEnvironment = hostEnvironment;
    }

    /// <summary>
    /// 归档正向迁移脚本。
    /// </summary>
    /// <param name="archiveDirectory">归档目录。</param>
    /// <param name="providerName">数据库提供器名称。</param>
    /// <param name="migrationName">迁移名称。</param>
    /// <param name="content">脚本内容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>归档文件路径。</returns>
    public Task<string> ArchiveForwardScriptAsync(
        string archiveDirectory,
        string providerName,
        string migrationName,
        string content,
        CancellationToken cancellationToken) {
        return ArchiveScriptAsync(
            archiveDirectory,
            providerName,
            migrationName,
            "forward",
            ".sql",
            content,
            cancellationToken);
    }

    /// <summary>
    /// 归档回滚参考脚本。
    /// </summary>
    /// <param name="archiveDirectory">归档目录。</param>
    /// <param name="providerName">数据库提供器名称。</param>
    /// <param name="migrationName">迁移名称。</param>
    /// <param name="content">脚本内容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>归档文件路径。</returns>
    public Task<string> ArchiveRollbackScriptAsync(
        string archiveDirectory,
        string providerName,
        string migrationName,
        string content,
        CancellationToken cancellationToken) {
        return ArchiveScriptAsync(
            archiveDirectory,
            providerName,
            migrationName,
            "rollback-reference",
            ".sql",
            content,
            cancellationToken);
    }

    /// <summary>
    /// 归档脚本文件。
    /// </summary>
    /// <param name="archiveDirectory">归档目录。</param>
    /// <param name="providerName">数据库提供器名称。</param>
    /// <param name="migrationName">迁移名称。</param>
    /// <param name="artifactName">资产类型。</param>
    /// <param name="extension">文件扩展名。</param>
    /// <param name="content">脚本内容。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>归档文件路径。</returns>
    private async Task<string> ArchiveScriptAsync(
        string archiveDirectory,
        string providerName,
        string migrationName,
        string artifactName,
        string extension,
        string content,
        CancellationToken cancellationToken) {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationName);

        var rootDirectory = ResolveArchiveRootPath(archiveDirectory);
        var providerDirectory = Path.Combine(rootDirectory, SanitizePathSegment(providerName));
        Directory.CreateDirectory(providerDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfffffff");
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var fileName = $"{timestamp}-{uniqueSuffix}-{SanitizePathSegment(migrationName)}-{artifactName}{extension}";
        var filePath = Path.Combine(providerDirectory, fileName);
        await File.WriteAllTextAsync(filePath, content, cancellationToken);
        return filePath;
    }

    /// <summary>
    /// 解析归档根目录。
    /// </summary>
    /// <param name="archiveDirectory">配置目录。</param>
    /// <returns>绝对路径。</returns>
    internal string ResolveArchiveRootPath(string archiveDirectory) {
        if (Path.IsPathRooted(archiveDirectory)) {
            return archiveDirectory;
        }

        return Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, archiveDirectory));
    }

    /// <summary>
    /// 清洗路径片段中的非法字符。
    /// </summary>
    /// <param name="value">原始文本。</param>
    /// <returns>安全路径片段。</returns>
    private static string SanitizePathSegment(string value) {
        var sanitized = value.Trim();
        foreach (var invalidCharacter in Path.GetInvalidFileNameChars()) {
            sanitized = sanitized.Replace(invalidCharacter, '-');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
