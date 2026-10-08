using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Infrastructure.Queries;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;

/// <summary>DWS分析独立窄表，避免量测查询扫描包含大报文的原始事实数据页。</summary>
public sealed class ParcelDwsMeasurementSnapshotEntityTypeConfiguration : IEntityTypeConfiguration<ParcelDwsMeasurementSnapshot> {
    /// <summary>沿用事实身份长度与量测精度；表按既有Parcel_规则自动分区。</summary>
    public void Configure(EntityTypeBuilder<ParcelDwsMeasurementSnapshot> builder) {
        builder.ToTable("Parcel_DwsMeasurements");
        builder.HasKey(row => row.Key);
        builder.Property(row => row.Key).HasMaxLength(64).ValueGeneratedNever();
        builder.Property(row => row.RecordId).HasMaxLength(128);
        builder.Property(row => row.SourceInstanceId).HasMaxLength(96);
        builder.Property(row => row.SourceRunId).HasMaxLength(96);
        builder.Property(row => row.WorkstationName).HasMaxLength(128);
        builder.Property(row => row.MessageIdentity).HasMaxLength(256);
        builder.Property(row => row.Barcode).HasMaxLength(1024);
        foreach (var property in builder.Metadata.GetProperties().Where(property => property.ClrType == typeof(decimal?))) {
            property.SetPrecision(18); property.SetScale(3);
        }
        builder.HasIndex(row => new { row.PartitionTime, row.Stage });
        builder.HasIndex(row => new { row.SourceInstanceId, row.PartitionTime, row.Stage });
        builder.HasIndex(row => new { row.ParcelId, row.Stage });
        // 时间窗口的阶段与成功筛选仅访问索引，避免探测边界时逐行回表。
        builder.HasIndex(row => new { row.PartitionTime, row.Stage, row.IsSuccess }).HasDatabaseName("IX_Dws_Time_Stage_Success");
    }
}
