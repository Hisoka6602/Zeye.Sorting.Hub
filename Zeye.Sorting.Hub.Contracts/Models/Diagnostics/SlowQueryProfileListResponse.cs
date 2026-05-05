namespace Zeye.Sorting.Hub.Contracts.Models.Diagnostics;

/// <summary>
/// 慢查询画像列表响应合同。
/// </summary>
public sealed record SlowQueryProfileListResponse {
    /// <summary>
    /// 快照生成时间（本地时间语义）。
    /// </summary>
    public DateTime GeneratedAtLocal { get; init; }

    /// <summary>
    /// 当前追踪到的指纹总数。
    /// </summary>
    public int TotalFingerprintCount { get; init; }

    /// <summary>
    /// 列表项。
    /// </summary>
    public required IReadOnlyList<SlowQueryProfileResponse> Items { get; init; }
}
