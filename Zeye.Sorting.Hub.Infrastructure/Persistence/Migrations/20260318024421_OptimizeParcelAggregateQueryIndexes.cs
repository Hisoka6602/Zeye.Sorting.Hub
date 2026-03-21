using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeParcelAggregateQueryIndexes : Migration
    {
        /// <summary>
        /// SQL Server Provider 名称。
        /// </summary>
        private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

        /// <summary>
        /// SQL Server 默认 schema。
        /// </summary>
        private const string SqlServerDefaultSchema = "dbo";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcels_WorkstationName",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_DischargeTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "ActualChuteId", "DischargeTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels",
                column: "CreatedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_NoReadType_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "NoReadType", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "RequestStatus", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "Status", "ExceptionType", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "Status", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "TargetChuteId", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels",
                columns: new[] { "WorkstationName", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode_ParcelId",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos",
                columns: new[] { "BarCode", "ParcelId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcels_ActualChuteId_DischargeTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_CreatedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_NoReadType_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode_ParcelId",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels",
                column: "WorkstationName");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos",
                column: "BarCode");
        }

        /// <summary>
        /// 按 Provider 解析迁移使用的 schema：
        /// - SQL Server 使用 dbo；
        /// - MySQL 不使用 schema（返回 null）。
        /// </summary>
        private static string ResolveSchema(MigrationBuilder migrationBuilder)
        {
            return migrationBuilder.ActiveProvider == SqlServerProvider
                ? SqlServerDefaultSchema
                : null;
        }
    }
}
