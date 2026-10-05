namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>具有来源租约的事实批次。</summary>
/// <param name="ProtocolVersion">协议版本。</param>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="JournalId">发送数据库身份。</param>
/// <param name="LeaseId">当前连接租约。</param>
/// <param name="BatchId">批次编号。</param>
/// <param name="Records">逐条不可变事实。</param>
public sealed record HubFactBatch(
    string ProtocolVersion,
    string SourceInstanceId,
    string JournalId,
    string LeaseId,
    string BatchId,
    IReadOnlyList<HubFactEnvelope> Records);
