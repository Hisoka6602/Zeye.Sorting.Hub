using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDwsWindowBoundaryIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Dws_Time_Stage_Success",
                table: "Parcel_DwsMeasurements",
                columns: new[] { "PartitionTime", "Stage", "IsSuccess" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dws_Time_Stage_Success",
                table: "Parcel_DwsMeasurements");
        }
    }
}
