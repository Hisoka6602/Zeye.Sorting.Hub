namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 体积信息响应合同。
/// </summary>
public sealed record VolumeInfoResponse {
    /// <summary>
    /// 体积来源类型（枚举数值）。
    /// </summary>
    public required int SourceType { get; init; }

    /// <summary>
    /// 原始体积字符串。
    /// </summary>
    public required string RawVolume { get; init; }

    /// <summary>
    /// 取证依据。
    /// </summary>
    public required string EvidenceCode { get; init; }

    /// <summary>
    /// 格式化后的长度（单位：毫米）。
    /// </summary>
    public required decimal FormattedLength { get; init; }

    /// <summary>
    /// 格式化后的宽度（单位：毫米）。
    /// </summary>
    public required decimal FormattedWidth { get; init; }

    /// <summary>
    /// 格式化后的高度（单位：毫米）。
    /// </summary>
    public required decimal FormattedHeight { get; init; }

    /// <summary>
    /// 格式化后的体积（单位：立方厘米）。
    /// </summary>
    public required decimal FormattedVolume { get; init; }

    /// <summary>
    /// 长度调整值（单位：毫米）。
    /// </summary>
    public required decimal? AdjustedLength { get; init; }

    /// <summary>
    /// 宽度调整值（单位：毫米）。
    /// </summary>
    public required decimal? AdjustedWidth { get; init; }

    /// <summary>
    /// 高度调整值（单位：毫米）。
    /// </summary>
    public required decimal? AdjustedHeight { get; init; }

    /// <summary>
    /// 体积调整值（单位：立方厘米）。
    /// </summary>
    public required decimal? AdjustedVolume { get; init; }

    /// <summary>
    /// 测量时间。
    /// </summary>
    public required DateTime MeasurementTime { get; init; }

    /// <summary>
    /// 体积绑定时间。
    /// </summary>
    public required DateTime? BindTime { get; init; }
}
