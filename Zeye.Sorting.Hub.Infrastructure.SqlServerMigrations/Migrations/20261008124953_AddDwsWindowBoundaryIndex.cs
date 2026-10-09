using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDwsWindowBoundaryIndex : Migration
    {
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedPartitionTimeStageIsSuccessColumns = new[] { "PartitionTime", "Stage", "IsSuccess" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Dws_Time_Stage_Success",
                schema: "dbo",
                table: "Parcel_DwsMeasurements",
                columns: CachedPartitionTimeStageIsSuccessColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dws_Time_Stage_Success",
                schema: "dbo",
                table: "Parcel_DwsMeasurements");
        }
    }
}
