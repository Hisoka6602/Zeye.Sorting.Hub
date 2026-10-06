using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsCoveringIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CompletedTime_Status_SourceParcelId_DetectedTime",
                table: "Parcels",
                columns: new[] { "CompletedTime", "Status", "SourceParcelId", "DetectedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedTime_Id_SourceParcelId_DetectedTime",
                table: "Parcels",
                columns: new[] { "CreatedTime", "Id", "SourceParcelId", "DetectedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_OccurredAt_IsSuccess_ParcelId_Stage",
                table: "Parcel_ProcessingRecords",
                columns: new[] { "OccurredAt", "IsSuccess", "ParcelId", "Stage" });
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
