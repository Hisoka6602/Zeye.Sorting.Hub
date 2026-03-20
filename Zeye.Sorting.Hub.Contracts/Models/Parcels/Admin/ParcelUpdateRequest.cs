using Zeye.Sorting.Hub.Contracts.Enums.Parcels;

namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;

/// <summary>
/// 管理端更新 Parcel 状态请求合同。
/// 每次请求仅允许触发一种状态转换操作（由 Operation 字段决定）。
/// </summary>
public sealed record ParcelUpdateRequest {
    /// <summary>
    /// 操作类型（决定本次状态转换的具体语义，见 <see cref="ParcelUpdateOperation"/>）。
    /// </summary>
    public required int Operation { get; init; }

    /// <summary>
    /// 完结时间（本地时间，仅 Operation=MarkCompleted 时有效，不允许 UTC/offset）。
    /// </summary>
    public DateTime? CompletedTime { get; init; }

    /// <summary>
    /// 分拣异常类型（对应 Domain.ParcelExceptionType 枚举数值，仅 Operation=MarkSortingException 时有效）。
    /// </summary>
    public int? ExceptionType { get; init; }

    /// <summary>
    /// 外部接口访问状态（对应 Domain.ApiRequestStatus 枚举数值，仅 Operation=UpdateRequestStatus 时有效）。
    /// </summary>
    public int? RequestStatus { get; init; }
}
