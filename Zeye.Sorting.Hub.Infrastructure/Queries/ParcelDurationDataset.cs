using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

namespace Zeye.Sorting.Hub.Infrastructure.Queries;

/// <summary>仅保留分析所需标量样本，区间和分页共用同一总体。</summary>
internal sealed record ParcelDurationDataset(DateTime GeneratedAt, long ObservedCount, long UnavailableCount,
    IReadOnlyList<ParcelDurationSampleResponse> Samples) {
    /// <summary>仅保存本次选中业务类型的结果，各类型独立读取、缓存和刷新。</summary>
    public IReadOnlyDictionary<string, ParcelDurationDataset>? ByType { get; init; }
}
