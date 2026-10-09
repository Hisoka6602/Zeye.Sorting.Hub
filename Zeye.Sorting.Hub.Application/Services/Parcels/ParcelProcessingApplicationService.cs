using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Repositories;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Application.Services.Parcels;

/// <summary>处理事实追加与未绑定事实检索应用服务。</summary>
public sealed partial class ParcelProcessingApplicationService {
    /// <summary>原子处理记录仓储。</summary>
    private readonly IParcelProcessingRepository _repository;
    /// <summary>输入验证与应用异常日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>组装处理事实应用服务。</summary>
    public ParcelProcessingApplicationService(IParcelProcessingRepository repository) => _repository = repository;

    /// <summary>验证来源合同后追加事实，服务端决定入库时间；传输方式不参与领域规则。</summary>
    public async Task<RepositoryResult<ParcelProcessingWriteResponse>> AppendAsync(ParcelProcessingRecordRequest request, CancellationToken cancellationToken) {
        try {
            var record = ParcelProcessingContractMapper.ToDomain(request, DateTime.Now);
            var result = await _repository.AppendAsync(record, cancellationToken);
            return result.IsSuccess && result.Value is not null
                ? RepositoryResult.Success<ParcelProcessingWriteResponse>(new() { ParcelId = result.Value.ParcelId?.ToString(System.Globalization.CultureInfo.InvariantCulture), IsDuplicate = result.Value.IsDuplicate, PartitionSuffix = result.Value.PartitionSuffix })
                : RepositoryResult.Fail<ParcelProcessingWriteResponse>(result.ErrorMessage ?? "保存处理记录失败。", result.ErrorCode ?? "ParcelProcessingWriteFailed");
        }
        catch (Exception ex) { Logger.Error(ex, "处理事实追加失败，RecordId={RecordId}", request.RecordId); throw; }
    }

    /// <summary>检索最近未关联包裹的事实，限制数量，返回真实来源数据。</summary>
    public async Task<IReadOnlyList<ParcelProcessingRecordResponse>> GetUnboundAsync(int limit, CancellationToken cancellationToken) {
        try {
            if (limit is < 1 or > 200) throw new ArgumentOutOfRangeException(nameof(limit), "返回条数必须为1至200。");
            return (await _repository.GetUnboundAsync(limit, cancellationToken)).Select(ParcelProcessingContractMapper.ToResponse).ToArray();
        }
        catch (Exception ex) { Logger.Error(ex, "查询未关联处理事实失败，Limit={Limit}", limit); throw; }
    }
}
