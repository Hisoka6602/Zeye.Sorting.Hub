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
    /// 落格时间。
    /// </summary>
    public required DateTime LandedTime { get; init; }
}
