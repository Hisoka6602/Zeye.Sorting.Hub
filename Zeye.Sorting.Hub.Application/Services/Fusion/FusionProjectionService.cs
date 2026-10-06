using System.Collections.Concurrent;
using System.Globalization;
using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;
using Zeye.Sorting.Hub.Application.Abstractions.Integrations;
using Zeye.Sorting.Hub.Application.Services.Parcels;

namespace Zeye.Sorting.Hub.Application.Services.Fusion;

/// <summary>同一包裹有序批量投影，独立包裹以有界并行复用原子业务用例。</summary>
public sealed class FusionProjectionService(IFusionIngestionGateway ingress, ParcelProcessingApplicationService processing, int maximumParallelism = 32) {
    /// <summary>独立投影失败的审计日志。</summary>
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    /// <summary>保留来源三元组和同票事实顺序；全部业务事务完成后才标记各原始凭据。</summary>
    public async Task<int> ProjectAsync(CancellationToken cancellationToken) {
        if (maximumParallelism is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(maximumParallelism));
        var changed = 0;
        var results = new ConcurrentBag<(FusionProjectionItem Item, string? ParcelId, string? Error)>();
        var groups = (await ingress.ClaimProjectionsAsync(cancellationToken)).GroupBy(item =>
            (item.Request.SourceInstanceId, item.Request.SourceRunId,
             item.Request.SourceParcelId?.ToString(CultureInfo.InvariantCulture) ?? "unbound:" + item.Request.RecordId));
        await Parallel.ForEachAsync(groups, new ParallelOptions {
            MaxDegreeOfParallelism = maximumParallelism, CancellationToken = cancellationToken
        }, async (group, token) => {
            foreach (var chunk in group.Chunk(64)) {
                IReadOnlyList<RepositoryResult<ParcelProcessingWriteResponse>> outcomes;
                try {
                    outcomes = await processing.AppendBatchAsync(chunk.Select(item => item.Request).ToArray(), token);
                    if (outcomes.Count != chunk.Length) throw new InvalidOperationException("投影返回结果数量不匹配。");
                }
                catch (Exception exception) when (exception is not OperationCanceledException) {
                    // 一个输入验证错误不能影响同批其他有效事实；验证在事务前完成，逐条退化仍安全。
                    Logger.Error(exception, "Fusion 包裹批次验证失败，按独立事实恢复，Key={Key}", chunk[0].Key);
                    var independent = new List<RepositoryResult<ParcelProcessingWriteResponse>>(chunk.Length);
                    foreach (var item in chunk) {
                        try { independent.Add(await processing.AppendAsync(item.Request, token)); }
                        catch (Exception failure) when (failure is not OperationCanceledException) {
                            Logger.Error(failure, "Fusion 事实投影失败，Key={Key}", item.Key);
                            independent.Add(RepositoryResult<ParcelProcessingWriteResponse>.Fail("投影失败。", "ProjectionFailed"));
                        }
                    }
                    outcomes = independent;
                }
                for (var index = 0; index < chunk.Length; index++) {
                    var outcome = outcomes[index];
                    if (outcome.IsSuccess) Interlocked.Increment(ref changed);
                    results.Add((chunk[index], outcome.IsSuccess ? outcome.Value?.ParcelId : null,
                        outcome.IsSuccess ? null : outcome.ErrorCode ?? "ProjectionFailed"));
                }
            }
        });
        await ingress.FinishProjectionsAsync(results.ToArray(), cancellationToken);
        return changed;
    }
}
