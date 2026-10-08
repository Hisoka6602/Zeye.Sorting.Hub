using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Tools.DatabaseVerification;

/// <summary>通过 EF 导出本轮来源的事实、聚合和图片证据，供 Fusion 原始发送库逐条对账。</summary>
internal static class FusionEvidenceExporter {
    /// <summary>只导出已授权的独立测试来源，不包含配置凭据。</summary>
    internal static async Task<string> WriteAsync(SortingHubDbContext database, SortingHubDbContext shard, string source, CancellationToken token) {
        var facts = await database.Set<FusionFactReceipt>().AsNoTracking().Where(fact => fact.SourceInstanceId == source).ToListAsync(token);
        var parcels = await shard.Set<Parcel>().AsNoTracking().Where(parcel => parcel.SourceInstanceId == source).AsSplitQuery().ToListAsync(token);
        var images = await database.Set<FusionImageUpload>().AsNoTracking().Where(image => image.SourceInstanceId == source).ToListAsync(token);
        var evidence = new {
            receipts = facts.Select(fact => new { fact.RecordId, sequence = fact.SourceSequence.ToString(), fact.BodySha256, fact.BodyJson, fact.Kind, fact.ProjectionState, fact.ProjectionError }),
            parcels = parcels.Select(parcel => new { id = parcel.Id.ToString(), parcel.SourceInstanceId, parcel.SourceRunId, sourceParcelId = parcel.SourceParcelId?.ToString(),
                parcel.BarCodes, parcel.Weight, parcel.Length, parcel.Width, parcel.Height, parcel.TargetChuteCode, parcel.ActualChuteCode,
                parcel.IsFallbackChuteAssigned, parcel.IsRoutingBlocked, parcel.HasImages, parcel.SourceExceptionCode,
                parcel.CreatedTime, parcel.DetectedTime, parcel.CompletedTime, status = parcel.Status.ToString() }),
            images = images.Select(image => new { image.Key, image.SourceImageId, image.SourceRunId, sourceParcelId = image.SourceParcelId?.ToString(), image.IsStored, image.ContentSha256, image.SizeBytes })
        };
        var path = Path.GetFullPath("data/business-history/verification-evidence.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(evidence, new JsonSerializerOptions(JsonSerializerDefaults.Web)), token);
        return path;
    }
}
