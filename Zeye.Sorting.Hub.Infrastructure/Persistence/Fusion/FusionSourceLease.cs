namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

/// <summary>跨服务进程的单来源连接租约。</summary>
public sealed class FusionSourceLease {
    /// <summary>来源主键。</summary>
    public string SourceInstanceId { get; set; } = "";
    /// <summary>最新发送数据库身份。</summary>
    public string JournalId { get; set; } = "";
    /// <summary>连接身份。</summary>
    public string ConnectionId { get; set; } = "";
    /// <summary>租约身份。</summary>
    public string LeaseId { get; set; } = "";
    /// <summary>中心进程身份。</summary>
    public string ServerId { get; set; } = "";
    /// <summary>中心本地租约到期时间。</summary>
    public DateTime ExpiresAt { get; set; }
    /// <summary>中心本地最近心跳时间。</summary>
    public DateTime? LastSeenAt { get; set; }
    /// <summary>数据库并发版本。</summary>
    public long Revision { get; set; }
}
