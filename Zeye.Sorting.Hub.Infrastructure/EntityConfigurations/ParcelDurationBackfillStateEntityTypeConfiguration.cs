using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Infrastructure.Persistence.ReadModels;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;

/// <summary>耐久补齐游标避免重复读取历史长报文。</summary>
public sealed class ParcelDurationBackfillStateEntityTypeConfiguration : IEntityTypeConfiguration<ParcelDurationBackfillState> {
    /// <summary>配置进度唯一键和并发版本，进度表不随包裹分表。</summary>
    public void Configure(EntityTypeBuilder<ParcelDurationBackfillState> builder) {
        builder.ToTable("ParcelDurationBackfillStates");
        builder.HasKey(row => row.Suffix);
        builder.Property(row => row.Suffix).HasMaxLength(32);
        builder.Property(row => row.Cursor).HasMaxLength(64);
        builder.Property(row => row.DwsCursor).HasMaxLength(64);
        builder.Property(row => row.Revision).IsConcurrencyToken();
    }
}
