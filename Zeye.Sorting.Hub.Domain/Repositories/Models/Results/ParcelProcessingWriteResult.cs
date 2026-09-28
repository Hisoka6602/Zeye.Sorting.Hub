namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

/// <summary>处理记录原子写入结果。</summary>
public sealed record ParcelProcessingWriteResult {
    /// <summary>中心包裹编号，未关联DWS为空。</summary>
    public long? ParcelId { get; init; }
    /// <summary>记录是否已经入库，本次未重复追加。</summary>
    public bool IsDuplicate { get; init; }
    /// <summary>已固定的物理分表后缀。</summary>
    public required string PartitionSuffix { get; init; }
}
