using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.ValueObjects;

namespace Zeye.Sorting.Hub.Infrastructure.EntityConfigurations {

    /// <summary>
    /// `BagInfo` 的 EF Core 持久化映射配置。
    ///
    /// 设计说明：
    /// - `BagInfo` 位于 Domain 层，为 `record class`（值对象/轻量模型），且不包含持久化所需的主键（Id）。
    /// - 为了避免在 Domain 对象上引入数据库概念（如 `Id`），此处采用 EF Core 的影子属性（Shadow Property）
    ///   作为主键，并将其映射到实体表（`Bags`）。
    /// - [MaxLength] 特征标记已在 Domain 层声明，此处无需重复配置 HasMaxLength。
    /// </summary>
    public sealed class BagInfoEntityTypeConfiguration : IEntityTypeConfiguration<BagInfo> {

        private const string SchemaDbo = "dbo";

        /// <summary>
        /// 执行逻辑：Configure。
        /// </summary>
        public void Configure(EntityTypeBuilder<BagInfo> builder) {
            builder.ToTable("Bags", SchemaDbo);

            // 影子主键（Shadow Key）：Domain 中不暴露 Id，避免领域污染
            builder.Property<long>("BagId").ValueGeneratedOnAdd();
            builder.HasKey("BagId");

            // 索引由 Domain 层 [Index] 特征标记声明，配置层不重复声明。
        }
    }
}
