namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;

/// <summary>原子追加处理记录的结果合同。</summary>
public sealed record ParcelProcessingWriteResponse {
    /// <summary>中心包裹编号，未绑定DWS为空。</summary>
    public string? ParcelId { get; init; }
    /// <summary>是否命中已保存的同一条记录。</summary>
    public bool IsDuplicate { get; init; }
    /// <summary>实际物理分表后缀。</summary>
    public required string PartitionSuffix { get; init; }
}
