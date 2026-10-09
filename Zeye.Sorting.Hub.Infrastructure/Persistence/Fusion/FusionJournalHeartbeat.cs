namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

/// <summary>按发送数据库保留的心跳及累计数据舍弃证据。</summary>
public sealed class FusionJournalHeartbeat {
    /// <summary>来源和发送数据库联合摘要。</summary>
    public string Key { get; set; } = "";
    /// <summary>稳定来源编码。</summary>
    public string SourceInstanceId { get; set; } = "";
    /// <summary>发送数据库身份。</summary>
    public string JournalId { get; set; } = "";
    /// <summary>中心本地接收时间。</summary>
    public DateTime ReceivedAt { get; set; }
    /// <summary>来源本地发送时间。</summary>
    public DateTime SentAt { get; set; }
    /// <summary>待确认事实数。</summary>
    public long PendingFacts { get; set; }
    /// <summary>已拒绝事实数。</summary>
    public long RejectedFacts { get; set; }
    /// <summary>待传图片数。</summary>
    public long PendingImages { get; set; }
    /// <summary>累计舍弃事实数。</summary>
    public long DroppedUnacknowledgedFacts { get; set; }
    /// <summary>累计舍弃图片数。</summary>
    public long DroppedUnacknowledgedImages { get; set; }
    /// <summary>保护未确认数据状态。</summary>
    public bool ProtectUnacknowledgedData { get; set; }
    /// <summary>来源缓存字节数。</summary>
    public long RetainedBytes { get; set; }
}
