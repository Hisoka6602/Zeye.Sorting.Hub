using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.WriteBuffering;

/// <summary>
/// Parcel 死信记录。
/// </summary>
/// <param name="Parcel">写入失败的包裹聚合。</param>
/// <param name="FailedAtLocal">进入死信时间（本地时间）。</param>
/// <param name="RetryCount">失败前已执行的重试次数。</param>
/// <param name="ErrorMessage">失败消息。</param>
/// <param name="LastRetryAtLocal">最近一次重试时间（本地时间）。</param>
public readonly record struct DeadLetterWriteEntry(
    Parcel Parcel,
    DateTime FailedAtLocal,
    int RetryCount,
    string ErrorMessage,
    DateTime? LastRetryAtLocal);
