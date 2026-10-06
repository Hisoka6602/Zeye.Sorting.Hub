using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Domain.Repositories;

/// <summary>包裹处理记录及当前快照的原子持久化契约。</summary>
public interface IParcelProcessingRepository {
    /// <summary>原子追加处理记录并刷新包裹快照，相同身份不得写入不同内容。</summary>
    Task<RepositoryResult<ParcelProcessingWriteResult>> AppendAsync(ParcelProcessingRecord record, CancellationToken cancellationToken);
    /// <summary>同一包裹的有界批量追加；其他实现顺序退化为已有逐条契约。</summary>
    async Task<IReadOnlyList<RepositoryResult<ParcelProcessingWriteResult>>> AppendBatchAsync(
        IReadOnlyList<ParcelProcessingRecord> records, CancellationToken cancellationToken) {
        if (records.Count is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(records));
        var results = new List<RepositoryResult<ParcelProcessingWriteResult>>(records.Count);
        foreach (var record in records) results.Add(await AppendAsync(record, cancellationToken));
        return results;
    }
    /// <summary>查询尚未关联包裹的处理记录，数量范围1至200。</summary>
    Task<IReadOnlyList<ParcelProcessingRecord>> GetUnboundAsync(int limit, CancellationToken cancellationToken);
}
