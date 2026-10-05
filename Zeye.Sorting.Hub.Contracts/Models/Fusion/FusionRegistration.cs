namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>耐久注册租约及传输限额。</summary>
/// <param name="ProtocolVersion">协议版本。</param>
/// <param name="HubId">中心编码。</param>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="JournalId">发送数据库身份。</param>
/// <param name="LeaseId">当前连接租约。</param>
/// <param name="MaxBatchRecords">最大批次条数。</param>
/// <param name="MaxBatchBytes">最大批次字节数。</param>
/// <param name="MaxImageChunkBytes">最大图片块解码字节数。</param>
public sealed record FusionRegistration(
    string ProtocolVersion,
    string HubId,
    string SourceInstanceId,
    string JournalId,
    string LeaseId,
    int MaxBatchRecords,
    int MaxBatchBytes,
    int MaxImageChunkBytes);
