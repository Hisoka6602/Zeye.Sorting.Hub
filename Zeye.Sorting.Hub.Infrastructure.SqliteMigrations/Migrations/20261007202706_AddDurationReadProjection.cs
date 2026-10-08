using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationReadProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parcel_DurationFacts",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    RecordId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    SourceInstanceId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    SourceRunId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    SourceParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    PartitionTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    HasReliableTimestamp = table.Column<bool>(type: "INTEGER", nullable: true),
                    RequestAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResponseAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ElapsedMilliseconds = table.Column<int>(type: "INTEGER", nullable: true),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Transport = table.Column<bool>(type: "INTEGER", nullable: false),
                    OperationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AttemptId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Outcome = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    RequestUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    IsSuccess = table.Column<bool>(type: "INTEGER", nullable: true),
                    Skipped = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DurationFacts", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ParcelDurationBackfillStates",
                columns: table => new
                {
                    Suffix = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Cursor = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Completed = table.Column<bool>(type: "INTEGER", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelDurationBackfillStates", x => x.Suffix);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationFacts_PartitionTime_ParcelId",
                table: "Parcel_DurationFacts",
                columns: new[] { "PartitionTime", "ParcelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationFacts_SourceInstanceId_PartitionTime_ParcelId",
                table: "Parcel_DurationFacts",
                columns: new[] { "SourceInstanceId", "PartitionTime", "ParcelId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Parcel_DurationFacts");

            migrationBuilder.DropTable(
                name: "ParcelDurationBackfillStates");
        }
    }
}
