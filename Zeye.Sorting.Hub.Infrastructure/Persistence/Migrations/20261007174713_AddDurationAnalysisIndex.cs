using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationAnalysisIndex : Migration
    {
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStagePartitionTimeParcelIdOccurredAtColumns = new[] { "Stage", "PartitionTime", "ParcelId", "OccurredAt", "SourceInstanceId", "SourceRunId", "SourceParcelId", "RecordId", "IsSuccess", "HasReliableTimestamp" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Processing_Stage_PartitionTime_Duration",
                table: "Parcel_ProcessingRecords",
                columns: CachedStagePartitionTimeParcelIdOccurredAtColumns);
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
