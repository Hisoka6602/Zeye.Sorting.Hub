namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

/// <summary>与每批物理删除同时提交的包裹身份和状态快照。</summary>
public sealed record ParcelCleanupDeletedItem {
    /// <summary>中心包裹编号。</summary>
    public long Id { get; init; }
    /// <summary>主条码。</summary>
    public string? BarCodes { get; init; }
    /// <summary>工作台。</summary>
    public string? WorkstationName { get; init; }
    /// <summary>来源实例。</summary>
    public string? SourceInstanceId { get; init; }
    /// <summary>来源会话。</summary>
    public string? SourceRunId { get; init; }
    /// <summary>来源包裹编号。</summary>
    public long? SourceParcelId { get; init; }
    /// <summary>创建本地时间。</summary>
    public DateTime CreatedTime { get; init; }
    /// <summary>扫码本地时间。</summary>
    public DateTime ScannedTime { get; init; }
    /// <summary>清理前状态。</summary>
    public int Status { get; init; }
}
