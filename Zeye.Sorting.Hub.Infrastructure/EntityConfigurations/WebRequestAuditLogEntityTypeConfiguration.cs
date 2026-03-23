using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations {

    /// <summary>
    /// WebRequestAuditLog 热数据主表映射。
    /// </summary>
    public sealed class WebRequestAuditLogEntityTypeConfiguration : IEntityTypeConfiguration<WebRequestAuditLog> {
        /// <summary>
        /// 执行热表字段与索引映射配置。
        /// </summary>
        /// <param name="builder">实体映射构建器。</param>
        public void Configure(EntityTypeBuilder<WebRequestAuditLog> builder) {
            builder.ToTable("WebRequestAuditLogs");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TraceId).HasMaxLength(128);
            builder.Property(x => x.CorrelationId).HasMaxLength(128);
            builder.Property(x => x.SpanId).HasMaxLength(64);
            builder.Property(x => x.OperationName).HasMaxLength(256);
            builder.Property(x => x.RequestMethod).HasMaxLength(16);
            builder.Property(x => x.RequestScheme).HasMaxLength(16);
            builder.Property(x => x.RequestHost).HasMaxLength(256);
            builder.Property(x => x.RequestPath).HasMaxLength(512);
            builder.Property(x => x.RequestRouteTemplate).HasMaxLength(512);
            builder.Property(x => x.UserName).HasMaxLength(128);
            builder.Property(x => x.ResourceId).HasMaxLength(128);

            // 写优化索引：围绕时间序列、追踪链路与高频筛选组合建立索引。
            builder.HasIndex(x => x.StartedAt).HasDatabaseName(WebRequestAuditLogIndexNames.StartedAt);
            builder.HasIndex(x => new { x.StatusCode, x.StartedAt }).HasDatabaseName(WebRequestAuditLogIndexNames.StatusCodeStartedAt);
            builder.HasIndex(x => new { x.IsSuccess, x.StartedAt }).HasDatabaseName(WebRequestAuditLogIndexNames.IsSuccessStartedAt);
            builder.HasIndex(x => new { x.OperationName, x.StartedAt }).HasDatabaseName(WebRequestAuditLogIndexNames.OperationNameStartedAt);
            builder.HasIndex(x => new { x.RequestPath, x.StartedAt }).HasDatabaseName(WebRequestAuditLogIndexNames.RequestPathStartedAt);
            builder.HasIndex(x => x.TraceId).HasDatabaseName(WebRequestAuditLogIndexNames.TraceId);
            builder.HasIndex(x => x.CorrelationId).HasDatabaseName(WebRequestAuditLogIndexNames.CorrelationId);
            builder.HasIndex(x => new { x.UserId, x.StartedAt }).HasDatabaseName(WebRequestAuditLogIndexNames.UserIdStartedAt);
            builder.HasIndex(x => new { x.TenantId, x.StartedAt }).HasDatabaseName(WebRequestAuditLogIndexNames.TenantIdStartedAt);
            builder.HasIndex(x => new { x.AuditResourceType, x.ResourceId, x.StartedAt }).HasDatabaseName(WebRequestAuditLogIndexNames.AuditResourceTypeResourceIdStartedAt);

            builder.HasOne(x => x.Detail)
                .WithOne(x => x.WebRequestAuditLog)
                .HasForeignKey<WebRequestAuditLogDetail>(x => x.WebRequestAuditLogId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
