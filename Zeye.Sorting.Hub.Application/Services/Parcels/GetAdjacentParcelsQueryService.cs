using NLog;
using Zeye.Sorting.Hub.Application.Utilities;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// 查询 Parcel 邻近记录应用服务。
/// </summary>
public sealed class GetAdjacentParcelsQueryService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Parcel 仓储。
    /// </summary>
    private readonly IParcelRepository _parcelRepository;

    /// <summary>
    /// 初始化查询 Parcel 邻近记录应用服务。
    /// </summary>
    /// <param name="parcelRepository">Parcel 仓储。</param>
    public GetAdjacentParcelsQueryService(IParcelRepository parcelRepository) {
        _parcelRepository = parcelRepository ?? throw new ArgumentNullException(nameof(parcelRepository));
    }

    /// <summary>
    /// 执行邻近查询。
    /// </summary>
    /// <param name="request">邻近查询请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>邻近查询响应。</returns>
    public async Task<ParcelAdjacentResponse> ExecuteAsync(ParcelAdjacentRequest request, CancellationToken cancellationToken) {
        if (request is null) {
            throw new ArgumentNullException(nameof(request));
        }

        Guard.ThrowIfZeroOrNegative(request.Id, nameof(request.Id), "包裹 Id 必须大于 0。", "查询 Parcel 邻近记录");
        Guard.ThrowIfNegative(request.BeforeCount, nameof(request.BeforeCount), "前向查询条数不能小于 0。", "查询 Parcel 邻近记录");
        Guard.ThrowIfNegative(request.AfterCount, nameof(request.AfterCount), "后向查询条数不能小于 0。", "查询 Parcel 邻近记录");

        // 归一化：将条数上限收敛至 IParcelRepository.MaxAdjacentCountPerSide，避免过大查询开销。
        var beforeCount = Math.Min(request.BeforeCount, IParcelRepository.MaxAdjacentCountPerSide);
        var afterCount = Math.Min(request.AfterCount, IParcelRepository.MaxAdjacentCountPerSide);
        try {
            // 步骤 1：使用归一化数量调用仓储邻近查询，保证单次查询开销可控。
            var adjacentResult = await _parcelRepository.GetAdjacentByIdAsync(
                request.Id,
                beforeCount,
                afterCount,
                cancellationToken);
            if (!adjacentResult.IsSuccess) {
                throw new KeyNotFoundException(adjacentResult.ErrorMessage);
            }

            var adjacent = adjacentResult.Value ?? Array.Empty<ParcelSummaryReadModel>();
            // 步骤 2：统一映射为 Contracts 响应，避免 Host 层重复映射。
            var items = adjacent.Select(ParcelContractMapper.ToListItem).ToArray();
            return new ParcelAdjacentResponse {
                Items = items,
                BeforeCount = beforeCount,
                AfterCount = afterCount
            };
        }
        catch (KeyNotFoundException ex) {
            Logger.Warn(
                ex,
                "查询 Parcel 邻近记录未找到，Id={ParcelId}, BeforeCount={BeforeCount}, AfterCount={AfterCount}",
                request.Id,
                request.BeforeCount,
                request.AfterCount);
            throw;
        }
        catch (Exception ex) {
            Logger.Error(
                ex,
                "查询 Parcel 邻近记录失败，Id={ParcelId}, BeforeCount={BeforeCount}, AfterCount={AfterCount}",
                request.Id,
                request.BeforeCount,
                request.AfterCount);
            throw;
        }
    }
}
