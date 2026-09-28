namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>全局包裹身份与物理分表定位索引。</summary>
public sealed class ParcelLocation {
    /// <summary>中心包裹编号，全局唯一。</summary>
    public long Id { get; set; }
    /// <summary>来源实例、编号会话和设备包裹号的稳定哈希，旧入口可为空。</summary>
    public string? SourceKey { get; set; }
    /// <summary>固定物理表后缀。</summary>
    public string Suffix { get; set; } = string.Empty;
    /// <summary>首次入库本地时间，补充记录不改变此值。</summary>
    public DateTime CreatedTime { get; set; }
}
