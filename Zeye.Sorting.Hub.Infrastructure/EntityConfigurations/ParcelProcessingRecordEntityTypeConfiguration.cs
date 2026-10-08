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
        // 未关联列表先按 ParcelId 过滤，再按入库时间倒序及稳定主键正序读取少量候选。
        builder.HasIndex(x => new { x.ParcelId, x.RecordedAt, x.Key }).IsDescending(false, true, false);
        builder.HasIndex(x => x.OccurredAt);
        // 报表只读时间、结果、包裹归属和阶段，覆盖索引避免扫描原始报文所在的数据页。
        builder.HasIndex(x => new { x.OccurredAt, x.IsSuccess, x.ParcelId, x.Stage });
        // 阶段分析按首次入库锚点读取晚到事实；覆盖必要身份与端点，避免读取大报文数据页。
        builder.HasIndex(x => new { x.Stage, x.PartitionTime, x.ParcelId, x.OccurredAt,
            x.SourceInstanceId, x.SourceRunId, x.SourceParcelId, x.RecordId, x.IsSuccess, x.HasReliableTimestamp })
            .HasDatabaseName("IX_Processing_Stage_PartitionTime_Duration");
        // 来源筛选需作为等值前缀，避免数据分布变化后优化器改走身份索引并读取宽事实。
        builder.HasIndex(x => new { x.SourceInstanceId, x.Stage, x.PartitionTime, x.ParcelId, x.OccurredAt,
            x.SourceRunId, x.SourceParcelId, x.RecordId, x.IsSuccess, x.HasReliableTimestamp })
            .HasDatabaseName("IX_Processing_Source_Stage_Duration");
        builder.HasIndex(x => new { x.SourceInstanceId, x.SourceRunId, x.SourceParcelId });
        builder.HasIndex(x => new { x.MessageIdentity, x.ReceivedAt });
        foreach (var property in builder.Metadata.GetProperties().Where(x => x.ClrType == typeof(decimal?))) { property.SetPrecision(18); property.SetScale(3); }
    }
}
