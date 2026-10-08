namespace Zeye.Sorting.Hub.Application.Abstractions.Persistence;

/// <summary>后台补齐DWS耐久读模型；宿主不依赖EF或基础设施投影实现。</summary>
public interface IParcelDwsMeasurementBackfillService {
    /// <summary>处理一个有界幂等批次，返回实际扫描量；已补齐时返回零。</summary>
    Task<int> RunBatchAsync(CancellationToken cancellationToken);
}
