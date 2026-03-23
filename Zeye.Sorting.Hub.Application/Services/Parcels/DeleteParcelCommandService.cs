using NLog;
using Zeye.Sorting.Hub.Application.Utilities;
using Zeye.Sorting.Hub.Domain.Repositories;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>
/// 管理端删除单个包裹应用服务。
/// </summary>
public sealed class DeleteParcelCommandService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Parcel 仓储。
    /// </summary>
    private readonly IParcelRepository _parcelRepository;

    /// <summary>
    /// 初始化管理端删除单个包裹应用服务。
    /// </summary>
    /// <param name="parcelRepository">Parcel 仓储。</param>
    public DeleteParcelCommandService(IParcelRepository parcelRepository) {
        _parcelRepository = parcelRepository ?? throw new ArgumentNullException(nameof(parcelRepository));
    }

    /// <summary>
    /// 执行删除指定包裹。
    /// </summary>
    /// <param name="parcelId">目标包裹 Id（必须大于 0）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>true 表示删除成功；false 表示包裹不存在。</returns>
    public async Task<bool> ExecuteAsync(long parcelId, CancellationToken cancellationToken) {
        Guard.ThrowIfZeroOrNegative(parcelId, nameof(parcelId), "包裹 Id 必须大于 0。", "删除包裹");

        try {
            // 步骤 1：加载包裹聚合根；不存在则返回 false，由 Host 层返回 404。
            var parcel = await _parcelRepository.GetByIdAsync(parcelId, cancellationToken);
            if (parcel is null) {
                return false;
            }

            // 步骤 2：调用仓储删除，持久化移除操作。
            var result = await _parcelRepository.RemoveAsync(parcel, cancellationToken);
            if (!result.IsSuccess) {
                Logger.Error("删除包裹失败，ParcelId={ParcelId}, ErrorMessage={ErrorMessage}", parcelId, result.ErrorMessage);
                throw new InvalidOperationException(result.ErrorMessage ?? "删除包裹失败。");
            }

            return true;
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException) {
            Logger.Error(ex, "删除包裹发生意外异常，ParcelId={ParcelId}", parcelId);
            throw;
        }
    }
}
