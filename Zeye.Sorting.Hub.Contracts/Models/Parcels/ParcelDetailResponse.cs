namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>
/// Parcel 详情响应合同。
/// </summary>
public sealed record ParcelDetailResponse : ParcelListItemResponse {
    /// <summary>
    /// 使用列表项合同初始化详情合同。
    /// </summary>
    /// <param name="source">列表项合同。</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public ParcelDetailResponse(ParcelListItemResponse source)
        : base(source ?? throw new ArgumentNullException(nameof(source))) {
    }
}
