namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>图片恢复偏移与已完成确认。</summary>
/// <param name="SourceImageId">来源图片编号。</param>
/// <param name="ContentSha256">内容摘要。</param>
/// <param name="UploadId">上传身份。</param>
/// <param name="NextOffset">已经耐久写入的字节偏移。</param>
/// <param name="MaxChunkBytes">最大解码分块大小。</param>
/// <param name="Stored">已完成时的存储确认。</param>
public sealed record HubImageBeginReceipt(
    string SourceImageId,
    string ContentSha256,
    string UploadId,
    long NextOffset,
    int MaxChunkBytes,
    HubImageStoredReceipt? Stored = null);
