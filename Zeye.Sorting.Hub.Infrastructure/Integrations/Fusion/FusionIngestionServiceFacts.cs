using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>原文接收、独立编号去重与可恢复投影任务。</summary>
public sealed partial class FusionIngestionService {
    /// <summary>完整批次限额按 UTF-8 序列化字节计算，原文摘要始终使用收到的字符串。</summary>
    public Task<HubBatchReceipt> PublishAsync(string connectionId, HubFactBatch batch, CancellationToken cancellationToken) =>
        LockedAsync(batch.SourceInstanceId, async () => {
            var connection = Connection(connectionId, batch.SourceInstanceId, batch.JournalId, batch.LeaseId);
            if (batch.ProtocolVersion != "1.0" || !FusionProtocol.IsHex(batch.BatchId, 32) || batch.Records is null
                || batch.Records.Count is < 1 || batch.Records.Count > _options.MaxBatchRecords
                || JsonSerializer.SerializeToUtf8Bytes(batch, FusionProtocol.Json).Length > _options.MaxBatchBytes)
                throw new ArgumentException("InvalidBatchLimits");
            var receipts = new List<HubFactReceipt>(batch.Records.Count);
            foreach (var envelope in batch.Records) {
                try {
                    if (envelope is null) throw new ArgumentException("InvalidEnvelope");
                    receipts.Add(await StoreFactAsync(connection, envelope, cancellationToken));
                }
                catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or OverflowException) {
                    Logger.Warn(exception, "Fusion 事实输入被拒绝，Source={Source}", batch.SourceInstanceId);
                    receipts.Add(new(envelope?.RecordId ?? "", envelope?.SourceSequence ?? "", envelope?.BodySha256 ?? "", "rejected", "InvalidFact"));
                }
                catch (Exception exception) when (exception is not OperationCanceledException) {
                    Logger.Error(exception, "Fusion 事实尚未完成耐久确认，Source={Source}", batch.SourceInstanceId);
                    receipts.Add(new(envelope?.RecordId ?? "", envelope?.SourceSequence ?? "", envelope?.BodySha256 ?? "", "retryable", "PersistenceUnavailable"));
                }
            }
            return new HubBatchReceipt(_options.HubId, batch.SourceInstanceId, batch.JournalId, batch.BatchId, receipts);
        }, cancellationToken);

    /// <summary>独立事实和序号唯一键，接收原文与投影任务在同一事务提交。</summary>
    private async Task<HubFactReceipt> StoreFactAsync(FusionConnectionLease connection, HubFactEnvelope envelope, CancellationToken cancellationToken) {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var sequence = FusionProtocol.ValidateEnvelope(envelope);
        var sourceFacts = db.Set<FusionFactReceipt>().AsNoTracking().Where(x => x.SourceInstanceId == connection.Source.SourceInstanceId
            && x.JournalId == connection.JournalId);
        // 分别按编号和来源序号走唯一索引，避免每次写入扫描整个来源日志。
        // UNION 合并同一记录的双重命中，同时保留两条不同记录的身份冲突。
        var previous = await sourceFacts.Where(x => x.RecordId == envelope.RecordId)
            .Union(sourceFacts.Where(x => x.SourceSequence == sequence)).ToListAsync(cancellationToken);
        if (previous.Count > 0) return Receipt(previous, envelope);
        var fact = FusionProtocol.Decode(envelope, connection.Source.SourceInstanceId, connection.JournalId, connection.Source);
        var request = FusionProtocol.Map(fact, connection.Source);
        // 步骤1：原文先进入耐久接收簿；同一行保存恢复投影状态，不能只在内存排队后确认。
        var stored = new FusionFactReceipt {
            Key = FusionProtocol.Key(fact.SourceInstanceId, fact.JournalId, fact.RecordId),
            SourceInstanceId = fact.SourceInstanceId, JournalId = fact.JournalId, RecordId = fact.RecordId, SourceSequence = sequence,
            BodyJson = envelope.BodyJson, BodySha256 = envelope.BodySha256, Kind = fact.Kind,
            ReceivedAt = DateTime.Now, OccurredAt = FusionProtocol.Local(fact.OccurredAtUtc, connection.Source.TimeZoneId),
            TenantId = connection.Source.TenantId, StoragePartitionId = connection.Source.StoragePartitionId,
            ProjectionState = request is not null ? "pending" : "complete", NextProjectionAt = DateTime.Now,
            ProjectionJson = request is null ? null : JsonSerializer.Serialize(request, FusionProtocol.Json)
        };
        db.Add(stored);
        // 步骤2：明确关联与上传描述可先后到达；仅保存明确来源三元组，绝不按条码猜测。
        if (fact.Kind is "image.association" && FusionProtocol.Boolean(fact.Data, "associationConfirmed") == true
            && fact.SourceParcelId is not null && (FusionProtocol.Decimal(fact.Data, "candidateCount") ?? 1) <= 1) {
            var imageId = FusionProtocol.Text(fact.Data, "sourceImageId", 128) ?? throw new ArgumentException("MissingImageIdentity");
            var key = FusionProtocol.Key(fact.SourceInstanceId, imageId);
            var image = await db.Set<FusionImageUpload>().AsTracking().SingleOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (image is null) { image = new() { Key = key, SourceInstanceId = fact.SourceInstanceId, SourceImageId = imageId, ModifiedAt = DateTime.Now }; db.Add(image); }
            var parcelId = FusionProtocol.Number(fact.SourceParcelId);
            if (image.SourceRunId is not null && (image.SourceRunId != fact.SourceRunId || image.SourceParcelId != parcelId))
                return new(envelope.RecordId, envelope.SourceSequence, envelope.BodySha256, "conflict", "ImageAssociationConflict");
            image.SourceRunId = fact.SourceRunId; image.SourceParcelId = parcelId;
            image.CameraName = FusionProtocol.Text(fact.Data, "cameraName", 128); image.Revision++;
        }
        await db.SaveChangesAsync(cancellationToken);
        return new(envelope.RecordId, envelope.SourceSequence, envelope.BodySha256, "stored");
    }

    /// <summary>同编号、同序号和同原始字节摘要才是重复，任意身份内容变更都冲突。</summary>
    private static HubFactReceipt Receipt(IReadOnlyList<FusionFactReceipt> previous, HubFactEnvelope envelope) {
        var identical = previous.Count == 1 && previous[0].RecordId == envelope.RecordId
            && previous[0].SourceSequence == FusionProtocol.Number(envelope.SourceSequence, true) && previous[0].BodySha256 == envelope.BodySha256;
        return new(envelope.RecordId, envelope.SourceSequence, envelope.BodySha256, identical ? "duplicate" : "conflict",
            identical ? null : "ImmutableFactConflict");
    }

    /// <summary>原子认领有界任务；失败和进程中断只延迟重试，不删除已经确认的原文。</summary>
    public async Task<IReadOnlyList<FusionProjectionItem>> ClaimProjectionsAsync(CancellationToken cancellationToken) {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var now = DateTime.Now;
        // 候选排序仅读取覆盖索引中的凭据键，避免原文大字段参与海量排序。
        var keys = await db.Set<FusionFactReceipt>().AsNoTracking().Where(x => x.ProjectionState != "complete" && x.NextProjectionAt <= now
            && (x.ProjectionClaimUntil == null || x.ProjectionClaimUntil <= now))
            .OrderBy(x => x.ReceivedAt).ThenBy(x => x.SourceSequence).ThenBy(x => x.Key)
            .Select(x => x.Key).Take(50).ToArrayAsync(cancellationToken);
        if (keys.Length == 0) return [];
        var claim = Guid.NewGuid().ToString("N");
        // 一次提交认领有界候选集；条件复核仍在数据库中原子执行。
        var acquired = await db.Set<FusionFactReceipt>().Where(x => keys.Contains(x.Key) && x.ProjectionState != "complete"
            && x.NextProjectionAt <= now && (x.ProjectionClaimUntil == null || x.ProjectionClaimUntil <= now))
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.ProjectionClaimId, claim)
                .SetProperty(x => x.ProjectionClaimUntil, now.AddMinutes(2))
                .SetProperty(x => x.ProjectionAttempts, x => x.ProjectionAttempts + 1), cancellationToken);
        if (acquired == 0) return [];
        // 原文用例在成功认领后按主键读取，最多50条，并恢复相同队列顺序。
        var rows = await db.Set<FusionFactReceipt>().AsNoTracking()
            .Where(x => keys.Contains(x.Key) && x.ProjectionClaimId == claim)
            .OrderBy(x => x.ReceivedAt).ThenBy(x => x.SourceSequence).ThenBy(x => x.Key)
            .Select(x => new { x.Key, x.ProjectionJson }).ToListAsync(cancellationToken);
        var items = new List<FusionProjectionItem>();
        foreach (var row in rows) {
            var request = JsonSerializer.Deserialize<ParcelProcessingRecordRequest>(row.ProjectionJson!, FusionProtocol.Json);
            if (request is not null) items.Add(new(row.Key, claim, request));
        }
        return items;
    }

    /// <summary>幂等业务用例完成后才标记投影完成；崩溃在两者之间会安全重放同一业务凭据。</summary>
    public async Task FinishProjectionAsync(FusionProjectionItem item, string? parcelId, string? error, CancellationToken cancellationToken) {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await db.Set<FusionFactReceipt>().Where(x => x.Key == item.Key && x.ProjectionClaimId == item.ClaimId)
            .ExecuteUpdateAsync(p => p.SetProperty(x => x.ProjectionState, error == null ? "complete" : "retry")
                .SetProperty(x => x.ParcelId, parcelId).SetProperty(x => x.ProjectionError, error)
                .SetProperty(x => x.NextProjectionAt, DateTime.Now.AddSeconds(error == null ? 0 : 15))
                .SetProperty(x => x.ProjectionClaimId, (string?)null).SetProperty(x => x.ProjectionClaimUntil, (DateTime?)null), cancellationToken);
    }

    /// <summary>有界查询原始接收证据，包括没有业务阶段映射的会话结束、计数周期及存储事实。</summary>
    public async Task<IReadOnlyList<FusionFactInspection>> GetFactsAsync(string source, string? journal, int limit, CancellationToken cancellationToken) {
        if (!FusionProtocol.IsIdentity(source) || journal is not null && !FusionProtocol.IsHex(journal, 32) || limit is < 1 or > 200)
            throw new ArgumentException("InvalidFactQuery");
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<FusionFactReceipt>().AsNoTracking().Where(x => x.SourceInstanceId == source && (journal == null || x.JournalId == journal))
            .OrderByDescending(x => x.ReceivedAt).Take(limit).ToListAsync(cancellationToken);
        return rows.Select(x => new FusionFactInspection(x.SourceInstanceId, x.JournalId, x.RecordId,
            x.SourceSequence.ToString(CultureInfo.InvariantCulture), x.BodySha256, x.Kind, x.BodyJson, x.ReceivedAt,
            x.ProjectionState, x.ProjectionError, x.ParcelId)).ToArray();
    }
}
