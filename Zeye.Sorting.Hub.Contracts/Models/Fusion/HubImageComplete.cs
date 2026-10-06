namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>完整文件校验请求。</summary>
/// <param name="UploadId">上传身份。</param>
/// <param name="SourceImageId">来源图片编号。</param>
/// <param name="SizeBytes">完整大小。</param>
/// <param name="ContentSha256">完整摘要。</param>
public sealed record HubImageComplete(
    string UploadId,
    string SourceImageId,
    long SizeBytes,
    string ContentSha256);
