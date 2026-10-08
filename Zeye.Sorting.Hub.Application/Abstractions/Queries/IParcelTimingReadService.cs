using Zeye.Sorting.Hub.Contracts.Models.Parcels;

namespace Zeye.Sorting.Hub.Application.Abstractions.Queries;

/// <summary>包裹时序的精确锚点检索和有界邻近读取。</summary>
public interface IParcelTimingReadService {
    /// <summary>精确查找候选，保留数字条码与中心编号的歧义。</summary>
    Task<ParcelTimingCandidatesResponse> SearchAsync(string query, string searchBy, int pageNumber, CancellationToken cancellationToken);
    /// <summary>读取目标包裹及稳定排序的前后各5票时序。</summary>
    Task<ParcelTimingResponse?> ReadAsync(long id, CancellationToken cancellationToken);
    /// <summary>仅批量读取选定的最多8票量测与动作，不读取邻票或完整聚合报文。</summary>
    Task<ParcelComparisonResponse> CompareAsync(IReadOnlyList<long> ids, CancellationToken cancellationToken);
}
