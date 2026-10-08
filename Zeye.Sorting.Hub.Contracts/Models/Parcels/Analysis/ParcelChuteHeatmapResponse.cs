namespace Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

/// <summary>目标或实际格口的独立热力分布；缺失编码不补成零票格口。</summary>
public sealed record ParcelChuteHeatmapResponse {
    /// <summary>完整总体中当前方向格口编码有效的票数，不受返回上限影响。</summary>
    public long SampleCount { get; init; }
    /// <summary>完整总体中当前方向编码缺失或为空白的票数。</summary>
    public long MissingCodeCount { get; init; }
    /// <summary>按总票数降序返回的格口分组，每组数据均为完整聚合。</summary>
    public IReadOnlyList<ParcelChuteHeatmapCellResponse> Cells { get; init; } = [];
    /// <summary>格口数量超过预算，仅返回最繁忙的部分格口；组内票数仍完整。</summary>
    public bool Truncated { get; init; }
}
