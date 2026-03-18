using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeParcelAggregateQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcels_WorkstationName",
                schema: "dbo",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode",
                schema: "dbo",
                table: "Parcel_BarCodeInfos");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_DischargeTime",
                schema: "dbo",
                table: "Parcels",
                columns: new[] { "ActualChuteId", "DischargeTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedTime",
                schema: "dbo",
                table: "Parcels",
                column: "CreatedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_NoReadType_ScannedTime",
                schema: "dbo",
                table: "Parcels",
                columns: new[] { "NoReadType", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime",
                schema: "dbo",
                table: "Parcels",
                columns: new[] { "RequestStatus", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime",
                schema: "dbo",
                table: "Parcels",
                columns: new[] { "Status", "ExceptionType", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ScannedTime",
                schema: "dbo",
                table: "Parcels",
                columns: new[] { "Status", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime",
                schema: "dbo",
                table: "Parcels",
                columns: new[] { "TargetChuteId", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime",
                schema: "dbo",
                table: "Parcels",
                columns: new[] { "WorkstationName", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode_ParcelId",
                schema: "dbo",
                table: "Parcel_BarCodeInfos",
                columns: new[] { "BarCode", "ParcelId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcels_ActualChuteId_DischargeTime",
                schema: "dbo",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_CreatedTime",
                schema: "dbo",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_NoReadType_ScannedTime",
                schema: "dbo",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime",
                schema: "dbo",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime",
                schema: "dbo",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_ScannedTime",
                schema: "dbo",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime",
                schema: "dbo",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime",
                schema: "dbo",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode_ParcelId",
                schema: "dbo",
                table: "Parcel_BarCodeInfos");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName",
                schema: "dbo",
                table: "Parcels",
                column: "WorkstationName");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode",
                schema: "dbo",
                table: "Parcel_BarCodeInfos",
                column: "BarCode");
        }
    }
}
