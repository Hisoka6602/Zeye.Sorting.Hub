using Zeye.Sorting.Hub.Contracts.Models.Parcels.Workbench;

namespace Zeye.Sorting.Hub.Application.Abstractions.Queries;

/// <summary>工作台完整窗口统计；结束时间由宿主提供，不接受无限查询范围。</summary>
public interface IParcelWorkbenchReadService {
    /// <summary>统计截至指定本地时刻的滚动24小时已入库包裹。</summary>
    Task<ParcelWorkbenchResponse> GetAsync(DateTime nowLocal, CancellationToken cancellationToken);
}
