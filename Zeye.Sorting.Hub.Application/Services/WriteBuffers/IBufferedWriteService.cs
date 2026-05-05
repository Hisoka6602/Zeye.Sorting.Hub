using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;

namespace Zeye.Sorting.Hub.Application.Services.WriteBuffers;

/// <summary>
/// 批量缓冲写入服务契约。
/// </summary>
public interface IBufferedWriteService {
    /// <summary>
    /// 将包裹集合写入缓冲通道。
    /// </summary>
    /// <param name="parcels">待入队的包裹集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>入队结果。</returns>
    Task<BufferedWriteResult> EnqueueAsync(Parcel[] parcels, CancellationToken cancellationToken);
}
