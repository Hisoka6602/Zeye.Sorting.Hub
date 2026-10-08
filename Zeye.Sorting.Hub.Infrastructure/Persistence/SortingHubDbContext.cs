using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Zeye.Sorting.Hub.Infrastructure.EntityConfigurations;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Sharding;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence {

    /// <summary>
    /// 分拣中心 DbContext（仅负责映射与 DbSet，不执行运维动作）
    /// </summary>
    public sealed partial class SortingHubDbContext : DbContext {
        /// <summary>当前包裹物理表后缀，空值表示历史基础表；只允许工厂在首次访问模型前设置。</summary>
        internal string ParcelPartitionSuffix { get; init; } = string.Empty;
        /// <summary>当前审计物理日表后缀，空值表示历史基础表。</summary>
        internal string AuditPartitionSuffix { get; init; } = string.Empty;
        /// <summary>
        /// SQL Server 默认 schema。
        /// </summary>
        private const string SqlServerDefaultSchema = "dbo";

        /// <summary>
        /// 初始化 <see cref="SortingHubDbContext"/>。
        /// </summary>
        /// <param name="options">DbContext 配置选项。</param>
        public SortingHubDbContext(DbContextOptions<SortingHubDbContext> options) : base(options) {
        }

        /// <summary>
        /// 应用程序集内全部实体类型配置。
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            if (Database.ProviderName == DbProviderNames.SqlServer) {
                modelBuilder.HasDefaultSchema(SqlServerDefaultSchema);
            }

            // 统一应用实体配置，Parcel 的索引根据数据库提供器单独配置。
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(SortingHubDbContext).Assembly,
                type => type != typeof(ParcelEntityTypeConfiguration));
            modelBuilder.ApplyConfiguration(new ParcelEntityTypeConfiguration(Database.ProviderName == DbProviderNames.SqlServer));
            // 协议身份区分大小写，不能由数据库默认语言排序规则合并不同来源图片。
            FusionEntityTypeConfiguration.ConfigureIdentities(modelBuilder, Database.ProviderName);
            modelBuilder.Entity<Management.ManagedDocument>(b => {
                b.ToTable("ManagedDocuments"); b.HasKey(x => x.Key); b.Property(x => x.Key).HasMaxLength(128);
                b.Property(x => x.Revision).IsConcurrencyToken();
            });

            // 步骤1：全局目录与去重凭据保持基础表名，跨分表身份不随粒度变化。
            modelBuilder.Entity<ParcelPartitionCatalogEntry>(b => {
                b.ToTable("ParcelPartitionCatalog"); b.HasKey(x => x.Suffix); b.Property(x => x.Suffix).HasMaxLength(32);
            });
            modelBuilder.Entity<ParcelLocation>(b => {
                b.ToTable("ParcelLocations"); b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
                b.Property(x => x.SourceKey).HasMaxLength(64); b.HasIndex(x => x.SourceKey).IsUnique(); b.Property(x => x.Suffix).HasMaxLength(32);
            });
            modelBuilder.Entity<ParcelProcessingReceipt>(b => {
                b.ToTable("ParcelProcessingReceipts"); b.HasKey(x => x.Key); b.Property(x => x.Key).HasMaxLength(64);
                b.Property(x => x.PayloadHash).HasMaxLength(64); b.Property(x => x.Suffix).HasMaxLength(32);
                b.HasIndex(x => new { x.ParcelId, x.RecordedAt });
            });
            modelBuilder.Entity<PartitionSchemaVersion>(b => {
                b.ToTable("PartitionSchemaVersions"); b.HasKey(x => x.Key); b.Property(x => x.Key).HasMaxLength(64);
                b.Property(x => x.MigrationId).HasMaxLength(150);
            });
            // 步骤2：包裹及其拥有的值对象、处理记录始终在同一周期，集包保持全局共享。
            if (ParcelPartitionSuffix.Length > 0) {
                foreach (var entity in modelBuilder.Model.GetEntityTypes()) {
                    var table = entity.GetTableName();
                    if (table == "Parcels" || table?.StartsWith("Parcel_", StringComparison.Ordinal) == true) {
                        // 约定索引名称随表名自动变化，显式名称必须同步重定位，SQLite/Oracle 的索引名称属于整个 schema。
                        foreach (var index in entity.GetIndexes()) {
                            if (index.FindAnnotation(RelationalAnnotationNames.Name)?.Value is string name)
                                index.SetDatabaseName(ParcelPartitionObjectNames.PhysicalName(name, table, ParcelPartitionSuffix, modelBuilder.Model.GetMaxIdentifierLength()));
                        }
                        entity.SetTableName(table + "_" + ParcelPartitionSuffix);
                    }
                }
            }

            if (Database.ProviderName == DbProviderNames.Oracle) OracleModelCompatibility.Configure(modelBuilder);
            if (AuditPartitionSuffix.Length > 0) {
                var entities = modelBuilder.Model.GetEntityTypes().Where(entity => entity.GetTableName() is "WebRequestAuditLogs" or "WebRequestAuditLogDetails").ToArray();
                var indexes = entities.SelectMany(entity => entity.GetIndexes()).Select(index => (Index: index, Name: index.GetDatabaseName()!)).ToArray();
                var keys = entities.SelectMany(entity => entity.GetKeys()).Select(key => (Key: key, Name: key.GetName()!)).ToArray();
                var foreignKeys = entities.SelectMany(entity => entity.GetForeignKeys()).Select(key => (Key: key, Name: key.GetConstraintName()!)).ToArray();
                foreach (var entity in entities) entity.SetTableName(entity.GetTableName() + "_" + AuditPartitionSuffix);
                foreach (var (index, name) in indexes) index.SetDatabaseName(AuditPartitionMaintenanceService.PhysicalName(name, AuditPartitionSuffix));
                foreach (var (key, name) in keys) key.SetName(AuditPartitionMaintenanceService.PhysicalName(name, AuditPartitionSuffix));
                foreach (var (key, name) in foreignKeys) key.SetConstraintName(AuditPartitionMaintenanceService.PhysicalName(name, AuditPartitionSuffix));
            }

            base.OnModelCreating(modelBuilder);
        }
    }
}
