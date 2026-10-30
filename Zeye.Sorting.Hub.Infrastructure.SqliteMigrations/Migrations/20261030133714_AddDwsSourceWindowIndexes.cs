using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDwsSourceWindowIndexes : Migration
    {
        /// <summary>来源时间窗口覆盖索引的固定列顺序。</summary>
        private static readonly string[] SourceWindowIndexColumns = ["SourceInstanceId", "PartitionTime", "Stage", "IsSuccess"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Processing_Source_Time_Stage_Success",
                table: "Parcel_ProcessingRecords",
                columns: SourceWindowIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Dws_Source_Time_Stage_Success",
                table: "Parcel_DwsMeasurements",
                columns: SourceWindowIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processing_Source_Time_Stage_Success",
                table: "Parcel_ProcessingRecords");

            migrationBuilder.DropIndex(
                name: "IX_Dws_Source_Time_Stage_Success",
                table: "Parcel_DwsMeasurements");
        }
    }
}
