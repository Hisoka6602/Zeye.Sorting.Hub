using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationCallProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DwsCompleted",
                table: "ParcelDurationBackfillStates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DwsCursor",
                table: "ParcelDurationBackfillStates",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "Projected",
                table: "Parcel_DurationFacts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Parcel_DurationCalls",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SampleKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceInstanceId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    SourceRunId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    SourceParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    PartitionTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Milliseconds = table.Column<decimal>(type: "TEXT", precision: 20, scale: 4, nullable: true),
                    TimingSource = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    RequestUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    IsSuccess = table.Column<bool>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DurationCalls", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_DwsMeasurements",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RecordId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    SourceInstanceId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    SourceRunId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    SourceParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    WorkstationName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    MessageIdentity = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    PartitionTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    MeasuredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasReliableTimestamp = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsSuccess = table.Column<bool>(type: "INTEGER", nullable: true),
                    WeightGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    LengthMm = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    WidthMm = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    HeightMm = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    VolumeMm3 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DwsMeasurements", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationFacts_ParcelId",
                table: "Parcel_DurationFacts",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationFacts_Projected_ParcelId",
                table: "Parcel_DurationFacts",
                columns: new[] { "Projected", "ParcelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_ParcelId",
                table: "Parcel_DurationCalls",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_PartitionTime_ParcelId",
                table: "Parcel_DurationCalls",
                columns: new[] { "PartitionTime", "ParcelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_SourceInstanceId_PartitionTime_ParcelId",
                table: "Parcel_DurationCalls",
                columns: new[] { "SourceInstanceId", "PartitionTime", "ParcelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_Type_PartitionTime_Milliseconds",
                table: "Parcel_DurationCalls",
                columns: new[] { "Type", "PartitionTime", "Milliseconds" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DwsMeasurements_ParcelId_Stage",
                table: "Parcel_DwsMeasurements",
                columns: new[] { "ParcelId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DwsMeasurements_PartitionTime_Stage",
                table: "Parcel_DwsMeasurements",
                columns: new[] { "PartitionTime", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DwsMeasurements_SourceInstanceId_PartitionTime_Stage",
                table: "Parcel_DwsMeasurements",
                columns: new[] { "SourceInstanceId", "PartitionTime", "Stage" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Parcel_DurationCalls");

            migrationBuilder.DropTable(
                name: "Parcel_DwsMeasurements");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_DurationFacts_ParcelId",
                table: "Parcel_DurationFacts");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_DurationFacts_Projected_ParcelId",
                table: "Parcel_DurationFacts");

            migrationBuilder.DropColumn(
                name: "DwsCompleted",
                table: "ParcelDurationBackfillStates");

            migrationBuilder.DropColumn(
                name: "DwsCursor",
                table: "ParcelDurationBackfillStates");

            migrationBuilder.DropColumn(
                name: "Projected",
                table: "Parcel_DurationFacts");
        }
    }
}
