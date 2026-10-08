using Zeye.Sorting.Hub.Contracts.Models.Parcels.Dws;

namespace Zeye.Sorting.Hub.Application.Abstractions.Queries;

/// <summary>独立只读DWS测量一致性分析，复用包裹读取权限。</summary>
public interface IParcelDwsConsistencyReadService {
    /// <summary>查询去重后的量测差异与有界可追溯明细。</summary>
    Task<ParcelDwsConsistencyResponse> ReadAsync(ParcelDwsConsistencyRequest request, CancellationToken cancellationToken);
}
