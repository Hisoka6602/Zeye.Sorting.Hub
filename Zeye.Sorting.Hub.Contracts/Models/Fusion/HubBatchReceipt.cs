namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>不使用高水位替代确认的批次结果。</summary>
/// <param name="HubId">中心编码。</param>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="JournalId">发送数据库身份。</param>
/// <param name="BatchId">批次编号。</param>
/// <param name="Records">逐条耐久确认。</param>
public sealed record HubBatchReceipt(
    string HubId,
    string SourceInstanceId,
    string JournalId,
    string BatchId,
    IReadOnlyList<HubFactReceipt> Records);
