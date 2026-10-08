namespace Zeye.Sorting.Hub.Contracts.Models.Parcels;

/// <summary>有界多包裹对比；按用户选择顺序返回，缺失包裹显式列出。</summary>
public sealed record ParcelComparisonResponse {
    /// <summary>去重后的请求编号，64位身份按字符串传输。</summary>
    public required IReadOnlyList<string> RequestedIds { get; init; }
    /// <summary>已不存在或尚未入库的请求编号。</summary>
    public required IReadOnlyList<string> MissingIds { get; init; }
    /// <summary>实际找到的包裹，最多8票，不包含自动邻票。</summary>
    public required IReadOnlyList<ParcelComparisonItemResponse> Items { get; init; }
}
