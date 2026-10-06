namespace Zeye.Sorting.Hub.Contracts.Models.Fusion;

/// <summary>不含机器凭据的多工作台状态。</summary>
/// <param name="SourceInstanceId">来源编码。</param>
/// <param name="WorkstationName">工作台名称。</param>
/// <param name="HubId">中心编码。</param>
/// <param name="TenantId">登记租户。</param>
/// <param name="StoragePartitionId">登记存储分区。</param>
/// <param name="LineId">产线。</param>
/// <param name="SiteCode">站点。</param>
/// <param name="DeviceCode">设备。</param>
/// <param name="JournalId">最近发送数据库身份。</param>
/// <param name="IsOnline">心跳租约有效状态。</param>
/// <param name="LastSeenAt">来源本地最近心跳时间。</param>
/// <param name="PendingFacts">等待确认事实数。</param>
/// <param name="RejectedFacts">被拒绝事实数。</param>
/// <param name="PendingImages">待上传图片数。</param>
/// <param name="DroppedUnacknowledgedFacts">累计舍弃事实数。</param>
/// <param name="DroppedUnacknowledgedImages">累计舍弃图片数。</param>
/// <param name="ProtectUnacknowledgedData">未确认缓存保护状态。</param>
/// <param name="RetainedBytes">发送缓存字节数。</param>
public sealed record FusionSourceStatus(
    string SourceInstanceId,
    string WorkstationName,
    string HubId,
    string TenantId,
    string StoragePartitionId,
    string LineId,
    string? SiteCode,
    string? DeviceCode,
    string? JournalId,
    bool IsOnline,
    DateTime? LastSeenAt,
    long PendingFacts,
    long RejectedFacts,
    long PendingImages,
    long DroppedUnacknowledgedFacts,
    long DroppedUnacknowledgedImages,
    bool ProtectUnacknowledgedData,
    long RetainedBytes);
