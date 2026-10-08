using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceStageDurationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Processing_Source_Stage_Duration",
                table: "Parcel_ProcessingRecords",
                columns: new[] { "SourceInstanceId", "Stage", "PartitionTime", "ParcelId", "OccurredAt", "SourceRunId", "SourceParcelId", "RecordId", "IsSuccess", "HasReliableTimestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processing_Source_Stage_Duration",
                table: "Parcel_ProcessingRecords");
        }
    }
}
