using Zeye.Sorting.Hub.Contracts.Models.Operations;

namespace Zeye.Sorting.Hub.Application.Abstractions.Storage;

/// <summary>完整备份、受保护下载、隔离恢复及备份目录维护的应用协作契约。</summary>
public interface IDatabaseBackupArtifactService {
    /// <summary>当前数据库提供程序是否支持事务快照备份。</summary>
    bool IsSupported { get; }

    /// <summary>读取已完整发布的备份清单。</summary>
    Task<IReadOnlyList<DatabaseBackupArtifact>> ListAsync(CancellationToken cancellationToken);

    /// <summary>创建完整数据库事务快照，提交完整清单后才返回成功。</summary>
    Task<DatabaseBackupArtifact> CreateAsync(string actor, CancellationToken cancellationToken);

    /// <summary>仅向服务端生成的新数据库恢复并逐表核验。</summary>
    Task<DatabaseBackupArtifact> RestoreIsolatedAsync(string id, CancellationToken cancellationToken);

    /// <summary>验证长度及摘要后获取指定完整备份的下载路径。</summary>
    Task<string> DownloadPathAsync(string id, CancellationToken cancellationToken);

    /// <summary>按保留窗口和容量上限维护本服务生成的备份，保留最低安全份数。</summary>
    Task MaintainAsync(CancellationToken cancellationToken);
}
