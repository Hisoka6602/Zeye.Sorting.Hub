using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationReadProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Parcel_DurationFacts",
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: true),
                    SourceInstanceId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    SourceRunId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    SourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    PartitionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    HasReliableTimestamp = table.Column<bool>(type: "bit", nullable: true),
                    RequestAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ElapsedMilliseconds = table.Column<int>(type: "int", nullable: true),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Transport = table.Column<bool>(type: "bit", nullable: false),
                    OperationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    AttemptId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RequestUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: true),
                    Skipped = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DurationFacts", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ParcelDurationBackfillStates",
                schema: "dbo",
                columns: table => new
                {
                    Suffix = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Cursor = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Completed = table.Column<bool>(type: "bit", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelDurationBackfillStates", x => x.Suffix);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationFacts_PartitionTime_ParcelId",
                schema: "dbo",
                table: "Parcel_DurationFacts",
                columns: new[] { "PartitionTime", "ParcelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationFacts_SourceInstanceId_PartitionTime_ParcelId",
                schema: "dbo",
                table: "Parcel_DurationFacts",
                columns: new[] { "SourceInstanceId", "PartitionTime", "ParcelId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Parcel_DurationFacts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ParcelDurationBackfillStates",
                schema: "dbo");
        }
    }
}
