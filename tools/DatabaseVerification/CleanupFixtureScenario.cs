using Microsoft.EntityFrameworkCore;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Infrastructure.Persistence;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Tools.DatabaseVerification;

/// <summary>专属验收数据库的两票过期样本；若存在其他过期数据则拒绝创建，避免扩大清理范围。</summary>
internal static class CleanupFixtureScenario {
    /// <summary>明确早于所有当前联调样本的清理截止时间。</summary>
    internal static readonly DateTime Cutoff = new(2021, 1, 1);

    /// <summary>通过 EF 和领域工厂构造根表旧数据，复核正式清理对旧根表的兼容性。</summary>
    internal static async Task<object> SeedAsync(IDbContextFactory<SortingHubDbContext> factory, ParcelPartitionStore partitions) {
        var catalog = await partitions.GetReadCatalogAsync(default);
        foreach (var suffix in catalog.Periods.Select(period => period.Suffix).Append(string.Empty).Distinct()) {
            await using var shard = await partitions.CreateContextAsync(suffix, default);
            if (await shard.Set<Parcel>().AnyAsync(parcel => parcel.CreatedTime < Cutoff))
                throw new InvalidOperationException("专属环境已存在其他过期数据，拒绝扩大本轮清理范围。");
        }
        await using var db = await factory.CreateDbContextAsync();
        var at = new DateTime(2020, 1, 1, 10, 0, 0);
        var run = Guid.NewGuid().ToString("N");
        var firstId = DateTime.Now.Ticks;
        var ids = new[] { firstId, firstId + 1 };
        foreach (var id in ids) {
            var record = new ParcelProcessingRecord {
                Key = Guid.NewGuid().ToString("N"), RecordId = Guid.NewGuid().ToString("N"), ParcelId = id,
                SourceInstanceId = "joint-cleanup-fixture", SourceRunId = run, SourceParcelId = id,
                PartitionTime = at, OccurredAt = at, Stage = ParcelProcessingStage.Detected,
                PayloadHash = "joint-cleanup-fixture", IsSuccess = true, Barcode = "JOINT-CLEANUP-" + run,
                WorkstationName = "隔离清理验收", RawPayload = new string('x', 4096)
            };
            var parcel = Parcel.CreateDetected(id, record, at);
            parcel.ApplyProcessingRecords([record]);
            db.Add(parcel); db.Add(record);
        }
        await db.SaveChangesAsync();
        return new { ids = ids.Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(), createdBefore = "2021-01-01", count = ids.Length };
    }
}
