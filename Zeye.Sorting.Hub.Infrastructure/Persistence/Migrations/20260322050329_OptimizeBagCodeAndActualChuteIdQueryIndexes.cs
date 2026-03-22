using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeBagCodeAndActualChuteIdQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 步骤 1：删除 BagCode 旧单列索引（无法覆盖 ScannedTime 范围过滤与排序）
            migrationBuilder.DropIndex(
                name: "IX_Parcels_BagCode",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            // 步骤 2：新增 (BagCode, ScannedTime) 复合索引
            // 覆盖 GetByBagCodeAsync 的查询路径：BagCode 等值 + ScannedTime 范围 + ScannedTime 降序排序
            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagCode_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "BagCode", "ScannedTime" });

            // 步骤 3：新增 (ActualChuteId, ScannedTime) 复合索引
            // 现有 (ActualChuteId, DischargeTime) 索引的第二列为 DischargeTime，无法覆盖按 ScannedTime 的分页排序
            // 新增此索引以覆盖 GetByChuteAsync 的 ActualChuteId 过滤 + ScannedTime 范围/排序路径
            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "ActualChuteId", "ScannedTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcels_BagCode_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagCode",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                column: "BagCode");
        }
    }
}
