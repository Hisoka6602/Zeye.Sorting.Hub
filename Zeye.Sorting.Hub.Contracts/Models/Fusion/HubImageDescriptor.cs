namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>不可变图片描述，文件名不参与路径解析。</summary>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="JournalId">发送数据库身份。</param>
/// <param name="LeaseId">当前连接租约。</param>
/// <param name="SourceImageId">来源图片编号。</param>
/// <param name="FileName">展示文件名。</param>
/// <param name="ContentType">允许的图片媒体类型。</param>
/// <param name="SizeBytes">完整文件大小。</param>
/// <param name="ContentSha256">完整内容摘要。</param>
public sealed record HubImageDescriptor(
    string SourceInstanceId,
    string JournalId,
    string LeaseId,
    string SourceImageId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string ContentSha256);
