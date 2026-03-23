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

            // 冷表承载大文本字段，避免热表过宽导致写放大与索引维护开销上升。
            builder.Property(x => x.RequestUrl).HasColumnType("longtext");
            builder.Property(x => x.RequestQueryString).HasColumnType("longtext");
            builder.Property(x => x.RequestHeadersJson).HasColumnType("longtext");
            builder.Property(x => x.ResponseHeadersJson).HasColumnType("longtext");
            builder.Property(x => x.RequestContentType).HasMaxLength(512);
            builder.Property(x => x.ResponseContentType).HasMaxLength(512);
            builder.Property(x => x.Accept).HasMaxLength(1024);
            builder.Property(x => x.Referer).HasMaxLength(1024);
            builder.Property(x => x.Origin).HasMaxLength(1024);
            builder.Property(x => x.AuthorizationType).HasMaxLength(128);
            builder.Property(x => x.UserAgent).HasMaxLength(2048);
            builder.Property(x => x.RequestBody).HasColumnType("longtext");
            builder.Property(x => x.ResponseBody).HasColumnType("longtext");
            builder.Property(x => x.CurlCommand).HasColumnType("longtext");
            builder.Property(x => x.ErrorMessage).HasColumnType("longtext");
            builder.Property(x => x.ExceptionStackTrace).HasColumnType("longtext");
            builder.Property(x => x.FileMetadataJson).HasColumnType("longtext");
            builder.Property(x => x.ImageMetadataJson).HasColumnType("longtext");
            builder.Property(x => x.DatabaseOperationSummary).HasColumnType("longtext");
            builder.Property(x => x.ExtraPropertiesJson).HasColumnType("longtext");
            builder.Property(x => x.Remark).HasColumnType("longtext");

            builder.HasIndex(x => x.StartedAt);
        }
    }
}
