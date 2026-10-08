using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;

/// <summary>调用样本与包裹共用自动分表，只索引统计所需标量。</summary>
public sealed class ParcelDurationCallEntityTypeConfiguration : IEntityTypeConfiguration<ParcelDurationCall> {
    /// <summary>配置耐久调用投影、查询索引与精确毫秒精度。</summary>
    public void Configure(EntityTypeBuilder<ParcelDurationCall> builder) {
        builder.ToTable("Parcel_DurationCalls");
        builder.HasKey(row => row.Key);
        builder.Property(row => row.Key).HasMaxLength(64).ValueGeneratedNever();
        builder.Property(row => row.SampleKey).HasMaxLength(160);
        builder.Property(row => row.SourceInstanceId).HasMaxLength(96);
        builder.Property(row => row.SourceRunId).HasMaxLength(96);
        builder.Property(row => row.Type).HasMaxLength(32);
        builder.Property(row => row.TimingSource).HasMaxLength(32);
        builder.Property(row => row.Provider).HasMaxLength(512);
        builder.Property(row => row.RequestUrl).HasMaxLength(512);
        builder.Property(row => row.Milliseconds).HasPrecision(20, 4);
        builder.HasIndex(row => new { row.PartitionTime, row.ParcelId });
        builder.HasIndex(row => new { row.Type, row.PartitionTime, row.Milliseconds });
        builder.HasIndex(row => new { row.SourceInstanceId, row.PartitionTime, row.ParcelId });
        builder.HasIndex(row => row.ParcelId);
    }
}
