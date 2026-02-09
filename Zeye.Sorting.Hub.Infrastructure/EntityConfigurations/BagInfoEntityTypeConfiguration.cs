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
    /// - `BagInfo` 位于 Domain 层，为 `record class`（值对象/轻量模型），且**不包含**持久化所需的主键（Id）。
    /// - 为了避免在 Domain 对象上引入数据库概念（如 `Id`），此处采用 EF Core 的“影子属性（Shadow Property）”
    ///   作为主键，并将其映射到实体表（`Bags`）。
    /// - 本配置按“实体表”建模：即便 `BagInfo` 在语义上更像值对象，也将其落为独立表，以支持：
    ///   - 多对一复用（例如多个包裹/记录引用同一个袋信息）
    ///   - 数据库层面的唯一约束与索引优化
    /// </summary>
    public sealed class BagInfoEntityTypeConfiguration : IEntityTypeConfiguration<BagInfo> {

        /// <summary>
        /// 默认数据库架构名。
        /// </summary>
        private const string SchemaDbo = "dbo";

        /// <summary>
        /// 通用“编码类字符串”最大长度（如格口名/袋号等）。
        /// 统一使用常量便于在迁移与约束调整时保持一致。
        /// </summary>
        private const int MaxCode128 = 128;

        /// <summary>
        /// 配置 `BagInfo` 在 EF Core 中的表映射、字段映射、约束与索引。
        /// </summary>
        /// <param name="builder">EF Core 实体类型构建器。</param>
        public void Configure(EntityTypeBuilder<BagInfo> builder) {
            // 将 `BagInfo` 映射到 dbo.Bags 表
            // 表名使用复数 `Bags`，体现“袋信息集合”的语义。
            builder.ToTable("Bags", SchemaDbo);

            // ----------------------------
            // 主键策略：影子主键（Shadow Key）
            // ----------------------------
            // 说明：
            // - Domain 中的 `BagInfo` 不暴露 Id（保持纯净领域模型）
            // - EF Core 允许为实体配置未出现在 CLR 类型上的属性（影子属性）
            // - 这里定义影子属性 `BagId` 作为主键，并配置为自增（ValueGeneratedOnAdd）
            //
            // 注意：
            // - 影子属性只能通过 EF Core ChangeTracker/EF.Property 访问
            // - 对业务逻辑不可见，从而避免“领域污染”
            builder.Property<long>("BagId")
                .HasColumnName("BagId")
                .ValueGeneratedOnAdd();

            // 显式指定主键为影子属性 `BagId`
            builder.HasKey("BagId");

            // ----------------------------
            // 字段映射：ChuteId（格口 Id）
            // ----------------------------
            // 说明：
            // - 必填字段：一个袋必须关联到某个格口
            // - 列名与属性名一致便于理解与排查问题
            builder.Property(x => x.ChuteId)
                .HasColumnName("ChuteId")
                .IsRequired();

            // ----------------------------
            // 字段映射：ChuteName（格口名称）
            // ----------------------------
            // 说明：
            // - 必填字段：用于显示/打印/追踪
            // - MaxLength：限制数据库字段长度，防止异常写入导致索引膨胀或截断
            builder.Property(x => x.ChuteName)
                .HasColumnName("ChuteName")
                .HasMaxLength(MaxCode128)
                .IsRequired();

            // ----------------------------
            // 字段映射：BagCode（袋号/袋条码）
            // ----------------------------
            // 说明：
            // - 必填字段：袋的业务标识（通常是条码）
            // - MaxLength：同上，限制长度
            builder.Property(x => x.BagCode)
                .HasColumnName("BagCode")
                .HasMaxLength(MaxCode128)
                .IsRequired();

            // ----------------------------
            // 字段映射：PackageCount（袋内包裹数）
            // ----------------------------
            // 说明：
            // - 必填字段：用于统计与下游校验
            // - 注意：如需限制非负，可在 Domain 或数据库 Check 约束中实现（此处未添加）
            builder.Property(x => x.PackageCount)
                .HasColumnName("PackageCount")
                .IsRequired();

            // ----------------------------
            // 字段映射：BaggingTime（装袋时间）
            // ----------------------------
            // 说明：
            // - 可选字段：允许未装袋/未知时间的场景
            // - 未标记 IsRequired()，即允许为 null
            builder.Property(x => x.BaggingTime)
                .HasColumnName("BaggingTime");

            // ----------------------------
            // 索引与唯一约束
            // ----------------------------

            // 约束：“每个格口对应一个 Bag”
            // 通过 `ChuteId` 唯一索引实现：
            // - 确保同一格口在任一时刻只能存在一条袋信息记录
            // - 同时可加速按格口查询（常见查询场景）
            builder.HasIndex(x => x.ChuteId).IsUnique();

            // `BagCode` 通常也应唯一：
            // - 同一个袋号若重复出现，通常代表重复落库或业务异常
            // - 若业务允许重复（例如历史归档/批次复用等），可移除 IsUnique()
            builder.HasIndex(x => x.BagCode).IsUnique();
        }
    }
}
