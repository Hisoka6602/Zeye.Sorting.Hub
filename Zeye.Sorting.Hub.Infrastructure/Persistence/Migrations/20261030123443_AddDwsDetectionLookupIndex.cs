using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDwsDetectionLookupIndex : Migration
    {
        /// <summary>DWS检测端点覆盖索引的固定列顺序。</summary>
        private static readonly string[] DetectionLookupIndexColumns = ["ParcelId", "Stage", "SourceInstanceId", "SourceRunId", "SourceParcelId", "OccurredAt", "PartitionTime", "HasReliableTimestamp", "IsSuccess"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Processing_Parcel_Stage_Detection",
                table: "Parcel_ProcessingRecords",
                columns: DetectionLookupIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processing_Parcel_Stage_Detection",
                table: "Parcel_ProcessingRecords");
        }
    }
}
