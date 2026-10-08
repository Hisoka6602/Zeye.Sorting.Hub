using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationCallProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DwsCompleted",
                schema: "dbo",
                table: "ParcelDurationBackfillStates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DwsCursor",
                schema: "dbo",
                table: "ParcelDurationBackfillStates",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Projected",
                schema: "dbo",
                table: "Parcel_DurationFacts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Parcel_DurationCalls",
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SampleKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    SourceInstanceId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    SourceRunId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    SourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    PartitionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Milliseconds = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    TimingSource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RequestUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    AttemptNumber = table.Column<int>(type: "int", nullable: true),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DurationCalls", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_DwsMeasurements",
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: true),
                    SourceInstanceId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    SourceRunId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    SourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    WorkstationName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MessageIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Barcode = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    PartitionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasReliableTimestamp = table.Column<bool>(type: "bit", nullable: true),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: true),
                    WeightGrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    LengthMm = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    WidthMm = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    HeightMm = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    VolumeMm3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DwsMeasurements", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationFacts_ParcelId",
                schema: "dbo",
                table: "Parcel_DurationFacts",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationFacts_Projected_ParcelId",
                schema: "dbo",
                table: "Parcel_DurationFacts",
                columns: new[] { "Projected", "ParcelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_ParcelId",
                schema: "dbo",
                table: "Parcel_DurationCalls",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_PartitionTime_ParcelId",
                schema: "dbo",
                table: "Parcel_DurationCalls",
                columns: new[] { "PartitionTime", "ParcelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_SourceInstanceId_PartitionTime_ParcelId",
                schema: "dbo",
                table: "Parcel_DurationCalls",
                columns: new[] { "SourceInstanceId", "PartitionTime", "ParcelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_Type_PartitionTime_Milliseconds",
                schema: "dbo",
                table: "Parcel_DurationCalls",
                columns: new[] { "Type", "PartitionTime", "Milliseconds" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DwsMeasurements_ParcelId_Stage",
                schema: "dbo",
                table: "Parcel_DwsMeasurements",
                columns: new[] { "ParcelId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DwsMeasurements_PartitionTime_Stage",
                schema: "dbo",
                table: "Parcel_DwsMeasurements",
                columns: new[] { "PartitionTime", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DwsMeasurements_SourceInstanceId_PartitionTime_Stage",
                schema: "dbo",
                table: "Parcel_DwsMeasurements",
                columns: new[] { "SourceInstanceId", "PartitionTime", "Stage" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Parcel_DurationCalls",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_DwsMeasurements",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_DurationFacts_ParcelId",
                schema: "dbo",
                table: "Parcel_DurationFacts");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_DurationFacts_Projected_ParcelId",
                schema: "dbo",
                table: "Parcel_DurationFacts");

            migrationBuilder.DropColumn(
                name: "DwsCompleted",
                schema: "dbo",
                table: "ParcelDurationBackfillStates");

            migrationBuilder.DropColumn(
                name: "DwsCursor",
                schema: "dbo",
                table: "ParcelDurationBackfillStates");

            migrationBuilder.DropColumn(
                name: "Projected",
                schema: "dbo",
                table: "Parcel_DurationFacts");
        }
    }
}
