using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurationCallProjection : Migration
    {
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedProjectedParcelIdColumns = new[] { "Projected", "ParcelId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedPartitionTimeParcelIdColumns = new[] { "PartitionTime", "ParcelId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdPartitionTimeParcelIdColumns = new[] { "SourceInstanceId", "PartitionTime", "ParcelId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedTypePartitionTimeMillisecondsColumns = new[] { "Type", "PartitionTime", "Milliseconds" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedParcelIdStageColumns = new[] { "ParcelId", "Stage" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedPartitionTimeStageColumns = new[] { "PartitionTime", "Stage" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdPartitionTimeStageColumns = new[] { "SourceInstanceId", "PartitionTime", "Stage" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DwsCompleted",
                table: "ParcelDurationBackfillStates",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DwsCursor",
                table: "ParcelDurationBackfillStates",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "Projected",
                table: "Parcel_DurationFacts",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Parcel_DurationCalls",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SampleKey = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    SourceInstanceId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceRunId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    PartitionTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Type = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Milliseconds = table.Column<decimal>(type: "decimal(20,4)", precision: 20, scale: 4, nullable: true),
                    TimingSource = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Provider = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestUrl = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttemptNumber = table.Column<int>(type: "int", nullable: true),
                    IsSuccess = table.Column<bool>(type: "tinyint(1)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DurationCalls", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_DwsMeasurements",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecordId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParcelId = table.Column<long>(type: "bigint", nullable: true),
                    SourceInstanceId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceRunId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    WorkstationName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MessageIdentity = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Barcode = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    PartitionTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    MeasuredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    HasReliableTimestamp = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    IsSuccess = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    WeightGrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    LengthMm = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    WidthMm = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    HeightMm = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    VolumeMm3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DwsMeasurements", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationFacts_ParcelId",
                table: "Parcel_DurationFacts",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationFacts_Projected_ParcelId",
                table: "Parcel_DurationFacts",
                columns: CachedProjectedParcelIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_ParcelId",
                table: "Parcel_DurationCalls",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_PartitionTime_ParcelId",
                table: "Parcel_DurationCalls",
                columns: CachedPartitionTimeParcelIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_SourceInstanceId_PartitionTime_ParcelId",
                table: "Parcel_DurationCalls",
                columns: CachedSourceInstanceIdPartitionTimeParcelIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DurationCalls_Type_PartitionTime_Milliseconds",
                table: "Parcel_DurationCalls",
                columns: CachedTypePartitionTimeMillisecondsColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DwsMeasurements_ParcelId_Stage",
                table: "Parcel_DwsMeasurements",
                columns: CachedParcelIdStageColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DwsMeasurements_PartitionTime_Stage",
                table: "Parcel_DwsMeasurements",
                columns: CachedPartitionTimeStageColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DwsMeasurements_SourceInstanceId_PartitionTime_Stage",
                table: "Parcel_DwsMeasurements",
                columns: CachedSourceInstanceIdPartitionTimeStageColumns);
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
