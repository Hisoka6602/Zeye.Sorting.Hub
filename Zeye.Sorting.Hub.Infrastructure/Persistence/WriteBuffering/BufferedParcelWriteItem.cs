using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

/// <summary>
/// Parcel 缓冲写入通道项。
/// </summary>
/// <param name="Parcel">待写入的包裹聚合。</param>
/// <param name="EnqueuedAtLocal">入队时间（本地时间）。</param>
/// <param name="RetryCount">当前重试次数。</param>
/// <param name="LastErrorMessage">最近一次失败消息。</param>
/// <param name="LastRetryAtLocal">最近一次重试时间（本地时间）。</param>
public readonly record struct BufferedParcelWriteItem(
    Parcel Parcel,
    DateTime EnqueuedAtLocal,
    int RetryCount,
    string? LastErrorMessage,
    DateTime? LastRetryAtLocal);
