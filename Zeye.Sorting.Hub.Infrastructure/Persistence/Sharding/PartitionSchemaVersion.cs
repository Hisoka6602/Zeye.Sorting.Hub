namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>物理分表已完成的 Code First 模型版本，每个周期在 DDL 成功后持久化。</summary>
public sealed class PartitionSchemaVersion {
    /// <summary>物理表组与周期的稳定键，最大 64 字符。</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>该分表对应的 EF Core 迁移版本，最大 150 字符。</summary>
    public string MigrationId { get; set; } = string.Empty;
    /// <summary>版本确认的本地时间。</summary>
    public DateTime UpdatedAt { get; set; }
}
