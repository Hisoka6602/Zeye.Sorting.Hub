using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analysis;

namespace Zeye.Sorting.Hub.Application.Abstractions.Queries;

/// <summary>包裹异常、完成耗时及格口流向分析的只读入口。</summary>
public interface IParcelAnalysisReadService {
    /// <summary>在有界首次入库日期范围内执行数据库聚合和分页下钻。</summary>
    Task<ParcelAnalysisResponse> ReadAsync(ParcelAnalysisRequest request, CancellationToken cancellationToken);
}
