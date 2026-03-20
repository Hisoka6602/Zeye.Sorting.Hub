using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Validation;

namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;

/// <summary>
/// Parcel 列表查询过滤参数（第一阶段最小集合）。
/// </summary>
[MaxTimeRange(nameof(ScannedTimeStart), nameof(ScannedTimeEnd), maxMonths: 3)]
public sealed record ParcelQueryFilter {
    /// <summary>
    /// 主条码关键字。
    /// </summary>
    public string? BarCodeKeyword { get; init; }

    /// <summary>
    /// 集包号。
    /// </summary>
    public string? BagCode { get; init; }

    /// <summary>
    /// 工作台名称。
    /// </summary>
    public string? WorkstationName { get; init; }

    /// <summary>
    /// 包裹状态。
    /// </summary>
    public ParcelStatus? Status { get; init; }

    /// <summary>
    /// 实际格口 Id。
    /// </summary>
    public long? ActualChuteId { get; init; }

    /// <summary>
    /// 目标格口 Id。
    /// </summary>
    public long? TargetChuteId { get; init; }

    /// <summary>
    /// 扫码开始时间（含边界）。
    /// </summary>
    public DateTime? ScannedTimeStart { get; init; }

    /// <summary>
    /// 扫码结束时间（含边界）。
    /// </summary>
    public DateTime? ScannedTimeEnd { get; init; }
}
