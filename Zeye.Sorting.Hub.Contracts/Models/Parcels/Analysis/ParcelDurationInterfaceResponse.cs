namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>按接口与Provider汇总，避免不同调用混为一个指标。</summary>
public sealed record ParcelDurationInterfaceResponse {
    /// <summary>业务Provider，未知时为空。</summary>
    public string? Provider { get; init; }
    /// <summary>不含查询参数的接口地址，不返回认证参数。</summary>
    public string? RequestUrl { get; init; }
    /// <summary>有效调用次数。</summary>
    public long Count { get; init; }
    /// <summary>明确失败次数。</summary>
    public long FailedCount { get; init; }
    /// <summary>平均耗时。</summary>
    public decimal AverageMilliseconds { get; init; }
    /// <summary>P95耗时。</summary>
    public decimal P95Milliseconds { get; init; }
}


