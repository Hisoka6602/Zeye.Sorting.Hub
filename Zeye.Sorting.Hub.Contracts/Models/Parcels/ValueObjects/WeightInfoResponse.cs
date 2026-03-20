namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 称重明细响应合同。
/// </summary>
public sealed record WeightInfoResponse {
    /// <summary>
    /// 原始重量字符串。
    /// </summary>
    public required string RawWeight { get; init; }

    /// <summary>
    /// 取证依据。
    /// </summary>
    public required string EvidenceCode { get; init; }

    /// <summary>
    /// 格式化后重量（单位：kg）。
    /// </summary>
    public required decimal FormattedWeight { get; init; }

    /// <summary>
    /// 称重时间。
    /// </summary>
    public required DateTime WeighingTime { get; init; }

    /// <summary>
    /// 调整后的重量（单位：kg）。
    /// </summary>
    public required decimal? AdjustedWeight { get; init; }
}
