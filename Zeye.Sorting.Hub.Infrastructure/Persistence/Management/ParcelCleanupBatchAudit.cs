namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

/// <summary>与删除同事务保存的精简批次凭据，用于准确计数和防止提交重试重复删除。</summary>
public sealed record ParcelCleanupBatchAudit {
    /// <summary>本批已提交的删除数量，不包含包裹身份或业务载荷。</summary>
    public int DeletedCount { get; init; }
    /// <summary>删除所在的物理分表后缀，空值表示基础表。</summary>
    public string PartitionSuffix { get; init; } = string.Empty;
    /// <summary>本批提交的本地时间。</summary>
    public DateTime CommittedAtLocal { get; init; }
}
