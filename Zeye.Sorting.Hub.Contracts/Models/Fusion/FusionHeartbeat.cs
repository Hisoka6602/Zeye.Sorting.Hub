namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>来源心跳及发送缓存舍弃指标。</summary>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="JournalId">发送数据库身份。</param>
/// <param name="LeaseId">当前连接租约。</param>
/// <param name="SentAtUtc">协议发送时间。</param>
/// <param name="PendingFacts">待确认事实数。</param>
/// <param name="RejectedFacts">被拒绝事实数。</param>
/// <param name="PendingImages">待传图片数。</param>
/// <param name="DroppedUnacknowledgedFacts">累计舍弃未确认事实数。</param>
/// <param name="DroppedUnacknowledgedImages">累计舍弃未确认图片数。</param>
/// <param name="ProtectUnacknowledgedData">发送缓存保护状态。</param>
/// <param name="RetainedBytes">发送缓存字节数。</param>
public sealed record FusionHeartbeat(
    string SourceInstanceId,
    string JournalId,
    string LeaseId,
    DateTimeOffset SentAtUtc,
    long PendingFacts,
    long RejectedFacts,
    long PendingImages,
    long DroppedUnacknowledgedFacts = 0,
    long DroppedUnacknowledgedImages = 0,
    bool ProtectUnacknowledgedData = false,
    long RetainedBytes = 0);
