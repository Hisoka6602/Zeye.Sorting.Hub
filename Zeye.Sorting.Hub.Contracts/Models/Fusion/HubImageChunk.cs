namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>图片顺序分块。</summary>
/// <param name="UploadId">上传身份。</param>
/// <param name="Offset">块起始偏移。</param>
/// <param name="DataBase64">编码后的块内容。</param>
/// <param name="ChunkSha256">解码字节摘要。</param>
public sealed record HubImageChunk(
    string UploadId,
    long Offset,
    string DataBase64,
    string ChunkSha256);
