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
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_DischargeTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "ActualChuteId", "DischargeTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                column: "CreatedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_NoReadType_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "NoReadType", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "RequestStatus", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "Status", "ExceptionType", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "Status", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "TargetChuteId", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "WorkstationName", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos",
                columns: new[] { "BarCode", "ParcelId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcels_ActualChuteId_DischargeTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_CreatedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_NoReadType_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                column: "WorkstationName");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos",
                column: "BarCode");
        }
    }
}
