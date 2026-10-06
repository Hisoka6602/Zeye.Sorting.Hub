namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;

/// <summary>
/// 格口信息响应合同。
/// </summary>
public sealed record ChuteInfoResponse {
    /// <summary>
    /// 目标格口 Id。
    /// </summary>
    public required long? TargetChuteId { get; init; }

    /// <summary>
    /// 实际落格格口 Id。
    /// </summary>
    public required long? ActualChuteId { get; init; }

    /// <summary>
    /// 备用格口 Id。
    /// </summary>
    public required long? BackupChuteId { get; init; }

    /// <summary>
    /// 落格时间；尚未确认落格时为空。
    /// </summary>
    public required DateTime? LandedTime { get; init; }

    /// <summary>原始目标格口编码，保留前导零及非数字编码。</summary>
    public string? TargetChuteCode { get; init; }

    /// <summary>原始实际落格编码，保留前导零及非数字编码。</summary>
    public string? ActualChuteCode { get; init; }
}
