using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Contracts.Models.Fusion;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Infrastructure.Integrations.Fusion;

/// <summary>同一有界批次只提交一次耐久事务，确认仍位于提交之后。</summary>
public sealed partial class FusionIngestionService {
    /// <summary>预读唯一身份，隔离错误输入，提交全部有效事实后返回逐条确认。</summary>
    private async Task<HubBatchReceipt> StoreFactBatchAsync(FusionConnectionLease connection, HubFactBatch batch, CancellationToken token) {
        var receipts = new HubFactReceipt?[batch.Records.Count];
        var valid = new List<(int Index, HubFactEnvelope Envelope, long Sequence)>();
        var staged = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < batch.Records.Count; index++) {
            var envelope = batch.Records[index];
            try {
                if (envelope is null) throw new ArgumentException("InvalidEnvelope");
                valid.Add((index, envelope, FusionProtocol.ValidateEnvelope(envelope)));
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or OverflowException) {
                Logger.Warn(exception, "Fusion 批次信封无效，Source={Source}, Record={Record}", batch.SourceInstanceId, envelope?.RecordId);
                receipts[index] = new(envelope?.RecordId ?? "", envelope?.SourceSequence ?? "", envelope?.BodySha256 ?? "", "rejected", "InvalidFact");
            }
        }
        try {
            if (valid.Count > 0) {
                await using var db = await _factory.CreateDbContextAsync(token);
                var ids = valid.Select(x => x.Envelope.RecordId).Distinct().ToArray();
                var sequences = valid.Select(x => x.Sequence).Distinct().ToArray();
                var source = db.Set<FusionFactReceipt>().AsNoTracking().Where(x =>
                    x.SourceInstanceId == connection.Source.SourceInstanceId && x.JournalId == connection.JournalId);
                var existing = await ReadExistingFactsAsync(source, x => ids.Contains(x.RecordId), x => sequences.Contains(x.SourceSequence), token);
                var byId = existing.ToDictionary(x => x.RecordId, StringComparer.Ordinal);
                var bySequence = existing.ToDictionary(x => x.SourceSequence);
                var images = new Dictionary<string, FusionImageUpload>(StringComparer.Ordinal);
                foreach (var (index, envelope, sequence) in valid) {
                    try {
                        var matches = new List<FusionFactReceipt>(2);
                        if (byId.TryGetValue(envelope.RecordId, out var sameId)) matches.Add(sameId);
                        if (bySequence.TryGetValue(sequence, out var sameSequence) && !matches.Contains(sameSequence)) matches.Add(sameSequence);
                        if (matches.Count > 0) { receipts[index] = Receipt(matches, envelope); continue; }
                        var fact = FusionProtocol.Decode(envelope, connection.Source.SourceInstanceId, connection.JournalId, connection.Source);
                        var request = FusionProtocol.Map(fact, connection.Source);
                        FusionImageUpload? image = null;
                        var newImage = false;
                        if (fact.Kind == "image.association" && FusionProtocol.Boolean(fact.Data, "associationConfirmed") == true
                            && fact.SourceParcelId is not null && (FusionProtocol.ReadDecimal(fact.Data, "candidateCount") ?? 1) <= 1) {
                            var imageId = FusionProtocol.Text(fact.Data, "sourceImageId", 128) ?? throw new ArgumentException("MissingImageIdentity");
                            var imageKey = FusionProtocol.Key(fact.SourceInstanceId, imageId);
                            if (!images.TryGetValue(imageKey, out image)) {
                                image = await db.Set<FusionImageUpload>().AsTracking().SingleOrDefaultAsync(x => x.Key == imageKey, token);
                                newImage = image is null;
                                image ??= new() { Key = imageKey, SourceInstanceId = fact.SourceInstanceId, SourceImageId = imageId, ModifiedAt = DateTime.Now };
                            }
                            var parcelId = FusionProtocol.Number(fact.SourceParcelId);
                            if (image.SourceRunId is not null && (image.SourceRunId != fact.SourceRunId || image.SourceParcelId != parcelId)) {
                                receipts[index] = new(envelope.RecordId, envelope.SourceSequence, envelope.BodySha256, "conflict", "ImageAssociationConflict");
                                continue;
                            }
                            var camera = FusionProtocol.Text(fact.Data, "cameraName", 128);
                            image.SourceRunId = fact.SourceRunId; image.SourceParcelId = parcelId; image.CameraName = camera; image.Revision++;
                            images[imageKey] = image;
                        }
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
                        if (newImage) db.Add(image!);
                        byId[stored.RecordId] = stored; bySequence[stored.SourceSequence] = stored;
                        staged.Add(stored.RecordId);
                        receipts[index] = new(envelope.RecordId, envelope.SourceSequence, envelope.BodySha256, "stored");
                    }
                    catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or OverflowException) {
                        Logger.Warn(exception, "Fusion 批次事实无效，Source={Source}, Record={Record}", batch.SourceInstanceId, envelope.RecordId);
                        receipts[index] = new(envelope.RecordId, envelope.SourceSequence, envelope.BodySha256, "rejected", "InvalidFact");
                    }
                }
                if (staged.Count > 0) {
                    var strategy = db.Database.CreateExecutionStrategy();
                    await strategy.ExecuteAsync(async () => {
                        await using var transaction = await db.Database.BeginTransactionAsync(token);
                        // Keep Added/Modified state until commit so transient failures can
                        // replay this same immutable batch inside the provider strategy.
                        await db.SaveChangesAsync(acceptAllChangesOnSuccess: false, token);
                        await transaction.CommitAsync(token);
                    });
                    db.ChangeTracker.AcceptAllChanges();
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException) {
            Logger.Error(exception, "Fusion 有界批次未完成耐久提交，Source={Source}", batch.SourceInstanceId);
            foreach (var (index, envelope, _) in valid) {
                var reply = receipts[index];
                if (reply is null || reply.Status == "stored" || reply.Status == "duplicate" && staged.Contains(envelope.RecordId))
                    receipts[index] = new(envelope.RecordId, envelope.SourceSequence, envelope.BodySha256, "retryable", "PersistenceUnavailable");
            }
        }
        return new(_options.HubId, batch.SourceInstanceId, batch.JournalId, batch.BatchId, receipts.Select(x => x!).ToArray());
    }
}
