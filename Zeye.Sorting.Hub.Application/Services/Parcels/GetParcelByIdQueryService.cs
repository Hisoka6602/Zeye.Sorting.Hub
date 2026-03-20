using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// 按 Id 查询 Parcel 详情应用服务。
/// </summary>
public sealed class GetParcelByIdQueryService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Parcel 仓储。
    /// </summary>
    private readonly IParcelRepository _parcelRepository;

    /// <summary>
    /// 初始化按 Id 查询 Parcel 详情应用服务。
    /// </summary>
    /// <param name="parcelRepository">Parcel 仓储。</param>
    public GetParcelByIdQueryService(IParcelRepository parcelRepository) {
        _parcelRepository = parcelRepository ?? throw new ArgumentNullException(nameof(parcelRepository));
    }

    /// <summary>
    /// 执行按 Id 查询详情。
    /// </summary>
    /// <param name="parcelId">包裹 Id。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>详情响应；不存在时返回 null。</returns>
    public async Task<ParcelDetailResponse?> ExecuteAsync(long parcelId, CancellationToken cancellationToken) {
        if (parcelId <= 0) {
            Logger.Warn("按 Id 查询 Parcel 详情参数非法，ParcelId={0}", parcelId);
            throw new ArgumentOutOfRangeException(nameof(parcelId), "包裹 Id 必须大于 0。");
        }

        try {
            var parcel = await _parcelRepository.GetByIdAsync(parcelId, cancellationToken);
            return parcel is null ? null : ParcelContractMapper.ToDetail(parcel);
        }
        catch (Exception ex) {
            Logger.Error(ex, "按 Id 查询 Parcel 详情失败，ParcelId={0}", parcelId);
            throw;
        }
    }
}
