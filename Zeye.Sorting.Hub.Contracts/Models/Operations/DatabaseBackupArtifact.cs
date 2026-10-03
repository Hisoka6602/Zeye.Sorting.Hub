namespace Zeye.Sorting.Hub.Contracts.Models.Operations;
/// <summary>数据库结构及数据备份的持久化清单，不含连接凭据。</summary>
public sealed record DatabaseBackupArtifact {
    /// <summary>服务端生成的不可猜测文件编号。</summary>
    public string Id { get; init; } = string.Empty;
    /// <summary>备份来源数据库。</summary>
    public string Database { get; init; } = string.Empty;
    /// <summary>备份完成本地时间。</summary>
    public DateTime CreatedAtLocal { get; init; }
    /// <summary>执行账号名称。</summary>
    public string RequestedBy { get; init; } = string.Empty;
    /// <summary>压缩文件字节数。</summary>
    public long SizeBytes { get; init; }
    /// <summary>完整文件的 SHA256 校验值。</summary>
    public string Sha256 { get; init; } = string.Empty;
    /// <summary>每个基础及物理分表的快照行数。</summary>
    public Dictionary<string, long> TableRows { get; init; } = [];
    /// <summary>最近恢复演练的隔离数据库。</summary>
    public string? RestoredDatabase { get; init; }
    /// <summary>逐表行数核对通过的本地时间。</summary>
    public DateTime? VerifiedAtLocal { get; init; }
}
