namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>包裹已登记图片及可供浏览器访问的临时地址。</summary>
/// <param name="ParcelId">包裹编号。</param>
/// <param name="HasImages">来源记录是否标记存在图片。</param>
/// <param name="Images">去重后的图片集合。</param>
public sealed record ParcelImagesResponse(long ParcelId, bool HasImages, IReadOnlyList<ParcelImageResponse> Images);
