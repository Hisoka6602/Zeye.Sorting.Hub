namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 灰检信息响应合同。
/// </summary>
public sealed record GrayDetectorInfoResponse {
    /// <summary>
    /// 小车编号。
    /// </summary>
    public required string CarrierNumber { get; init; }

    /// <summary>
    /// 附加框信息。
    /// </summary>
    public required string AttachBoxInfo { get; init; }

    /// <summary>
    /// 主框信息。
    /// </summary>
    public required string MainBoxInfo { get; init; }

    /// <summary>
    /// 联动小车数量。
    /// </summary>
    public required int LinkedCarrierCount { get; init; }

    /// <summary>
    /// 包裹中心点坐标。
    /// </summary>
    public required string? CenterPosition { get; init; }

    /// <summary>
    /// 检测结果返回时间。
    /// </summary>
    public required DateTime ResultTime { get; init; }

    /// <summary>
    /// 原始返回数据内容。
    /// </summary>
    public required string RawResult { get; init; }
}
