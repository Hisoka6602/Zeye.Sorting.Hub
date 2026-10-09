using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsCoveringIndexes : Migration
    {
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedCompletedTimeStatusSourceParcelIdDetectedTimeColumns = new[] { "CompletedTime", "Status", "SourceParcelId", "DetectedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedCreatedTimeIdSourceParcelIdDetectedTimeColumns = new[] { "CreatedTime", "Id", "SourceParcelId", "DetectedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedOccurredAtIsSuccessParcelIdStageColumns = new[] { "OccurredAt", "IsSuccess", "ParcelId", "Stage" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CompletedTime_Status_SourceParcelId_DetectedTime",
                table: "Parcels",
                columns: CachedCompletedTimeStatusSourceParcelIdDetectedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedTime_Id_SourceParcelId_DetectedTime",
                table: "Parcels",
                columns: CachedCreatedTimeIdSourceParcelIdDetectedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_OccurredAt_IsSuccess_ParcelId_Stage",
                table: "Parcel_ProcessingRecords",
                columns: CachedOccurredAtIsSuccessParcelIdStageColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcels_CompletedTime_Status_SourceParcelId_DetectedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_CreatedTime_Id_SourceParcelId_DetectedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_ProcessingRecords_OccurredAt_IsSuccess_ParcelId_Stage",
                table: "Parcel_ProcessingRecords");
        }
    }
}
