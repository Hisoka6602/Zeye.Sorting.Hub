using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;

/// <summary>
/// Outbox 消息 EF Core 映射。
/// </summary>
public sealed class OutboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<OutboxMessage> {
    /// <summary>
    /// 配置 Outbox 消息实体映射。
    /// </summary>
    /// <param name="builder">实体构建器。</param>
    public void Configure(EntityTypeBuilder<OutboxMessage> builder) {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.EventType).HasMaxLength(OutboxMessage.MaxEventTypeLength);
        builder.Property(x => x.PayloadJson).IsRequired();
        builder.Property(x => x.FailureMessage).HasMaxLength(OutboxMessage.MaxFailureMessageLength);
        builder.Property(x => x.Status).HasConversion<int>().IsConcurrencyToken();

        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.EventType, x.CreatedAt });
    }
}
