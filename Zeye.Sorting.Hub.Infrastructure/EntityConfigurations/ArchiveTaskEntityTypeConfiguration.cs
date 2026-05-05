using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Domain.Aggregates.DataGovernance;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;

/// <summary>
/// 归档任务实体映射配置。
/// </summary>
public sealed class ArchiveTaskEntityTypeConfiguration : IEntityTypeConfiguration<ArchiveTask> {
    /// <summary>
    /// 执行实体字段映射。
    /// </summary>
    /// <param name="builder">实体构建器。</param>
    public void Configure(EntityTypeBuilder<ArchiveTask> builder) {
        builder.ToTable("ArchiveTasks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedBy).HasMaxLength(64);
        builder.Property(x => x.Remark).HasMaxLength(512);
        builder.Property(x => x.PlanSummary).HasMaxLength(1024);
        builder.Property(x => x.FailureMessage).HasMaxLength(2048);
        builder.Property(x => x.CheckpointPayload);
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TaskType, x.CreatedAt });
    }
}
