using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;

/// <summary>追加式处理记录映射；允许未关联包裹的DWS记录独立入库。</summary>
public sealed class ParcelProcessingRecordEntityTypeConfiguration : IEntityTypeConfiguration<ParcelProcessingRecord> {
    /// <summary>配置处理记录主键、查询索引与量测精度。</summary>
    public void Configure(EntityTypeBuilder<ParcelProcessingRecord> builder) {
        builder.ToTable("Parcel_ProcessingRecords");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).ValueGeneratedNever();
        builder.HasIndex(x => new { x.ParcelId, x.OccurredAt });
        builder.HasIndex(x => x.OccurredAt);
        builder.HasIndex(x => new { x.SourceInstanceId, x.SourceRunId, x.SourceParcelId });
        builder.HasIndex(x => new { x.MessageIdentity, x.ReceivedAt });
        foreach (var property in builder.Metadata.GetProperties().Where(x => x.ClrType == typeof(decimal?))) { property.SetPrecision(18); property.SetScale(3); }
    }
}
