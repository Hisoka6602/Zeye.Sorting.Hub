namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 集包信息响应合同。
/// </summary>
public sealed record BagInfoResponse {
    /// <summary>
    /// 格口 Id。
    /// </summary>
    public required long ChuteId { get; init; }

    /// <summary>
    /// 格口名称。
    /// </summary>
    public required string ChuteName { get; init; }

    /// <summary>
    /// 集包号。
    /// </summary>
    public required string BagCode { get; init; }

    /// <summary>
    /// 当前集包中包裹数量。
    /// </summary>
    public required int ParcelCount { get; init; }

    /// <summary>
    /// 集包完成时间。
    /// </summary>
    public required DateTime? BaggingTime { get; init; }
}
