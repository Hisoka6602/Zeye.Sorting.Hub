using System.ComponentModel;

namespace Zeye.Sorting.Hub.Domain.Enums;

/// <summary>
/// Parcel 更新操作类型。
/// </summary>
public enum ParcelUpdateOperation {
    /// <summary>
    /// 标记包裹完结（需提供完结时间）。
    /// </summary>
    [Description("标记完结")]
    MarkCompleted = 1,

    /// <summary>
    /// 标记分拣异常（需提供异常类型）。
    /// </summary>
    [Description("标记分拣异常")]
    MarkSortingException = 2,

    /// <summary>
    /// 更新外部接口访问状态（需提供访问状态值）。
    /// </summary>
    [Description("更新接口访问状态")]
    UpdateRequestStatus = 3
}
