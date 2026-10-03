namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>图片来源信息；无法预览时仍保留来源事实。</summary>
/// <param name="CameraName">来源相机名称。</param>
/// <param name="SourcePath">来源保存的图片路径或对象键。</param>
/// <param name="Url">可访问的 HTTP(S) 地址。</param>
/// <param name="UnavailableReason">无法生成预览地址的中文原因。</param>
public sealed record ParcelImageResponse(string CameraName, string SourcePath, string? Url, string? UnavailableReason);
