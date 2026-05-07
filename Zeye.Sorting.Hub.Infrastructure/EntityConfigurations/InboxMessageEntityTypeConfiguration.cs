using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Domain.Aggregates.Events;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;

/// <summary>
/// Inbox 消息 EF Core 映射。
/// </summary>
public sealed class InboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<InboxMessage> {
    /// <summary>
    /// 配置 Inbox 消息实体映射。
    /// </summary>
    /// <param name="builder">实体构建器。</param>
    public void Configure(EntityTypeBuilder<InboxMessage> builder) {
        builder.ToTable("InboxMessages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();
        builder.Property(x => x.SourceSystem).HasMaxLength(InboxMessage.MaxSourceSystemLength);
        builder.Property(x => x.MessageId).HasMaxLength(InboxMessage.MaxMessageIdLength);
        builder.Property(x => x.EventType).HasMaxLength(InboxMessage.MaxEventTypeLength);
        builder.Property(x => x.FailureMessage).HasMaxLength(InboxMessage.MaxFailureMessageLength);
        builder.Property(x => x.Status).HasConversion<int>();

        builder.HasIndex(x => new { x.SourceSystem, x.MessageId }).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.ExpiresAt, x.Status });
    }
}
