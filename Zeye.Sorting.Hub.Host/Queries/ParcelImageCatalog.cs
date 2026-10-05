using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Enums.ObjectStorage;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Host.Queries;

/// <summary>汇总已保存图片信息与来源处理事实，不推测来源设备的文件访问地址。</summary>
public static class ParcelImageCatalog {
    /// <summary>优先使用对象存储元数据，登记和上传同一路径只展示一次。</summary>
    /// <param name="parcel">已保存包裹详情。</param>
    /// <param name="signReadUrl">现有对象存储的读取签名器，未启用时为空。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>图片预览集合。</returns>
    public static async Task<ParcelImagesResponse> BuildAsync(
        ParcelDetailResponse parcel,
        Func<string, string, CancellationToken, Task<string>>? signReadUrl,
        CancellationToken cancellationToken) {
        var images = new List<ParcelImageResponse>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var image in parcel.ImageInfos) {
            var path = string.IsNullOrWhiteSpace(image.RelativePath) ? image.ObjectKey : image.RelativePath;
            if (string.IsNullOrWhiteSpace(path) || !seen.Add(path)) continue;
            var url = WebUrl(path);
            string? reason = null;
            if (image.StorageProvider == (int)ObjectStorageProvider.Minio && !string.IsNullOrWhiteSpace(image.BucketName) && !string.IsNullOrWhiteSpace(image.ObjectKey)) {
                if (signReadUrl is null) reason = "对象存储尚未启用，暂时无法预览。";
                else {
                    try {
                        url = WebUrl(await signReadUrl(image.BucketName, image.ObjectKey, cancellationToken));
                        if (url is null) reason = "对象存储未返回可访问的图片地址。";
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException) {
                        reason = "暂时无法获取图片访问地址，请重试。";
                    }
                }
            }
            images.Add(new(image.CameraName, path, url, url is null ? reason ?? LocalPathReason : null));
        }
        foreach (var record in parcel.ProcessingRecords) {
            if (record.Stage is not ((int)ParcelProcessingStage.ImageRegistered) and not ((int)ParcelProcessingStage.ImageUploaded)
                || record.IsSuccess == false || string.IsNullOrWhiteSpace(record.ImagePath) || !seen.Add(record.ImagePath)) continue;
            var url = WebUrl(record.ImagePath);
            images.Add(new(record.ImageCamera ?? "", record.ImagePath, url, url is null ? LocalPathReason : null));
        }
        return new(parcel.Id, parcel.HasImages || images.Count > 0, images);
    }

    /// <summary>来源仅保存本地路径时的预览提示。</summary>
    private const string LocalPathReason = "图片已登记，但来源尚未提供可访问的图片地址。";

    /// <summary>只接受不包含访问凭据的浏览器网页地址。</summary>
    private static string? WebUrl(string path) => System.Text.RegularExpressions.Regex.IsMatch(path, "^/api/parcels/fusion/images/[a-f0-9]{64}/content$")
        ? path : Uri.TryCreate(path, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo) ? uri.AbsoluteUri : null;
}
