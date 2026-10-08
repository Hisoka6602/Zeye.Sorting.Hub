using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;

/// <summary>接口耗时窄表随包裹自动分表，历史补齐进度保持全局。</summary>
public sealed class ParcelDurationFactEntityTypeConfiguration : IEntityTypeConfiguration<ParcelDurationFact> {
    /// <summary>只映射有界标量列，拒绝查询长报文造成的宽表扫描。</summary>
    public void Configure(EntityTypeBuilder<ParcelDurationFact> builder) {
        builder.ToTable("Parcel_DurationFacts");
        builder.HasKey(row => row.Key);
        builder.Property(row => row.Key).HasMaxLength(64).ValueGeneratedNever();
        builder.Property(row => row.RecordId).HasMaxLength(128);
        builder.Property(row => row.SourceInstanceId).HasMaxLength(96);
        builder.Property(row => row.SourceRunId).HasMaxLength(96);
        builder.Property(row => row.Type).HasMaxLength(32);
        builder.Property(row => row.OperationId).HasMaxLength(128);
        builder.Property(row => row.AttemptId).HasMaxLength(128);
        builder.Property(row => row.Outcome).HasMaxLength(128);
        builder.Property(row => row.Provider).HasMaxLength(512);
        builder.Property(row => row.RequestUrl).HasMaxLength(512);
        builder.HasIndex(row => new { row.PartitionTime, row.ParcelId });
        builder.HasIndex(row => new { row.SourceInstanceId, row.PartitionTime, row.ParcelId });
        builder.HasIndex(row => new { row.Projected, row.ParcelId });
        builder.HasIndex(row => row.ParcelId);
    }
}
