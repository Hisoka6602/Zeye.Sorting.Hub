namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>异常类型的完整数据库分组计数。</summary>
public sealed record ParcelAnalysisGroupResponse {
    /// <summary>用于下钻的枚举数值，未提供时为空。</summary>
    public int? Code { get; init; }
    /// <summary>服务端领域说明中的中文名称。</summary>
    public required string Name { get; init; }
    /// <summary>该类型的当前异常票数。</summary>
    public long Count { get; init; }
}
