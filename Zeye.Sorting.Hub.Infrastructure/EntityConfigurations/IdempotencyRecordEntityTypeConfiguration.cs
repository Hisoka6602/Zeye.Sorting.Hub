using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Domain.Aggregates.Idempotency;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;

/// <summary>
/// 幂等记录 EF Core 映射。
/// </summary>
public sealed class IdempotencyRecordEntityTypeConfiguration : IEntityTypeConfiguration<IdempotencyRecord> {
    /// <summary>
    /// 配置幂等记录实体映射。
    /// </summary>
    /// <param name="builder">实体构建器。</param>
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder) {
        builder.ToTable("IdempotencyRecords");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.SourceSystem).HasMaxLength(IdempotencyRecord.MaxSourceSystemLength);
        builder.Property(x => x.OperationName).HasMaxLength(IdempotencyRecord.MaxOperationNameLength);
        builder.Property(x => x.BusinessKey).HasMaxLength(IdempotencyRecord.MaxBusinessKeyLength);
        builder.Property(x => x.PayloadHash).HasMaxLength(IdempotencyRecord.PayloadHashLength);
        builder.Property(x => x.FailureMessage).HasMaxLength(IdempotencyRecord.MaxFailureMessageLength);
        builder.Property(x => x.Status).HasConversion<int>();

        builder.HasIndex(x => new { x.SourceSystem, x.OperationName, x.BusinessKey, x.PayloadHash })
            .IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
