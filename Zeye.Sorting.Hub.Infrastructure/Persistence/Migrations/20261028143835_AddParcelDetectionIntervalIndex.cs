using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParcelDetectionIntervalIndex : Migration
    {
        /// <summary>源端检测时间覆盖索引的固定列顺序，供迁移只读复用。</summary>
        private static readonly string[] DetectionIntervalIndexColumns = ["DetectedTime", "Id", "SourceParcelId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Parcels_DetectedTime_Id_SourceParcelId",
                table: "Parcels",
                columns: DetectionIntervalIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcels_DetectedTime_Id_SourceParcelId",
                table: "Parcels");
        }
    }
}
