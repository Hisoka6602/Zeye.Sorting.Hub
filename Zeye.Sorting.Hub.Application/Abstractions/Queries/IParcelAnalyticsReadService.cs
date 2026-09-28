using Zeye.Sorting.Hub.Contracts.Models.Parcels.Analytics;

namespace Zeye.Sorting.Hub.Application.Abstractions.Queries;

/// <summary>有界、只读的包裹运营分析查询合同。</summary>
public interface IParcelAnalyticsReadService {
    /// <summary>按两个包含端点的本地日期生成来源包裹快照及独立处理事实指标。</summary>
    Task<ParcelAnalyticsResponse> GetAsync(DateTime fromLocalDate, DateTime toLocalDate, CancellationToken cancellationToken);
}
