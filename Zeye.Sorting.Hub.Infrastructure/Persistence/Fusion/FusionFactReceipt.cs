namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

/// <summary>不可变原始接收簿及同事务待投影任务。</summary>
public sealed class FusionFactReceipt {
    /// <summary>来源发送库和事实编号联合摘要。</summary>
    public string Key { get; set; } = "";
    /// <summary>来源编码。</summary>
    public string SourceInstanceId { get; set; } = "";
    /// <summary>发送数据库身份。</summary>
    public string JournalId { get; set; } = "";
    /// <summary>来源事实编号。</summary>
    public string RecordId { get; set; } = "";
    /// <summary>有效十进制序号。</summary>
    public long SourceSequence { get; set; }
    /// <summary>原始字节摘要。</summary>
    public string BodySha256 { get; set; } = "";
    /// <summary>不可变原始报文。</summary>
    public string BodyJson { get; set; } = "";
    /// <summary>接收时已经验证的业务用例输入，部署配置变化不重写历史投影语义。</summary>
    public string? ProjectionJson { get; set; }
    /// <summary>事实类型。</summary>
    public string Kind { get; set; } = "";
    /// <summary>中心本地接收时间。</summary>
    public DateTime ReceivedAt { get; set; }
    /// <summary>来源本地发生时间。</summary>
    public DateTime OccurredAt { get; set; }
    /// <summary>接收时登记租户。</summary>
    public string TenantId { get; set; } = "";
    /// <summary>接收时登记分区。</summary>
    public string StoragePartitionId { get; set; } = "";
    /// <summary>pending、complete 或 retry。</summary>
    public string ProjectionState { get; set; } = "pending";
    /// <summary>投影认领次数。</summary>
    public int ProjectionAttempts { get; set; }
    /// <summary>下一次本地投影时间。</summary>
    public DateTime NextProjectionAt { get; set; }
    /// <summary>当前工作者认领身份。</summary>
    public string? ProjectionClaimId { get; set; }
    /// <summary>认领失效时间。</summary>
    public DateTime? ProjectionClaimUntil { get; set; }
    /// <summary>安全投影错误编码。</summary>
    public string? ProjectionError { get; set; }
    /// <summary>中心包裹编号。</summary>
    public string? ParcelId { get; set; }
}
