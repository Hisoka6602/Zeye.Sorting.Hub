namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

/// <summary>处理记录全局去重凭据，与处理记录及快照在同一事务中提交。</summary>
public sealed class ParcelProcessingReceipt {
    /// <summary>来源实例、会话与记录标识计算的稳定主键。</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>原始合同内容哈希，相同身份不同内容视为冲突。</summary>
    public string PayloadHash { get; set; } = string.Empty;
    /// <summary>包裹中心编号，未关联DWS为空。</summary>
    public long? ParcelId { get; set; }
    /// <summary>处理记录物理分表后缀。</summary>
    public string Suffix { get; set; } = string.Empty;
    /// <summary>实际入库本地时间。</summary>
    public DateTime RecordedAt { get; set; }
}
