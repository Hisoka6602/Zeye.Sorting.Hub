namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>心跳持久化确认。</summary>
/// <param name="HubId">中心编码。</param>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="JournalId">发送数据库身份。</param>
/// <param name="ReceivedAtUtc">协议确认时间。</param>
public sealed record HubHeartbeatReceipt(
    string HubId,
    string SourceInstanceId,
    string JournalId,
    DateTimeOffset ReceivedAtUtc);
