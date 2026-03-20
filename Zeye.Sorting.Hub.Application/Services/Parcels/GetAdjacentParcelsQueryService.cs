using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// 查询 Parcel 邻近记录应用服务。
/// </summary>
public sealed class GetAdjacentParcelsQueryService {
    /// <summary>
    /// 邻近单侧最大条数。
    /// </summary>
    private const int MaxAdjacentCountPerSide = 100;

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

        if (request.BeforeCount < 0) {
            Logger.Warn("查询 Parcel 邻近记录参数非法，BeforeCount={0}", request.BeforeCount);
            throw new ArgumentOutOfRangeException(nameof(request.BeforeCount), "前向查询条数不能小于 0。");
        }

        if (request.AfterCount < 0) {
            Logger.Warn("查询 Parcel 邻近记录参数非法，AfterCount={0}", request.AfterCount);
            throw new ArgumentOutOfRangeException(nameof(request.AfterCount), "后向查询条数不能小于 0。");
        }

        var beforeCount = NormalizeAdjacentCount(request.BeforeCount);
        var afterCount = NormalizeAdjacentCount(request.AfterCount);
        try {
            // 步骤 1：使用归一化数量调用仓储邻近查询，保证单次查询开销可控。
            var adjacent = await _parcelRepository.GetAdjacentByScannedTimeAsync(
                request.ScannedTime,
                beforeCount,
                afterCount,
                cancellationToken);
            // 步骤 2：统一映射为 Contracts 响应，避免 Host 层重复映射。
            var items = adjacent.Select(ParcelContractMapper.ToListItem).ToArray();
            return new ParcelAdjacentResponse {
                Items = items,
                BeforeCount = beforeCount,
                AfterCount = afterCount
            };
        }
        catch (Exception ex) {
            Logger.Error(
                ex,
                "查询 Parcel 邻近记录失败，ScannedTime={0}, BeforeCount={1}, AfterCount={2}",
                request.ScannedTime,
                request.BeforeCount,
                request.AfterCount);
            throw;
        }
    }

    /// <summary>
    /// 归一化邻近查询数量，避免过大查询开销。
    /// </summary>
    /// <param name="count">请求数量。</param>
    /// <returns>归一化后的数量。</returns>
    private static int NormalizeAdjacentCount(int count) {
        return count > MaxAdjacentCountPerSide ? MaxAdjacentCountPerSide : count;
    }
}
