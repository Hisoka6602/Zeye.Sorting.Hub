using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseExternalProvidedParcelId : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// 应用迁移：
        /// - MySQL：移除 Parcels.Id 的 Identity 注解，改为外部提供主键；
        /// - SQL Server：保持 no-op（SQL Server 不支持通过 ALTER COLUMN 直接移除 IDENTITY）。
        /// </summary>
        /// <param name="migrationBuilder">迁移构建器。</param>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == DbProviderNames.MySql)
            {
                migrationBuilder.AlterColumn<long>(
                    name: "Id",
                    table: "Parcels",
                    type: "bigint",
                    nullable: false,
                    oldClrType: typeof(long),
                    oldType: "bigint")
                    .OldAnnotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);
                return;
            }

            if (migrationBuilder.ActiveProvider == DbProviderNames.SqlServer)
            {
                // SQL Server 不支持通过 ALTER COLUMN 直接移除 IDENTITY。
                // 为避免生成不可执行脚本，此迁移在 SQL Server 路径保持 no-op，
                // 后续需通过“建新表 + 数据回填 + 外键重建”专项迁移落地。
                return;
            }

            throw new NotSupportedException(
                $"迁移 {nameof(UseExternalProvidedParcelId)} 暂不支持 Provider: {migrationBuilder.ActiveProvider}。");
        }

        /// <inheritdoc />
        /// <summary>
        /// 回滚迁移：
        /// - MySQL：恢复 Parcels.Id 的 Identity 注解；
        /// - SQL Server：保持 no-op（对应 Up 中 SQL Server 分支的 no-op 行为）。
        /// </summary>
        /// <param name="migrationBuilder">迁移构建器。</param>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            if (migrationBuilder.ActiveProvider == DbProviderNames.MySql)
            {
                migrationBuilder.AlterColumn<long>(
                    name: "Id",
                    table: "Parcels",
                    type: "bigint",
                    nullable: false,
                    oldClrType: typeof(long),
                    oldType: "bigint")
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);
                return;
            }

            if (migrationBuilder.ActiveProvider == DbProviderNames.SqlServer)
            {
                // SQL Server 下此迁移为 no-op，对应回滚同样保持 no-op。
                return;
            }

            throw new NotSupportedException(
                $"迁移 {nameof(UseExternalProvidedParcelId)} 暂不支持 Provider: {migrationBuilder.ActiveProvider}。");
        }
    }
}
