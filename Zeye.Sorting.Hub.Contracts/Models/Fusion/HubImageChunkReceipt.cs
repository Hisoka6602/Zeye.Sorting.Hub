namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>文件落盘后的分块确认。</summary>
/// <param name="UploadId">上传身份。</param>
/// <param name="NextOffset">下一耐久偏移。</param>
public sealed record HubImageChunkReceipt(
    string UploadId,
    long NextOffset);
