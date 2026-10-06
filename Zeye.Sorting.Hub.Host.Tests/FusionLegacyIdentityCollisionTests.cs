using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>提前查询基础表碰撞后仍禁止分表覆盖遗留包裹身份。</summary>
public sealed class FusionLegacyIdentityCollisionTests {
    /// <summary>遗留基础包裹与来源哈希同编号时完整拒绝，事实、凭据和分表均不写入。</summary>
    [Fact]
    public async Task LegacyBaseIdentityCollisionCannotOverwriteOrCreateReceipt() {
        await using var database = new RelationalParcelTestDatabase();
        await database.InitializeAsync();
        var record = new ParcelProcessingRecord { RecordId = "legacy-collision", SourceInstanceId = "fusion-sorter-01",
            SourceRunId = "counter-session-01", SourceParcelId = 21, Stage = ParcelProcessingStage.Detected,
            OccurredAt = new(2026, 9, 28, 10, 0, 0), RecordedAt = new(2026, 9, 28, 10, 0, 0),
            PartitionTime = new(2026, 9, 28, 10, 0, 0), PayloadHash = "original", IsSuccess = true };
        var sourceKey = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] {
            record.SourceInstanceId, record.SourceRunId, "21" }));
        var id = BinaryPrimitives.ReadInt64BigEndian(sourceKey) & long.MaxValue;
        if (id == 0) id = 1;
        await using (var db = await database.Factory.CreateDbContextAsync()) {
            db.Add(Parcel.CreateDetected(id, record with { SourceInstanceId = "legacy", Barcode = "LEGACY" }, record.RecordedAt));
            await db.SaveChangesAsync();
        }
        var result = await database.Processing.AppendAsync(record, default);
        Assert.False(result.IsSuccess);
        Assert.Equal("ParcelSourceConflict", result.ErrorCode);
        Assert.Equal(1, await database.CountPhysicalAsync("Parcels"));
        Assert.Equal(0, await database.CountPhysicalAsync("Parcels_202609"));
        Assert.Equal(0, await database.CountPhysicalAsync("Parcel_ProcessingRecords_202609"));
        Assert.Equal(0, await database.CountPhysicalAsync("ParcelProcessingReceipts"));
    }
}
