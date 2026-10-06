using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>有界提交投影状态，减少逐条落盘；包裹用例已独立耐久保存。</summary>
public sealed partial class FusionIngestionService {
    /// <summary>使独立包裹并行有效的有界领取规模，同时低于 SQL Server 参数上限。</summary>
    private const int ProjectionBatchSize = 512;
    /// <summary>同一认领中的成功结果一次提交；失败结果保留独立错误与重试计划。</summary>
    public async Task FinishProjectionsAsync(
        IReadOnlyList<(FusionProjectionItem Item, string? ParcelId, string? Error)> results, CancellationToken cancellationToken) {
        if (results.Count > ProjectionBatchSize) throw new ArgumentException("投影结果批次不能超过512条。", nameof(results));
        if (results.Select(result => result.Item.Key).Distinct(StringComparer.Ordinal).Count() != results.Count)
            throw new ArgumentException("投影结果不能包含重复凭据。", nameof(results));
        foreach (var group in results.GroupBy(result => result.Item.ClaimId, StringComparer.Ordinal)) {
            // SQL Server 最多允许10层CASE，单次映射限制为8条；全空归属直接写NULL。
            foreach (var succeeded in group.Where(result => result.Error is null).Chunk(8)) {
                var keys = succeeded.Select(result => result.Item.Key).ToArray();
                var row = Expression.Parameter(typeof(FusionFactReceipt), "row");
                Expression value = Expression.Constant(null, typeof(string));
                foreach (var result in succeeded.Where(result => result.ParcelId is not null).Reverse())
                    value = Expression.Condition(Expression.Equal(Expression.Property(row, nameof(FusionFactReceipt.Key)),
                        Expression.Constant(result.Item.Key)), Expression.Constant(result.ParcelId, typeof(string)), value);
                var parcelIds = Expression.Lambda<Func<FusionFactReceipt, string?>>(value, row);
                var claim = group.Key;
                Expression<Func<SetPropertyCalls<FusionFactReceipt>, SetPropertyCalls<FusionFactReceipt>>> updates = p => p
                    .SetProperty(x => x.ProjectionState, "complete").SetProperty(x => x.ProjectionError, (string?)null)
                    .SetProperty(x => x.NextProjectionAt, DateTime.Now)
                    .SetProperty(x => x.ProjectionClaimId, (string?)null).SetProperty(x => x.ProjectionClaimUntil, (DateTime?)null);
                Expression<Func<SetPropertyCalls<FusionFactReceipt>, SetPropertyCalls<FusionFactReceipt>>> shape = p => p.SetProperty(x => x.ParcelId, x => x.ParcelId);
                var setter = (MethodCallExpression)shape.Body;
                var mapped = Expression.Call(updates.Body, setter.Method, setter.Arguments[0], parcelIds);
                updates = Expression.Lambda<Func<SetPropertyCalls<FusionFactReceipt>, SetPropertyCalls<FusionFactReceipt>>>(mapped, updates.Parameters);
                await using var db = await _factory.CreateDbContextAsync(cancellationToken);
                await db.Set<FusionFactReceipt>().Where(x => keys.Contains(x.Key) && x.ProjectionClaimId == claim)
                    .ExecuteUpdateAsync(updates, cancellationToken);
            }
            foreach (var result in group.Where(result => result.Error is not null))
                await FinishProjectionAsync(result.Item, result.ParcelId, result.Error, cancellationToken);
        }
    }
}
