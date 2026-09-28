namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;

/// <summary>已保存的完整处理事实及中心定位结果。</summary>
public sealed record ParcelProcessingRecordResponse : ParcelProcessingRecordRequest {
    /// <summary>中心包裹编号，未关联时为空；使用字符串避免浏览器整数精度损失。</summary>
    public string? ParcelId { get; init; }
    /// <summary>实际入库本地时间。</summary>
    public DateTime RecordedAt { get; init; }
    /// <summary>首次入库分表依据，补充记录不改变此值。</summary>
    public DateTime PartitionTime { get; init; }
}

