namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>对象存在后的耐久存储凭据。</summary>
/// <param name="HubId">中心编码。</param>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="SourceImageId">来源图片编号。</param>
/// <param name="SizeBytes">完整大小。</param>
/// <param name="ContentSha256">完整摘要。</param>
/// <param name="StorageProvider">存储实现名称。</param>
/// <param name="Bucket">存储分区。</param>
/// <param name="ObjectKey">安全对象键。</param>
public sealed record HubImageStoredReceipt(
    string HubId,
    string SourceInstanceId,
    string SourceImageId,
    long SizeBytes,
    string ContentSha256,
    string StorageProvider,
    string Bucket,
    string ObjectKey);
