using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

/// <summary>后台分批补齐历史接口耗时索引；页面请求不建表、不写库、不补齐历史。</summary>
public sealed class ParcelDurationBackfillService(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions) {
    /// <summary>有界批次低于 Oracle IN 和 SQL Server 参数上限，内存不随历史总量增加。</summary>
    internal const int BatchSize = 512;
    /// <summary>新旧协议的接口及操作诊断阶段。</summary>
    private static readonly ParcelProcessingStage[] Stages = [ParcelProcessingStage.ScanUploaded, ParcelProcessingStage.LandingReported, ParcelProcessingStage.ImageUploaded];

    /// <summary>优先当前周期；返回本批扫描量，全部补齐时返回零。</summary>
    public async Task<int> RunBatchAsync(CancellationToken cancellationToken) {
        var catalog = await partitions.GetReadCatalogAsync(cancellationToken);
        await using var template = await factory.CreateDbContextAsync(cancellationToken);
        var completed = (await template.Set<ParcelDurationBackfillState>().AsNoTracking().Where(row => row.Completed)
            .Select(row => row.Suffix).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var suffixes = catalog.Periods.OrderByDescending(period => period.Start).Select(period => period.Suffix).Append(string.Empty);
        foreach (var suffix in suffixes) {
            if (completed.Contains(suffix)) continue;
            var strategy = template.Database.CreateExecutionStrategy();
            try {
                return await strategy.ExecuteAsync(async () => {
                    await using var db = await partitions.CreateContextAsync(suffix, cancellationToken);
                    await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                    var state = await db.Set<ParcelDurationBackfillState>().AsTracking().SingleOrDefaultAsync(row => row.Suffix == suffix, cancellationToken);
                    if (state?.Completed == true) return 0;
                    if (state is null) { state = new() { Suffix = suffix }; db.Add(state); }
                    var records = await BuildBatch(db.Set<ParcelProcessingRecord>().AsNoTracking(), state.Cursor).ToListAsync(cancellationToken);
                    if (records.Count > 0) {
                        var keys = records.Select(row => row.Key).ToArray();
                        var existing = (await db.Set<ParcelDurationFact>().Where(row => keys.Contains(row.Key)).Select(row => row.Key)
                            .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
                        db.AddRange(records.Where(row => !existing.Contains(row.Key)).Select(ParcelDurationFact.Create));
                        state.Cursor = records[^1].Key;
                    }
                    state.Completed = records.Count < BatchSize;
                    state.Revision++;
                    state.UpdatedAt = DateTime.Now;
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return records.Count;
                });
            }
            catch (DbUpdateConcurrencyException) { return 0; }
            catch (DbUpdateException exception) when (DuplicateKeyExceptionDetector.IsDuplicateKeyException(exception)) { return 0; }
        }
        return 0;
    }

    /// <summary>按主键继续而非 OFFSET；一次只搬运有界分类前部，不加载请求或响应正文。</summary>
    internal static IQueryable<ParcelProcessingRecord> BuildBatch(IQueryable<ParcelProcessingRecord> facts, string cursor) {
        facts = facts.Where(row => Stages.Contains(row.Stage));
        if (cursor.Length > 0) facts = facts.Where(row => DatabaseTextFunctions.IsAfter(row.Key, cursor));
        return facts.OrderBy(row => row.Key).Take(BatchSize).Select(row => new ParcelProcessingRecord {
            Key = row.Key, RecordId = row.RecordId, ParcelId = row.ParcelId, SourceInstanceId = row.SourceInstanceId,
            SourceRunId = row.SourceRunId, SourceParcelId = row.SourceParcelId, PartitionTime = row.PartitionTime,
            OccurredAt = row.OccurredAt, Stage = row.Stage, HasReliableTimestamp = row.HasReliableTimestamp,
            RequestAt = row.RequestAt, ResponseAt = row.ResponseAt, ElapsedMilliseconds = row.ElapsedMilliseconds,
            AttemptNumber = row.AttemptNumber, Provider = row.Provider, RequestUrl = row.RequestUrl, IsSuccess = row.IsSuccess,
            ErrorMessage = row.ErrorMessage, RawPayload = row.RawPayload == null ? null : row.RawPayload.Substring(0,
                row.ErrorMessage != null && row.ErrorMessage.StartsWith("{\"operationId\"") ? 512 : 2048)
        });
    }
}
