using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Domain.Aggregates.AuditLogs.WebRequests;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations {

    /// <summary>
    /// WebRequestAuditLog 冷数据详情表映射。
    /// </summary>
    public sealed class WebRequestAuditLogDetailEntityTypeConfiguration : IEntityTypeConfiguration<WebRequestAuditLogDetail> {
        /// <summary>
        /// 执行冷表字段映射配置。
        /// </summary>
        /// <param name="builder">实体映射构建器。</param>
        public void Configure(EntityTypeBuilder<WebRequestAuditLogDetail> builder) {
            builder.ToTable("WebRequestAuditLogDetails");
            builder.HasKey(x => x.WebRequestAuditLogId);

            // 冷表承载大文本字段，交由各 Provider 使用默认大文本映射，避免固定方言类型导致跨库迁移失败。
            builder.Property(x => x.RequestUrl);
            builder.Property(x => x.RequestQueryString);
            builder.Property(x => x.RequestHeadersJson);
            builder.Property(x => x.ResponseHeadersJson);
            builder.Property(x => x.RequestContentType).HasMaxLength(512);
            builder.Property(x => x.ResponseContentType).HasMaxLength(512);
            builder.Property(x => x.Accept).HasMaxLength(1024);
            builder.Property(x => x.Referer).HasMaxLength(1024);
            builder.Property(x => x.Origin).HasMaxLength(1024);
            builder.Property(x => x.AuthorizationType).HasMaxLength(128);
            builder.Property(x => x.UserAgent).HasMaxLength(2048);
            builder.Property(x => x.RequestBody);
            builder.Property(x => x.ResponseBody);
            builder.Property(x => x.CurlCommand);
            builder.Property(x => x.ErrorMessage);
            // 异常与资源标识字段采用固定上限，便于跨数据库统一索引/存储策略并避免无界增长。
            builder.Property(x => x.ExceptionType).HasMaxLength(512);
            builder.Property(x => x.ErrorCode).HasMaxLength(128);
            builder.Property(x => x.ExceptionStackTrace);
            builder.Property(x => x.FileMetadataJson);
            builder.Property(x => x.ImageMetadataJson);
            builder.Property(x => x.DatabaseOperationSummary);
            builder.Property(x => x.ResourceCode).HasMaxLength(128);
            builder.Property(x => x.ResourceName).HasMaxLength(256);
            builder.Property(x => x.Tags);
            builder.Property(x => x.ExtraPropertiesJson);
            builder.Property(x => x.Remark);

            builder.HasIndex(x => x.StartedAt);
        }
    }
}
