namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 条码明细响应合同。
/// </summary>
public sealed record BarCodeInfoResponse {
    /// <summary>
    /// 条码内容。
    /// </summary>
    public required string BarCode { get; init; }

    /// <summary>
    /// 条码类型（枚举数值）；来源未上报时为空。
    /// </summary>
    public required int? BarCodeType { get; init; }

    /// <summary>
    /// 采集时间。
    /// </summary>
    public required DateTime? CapturedTime { get; init; }

    /// <summary>来源处理记录标识；仅有主表摘要时为空。</summary>
    public string? RecordId { get; init; }
}
