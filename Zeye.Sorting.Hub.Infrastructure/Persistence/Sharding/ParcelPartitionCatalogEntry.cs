namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>已创建分表目录，历史表不受当前配置粒度变更影响。</summary>
public sealed class ParcelPartitionCatalogEntry {
    /// <summary>经过验证的物理表后缀。</summary>
    public string Suffix { get; set; } = string.Empty;
    /// <summary>周期开始本地时间。</summary>
    public DateTime Start { get; set; }
    /// <summary>周期结束本地时间。</summary>
    public DateTime End { get; set; }
    /// <summary>创建完成时间，用于运行审计。</summary>
    public DateTime CreatedTime { get; set; }
}
