using System.Globalization;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>批量映射同一包裹的事实，保持逐记录验证与可审计返回结果。</summary>
public sealed partial class ParcelProcessingApplicationService {
    /// <summary>统一服务器入库时间；仓储仅对同来源会话包裹实施原子有界批次。</summary>
    public async Task<IReadOnlyList<RepositoryResult<ParcelProcessingWriteResponse>>> AppendBatchAsync(
        IReadOnlyList<ParcelProcessingRecordRequest> requests, CancellationToken cancellationToken) {
        if (requests.Count is < 1 or > 64) throw new ArgumentOutOfRangeException(nameof(requests));
        var recordedAt = DateTime.Now;
        var records = requests.Select(request => ParcelProcessingContractMapper.ToDomain(request, recordedAt)).ToArray();
        var outcomes = await _repository.AppendBatchAsync(records, cancellationToken);
        return outcomes.Select(result => result.IsSuccess && result.Value is not null
            ? RepositoryResult<ParcelProcessingWriteResponse>.Success(new() { ParcelId = result.Value.ParcelId?.ToString(CultureInfo.InvariantCulture),
                IsDuplicate = result.Value.IsDuplicate, PartitionSuffix = result.Value.PartitionSuffix })
            : RepositoryResult<ParcelProcessingWriteResponse>.Fail(result.ErrorMessage ?? "保存处理记录失败。", result.ErrorCode ?? "ParcelProcessingWriteFailed")).ToArray();
    }
}
