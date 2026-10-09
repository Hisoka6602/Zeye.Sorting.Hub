using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Fusion;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;

/// <summary>全局接收凭据、连接租约和图片断点的提供器无关映射。</summary>
public sealed class FusionEntityTypeConfiguration :
    IEntityTypeConfiguration<FusionSourceLease>, IEntityTypeConfiguration<FusionJournalHeartbeat>,
    IEntityTypeConfiguration<FusionFactReceipt>, IEntityTypeConfiguration<FusionImageUpload> {
    /// <summary>协议来源和图片编号使用提供器的二进制排序规则，SQLite 默认比较已区分大小写。</summary>
    public static void ConfigureIdentities(ModelBuilder model, string? provider) {
        var collation = provider == Persistence.DbProviderNames.SqlServer ? "Latin1_General_100_BIN2"
            : provider == Persistence.DbProviderNames.MySql ? "utf8mb4_bin" : null;
        if (collation is null) return;
        foreach (var entity in model.Model.GetEntityTypes().Where(x => x.ClrType.Namespace == typeof(FusionSourceLease).Namespace)) {
            foreach (var property in entity.GetProperties().Where(x => x.Name is "SourceInstanceId" or "SourceImageId"))
                property.SetCollation(collation);
        }
    }
    /// <summary>单来源租约用版本令牌保护多个 Hub 进程的注册竞争。</summary>
    public void Configure(EntityTypeBuilder<FusionSourceLease> builder) {
        builder.ToTable("FusionSourceLeases"); builder.HasKey(x => x.SourceInstanceId);
        builder.Property(x => x.SourceInstanceId).HasMaxLength(96); builder.Property(x => x.JournalId).HasMaxLength(32);
        builder.Property(x => x.ConnectionId).HasMaxLength(128); builder.Property(x => x.LeaseId).HasMaxLength(32);
        builder.Property(x => x.ServerId).HasMaxLength(32); builder.Property(x => x.Revision).IsConcurrencyToken();
    }
    /// <summary>舍弃指标按来源和发送数据库永久分开保存。</summary>
    public void Configure(EntityTypeBuilder<FusionJournalHeartbeat> builder) {
        builder.ToTable("FusionJournalHeartbeats"); builder.HasKey(x => x.Key); builder.Property(x => x.Key).HasMaxLength(64);
        builder.Property(x => x.SourceInstanceId).HasMaxLength(96); builder.Property(x => x.JournalId).HasMaxLength(32);
        builder.HasIndex(x => new { x.SourceInstanceId, x.JournalId }).IsUnique();
    }
    /// <summary>原始编号和序号分别唯一；投影状态与原文同事务提交。</summary>
    public void Configure(EntityTypeBuilder<FusionFactReceipt> builder) {
        builder.ToTable("FusionFactReceipts"); builder.HasKey(x => x.Key); builder.Property(x => x.Key).HasMaxLength(64);
        builder.Property(x => x.SourceInstanceId).HasMaxLength(96); builder.Property(x => x.JournalId).HasMaxLength(32);
        builder.Property(x => x.RecordId).HasMaxLength(32); builder.Property(x => x.BodySha256).HasMaxLength(64);
        builder.Property(x => x.Kind).HasMaxLength(64); builder.Property(x => x.TenantId).HasMaxLength(96);
        builder.Property(x => x.StoragePartitionId).HasMaxLength(96); builder.Property(x => x.ProjectionState).HasMaxLength(16);
        builder.Property(x => x.ProjectionClaimId).HasMaxLength(32); builder.Property(x => x.ProjectionError).HasMaxLength(128);
        builder.Property(x => x.ParcelId).HasMaxLength(32);
        builder.HasIndex(x => new { x.SourceInstanceId, x.JournalId, x.RecordId }).IsUnique();
        builder.HasIndex(x => new { x.SourceInstanceId, x.JournalId, x.SourceSequence }).IsUnique();
        builder.HasIndex(x => new { x.ProjectionState, x.NextProjectionAt });
        builder.HasIndex(x => new { x.ProjectionState, x.ReceivedAt, x.SourceSequence, x.NextProjectionAt, x.ProjectionClaimUntil })
            .HasDatabaseName("IX_FusionFacts_ProjectionQueue");
        builder.HasIndex(x => new { x.SourceInstanceId, x.ProjectionState, x.ProjectionError })
            .HasDatabaseName("IX_FusionFacts_SourceProgress");
    }
    /// <summary>图片唯一身份不依赖条码或内容摘要。</summary>
    public void Configure(EntityTypeBuilder<FusionImageUpload> builder) {
        builder.ToTable("FusionImageUploads"); builder.HasKey(x => x.Key); builder.Property(x => x.Key).HasMaxLength(64);
        builder.Property(x => x.SourceInstanceId).HasMaxLength(96); builder.Property(x => x.SourceImageId).HasMaxLength(128);
        builder.Property(x => x.UploadId).HasMaxLength(32); builder.Property(x => x.FileName).HasMaxLength(256);
        builder.Property(x => x.ContentType).HasMaxLength(64); builder.Property(x => x.ContentSha256).HasMaxLength(64);
        builder.Property(x => x.SourceRunId).HasMaxLength(32); builder.Property(x => x.CameraName).HasMaxLength(128);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasIndex(x => new { x.SourceInstanceId, x.SourceImageId }).IsUnique();
        builder.HasIndex(x => x.UploadId).IsUnique(); builder.HasIndex(x => new { x.IsStored, x.ModifiedAt });
    }
}
