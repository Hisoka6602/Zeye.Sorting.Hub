using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationAnalysisIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Processing_Stage_PartitionTime_Duration",
                table: "Parcel_ProcessingRecords",
                columns: new[] { "Stage", "PartitionTime", "ParcelId", "OccurredAt", "SourceInstanceId", "SourceRunId", "SourceParcelId", "RecordId", "IsSuccess", "HasReliableTimestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processing_Stage_PartitionTime_Duration",
                table: "Parcel_ProcessingRecords");
        }
    }
}
