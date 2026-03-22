using Microsoft.EntityFrameworkCore.Migrations;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBarCodesFullTextIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 步骤 1：仅对 MySQL 创建 FULLTEXT 全文索引。
            // SQL Server 的全文搜索（Full-Text Catalog + Full-Text Index）需要外部基础设施配置，
            // 超出本次迁移范围，SQL Server 路径保持 LIKE '%keyword%' 查询（已知限制）。
            if (migrationBuilder.ActiveProvider == DbProviderNames.MySql)
            {
                // 步骤 2：在 Parcels.BarCodes 列创建 FULLTEXT 索引，供 MATCH...AGAINST 查询使用。
                // 注意：MySQL InnoDB 默认 innodb_ft_min_token_size=3，即 ≥3 字符的词才会被索引。
                // 大多数条码场景（8+ 位）不受影响。
                // 若业务存在 <3 字符的短条码搜索需求，需在 MySQL 侧调低 innodb_ft_min_token_size
                // 并重建索引，或在应用层对短关键字回退至 LIKE '%keyword%' 查询（需额外分支逻辑）。
                migrationBuilder.Sql(
                    "ALTER TABLE `Parcels` ADD FULLTEXT INDEX `FTX_Parcels_BarCodes` (`BarCodes`);");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == DbProviderNames.MySql)
            {
                migrationBuilder.Sql(
                    "ALTER TABLE `Parcels` DROP INDEX `FTX_Parcels_BarCodes`;");
            }
        }
    }
}
