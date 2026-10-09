using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistFusionProcessing : Migration
    {
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedMessageIdentityReceivedAtColumns = new[] { "MessageIdentity", "ReceivedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedParcelIdOccurredAtColumns = new[] { "ParcelId", "OccurredAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdSourceRunIdSourceParcelIdColumns = new[] { "SourceInstanceId", "SourceRunId", "SourceParcelId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedParcelIdRecordedAtColumns = new[] { "ParcelId", "RecordedAt" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Width",
                table: "Parcels",
                type: ResolveColumnType("decimal(18,3)", migrationBuilder),
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: ResolveColumnType("decimal(18,3)", migrationBuilder),
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Weight",
                table: "Parcels",
                type: ResolveColumnType("decimal(21,6)", migrationBuilder),
                precision: 21,
                scale: 6,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: ResolveColumnType("decimal(18,3)", migrationBuilder),
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Volume",
                table: "Parcels",
                type: ResolveColumnType("decimal(18,3)", migrationBuilder),
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: ResolveColumnType("decimal(18,3)", migrationBuilder),
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<long>(
                name: "TargetChuteId",
                table: "Parcels",
                type: ResolveColumnType("bigint", migrationBuilder),
                nullable: true,
                oldClrType: typeof(long),
                oldType: ResolveColumnType("bigint", migrationBuilder));

            migrationBuilder.AlterColumn<decimal>(
                name: "Length",
                table: "Parcels",
                type: ResolveColumnType("decimal(18,3)", migrationBuilder),
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: ResolveColumnType("decimal(18,3)", migrationBuilder),
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "Height",
                table: "Parcels",
                type: ResolveColumnType("decimal(18,3)", migrationBuilder),
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: ResolveColumnType("decimal(18,3)", migrationBuilder),
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DischargeTime",
                table: "Parcels",
                type: ResolveColumnType("datetime(6)", migrationBuilder),
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: ResolveColumnType("datetime(6)", migrationBuilder));

            migrationBuilder.AlterColumn<long>(
                name: "ActualChuteId",
                table: "Parcels",
                type: ResolveColumnType("bigint", migrationBuilder),
                nullable: true,
                oldClrType: typeof(long),
                oldType: ResolveColumnType("bigint", migrationBuilder));

            migrationBuilder.AddColumn<string>(
                name: "ActualChuteCode",
                table: "Parcels",
                type: ResolveColumnType("varchar(128)", migrationBuilder),
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "DetectedTime",
                table: "Parcels",
                type: ResolveColumnType("datetime(6)", migrationBuilder),
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFallbackChuteAssigned",
                table: "Parcels",
                type: ResolveColumnType("tinyint(1)", migrationBuilder),
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRoutingBlocked",
                table: "Parcels",
                type: ResolveColumnType("tinyint(1)", migrationBuilder),
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MeasurementTime",
                table: "Parcels",
                type: ResolveColumnType("datetime(6)", migrationBuilder),
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceExceptionCode",
                table: "Parcels",
                type: ResolveColumnType("varchar(128)", migrationBuilder),
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SourceInstanceId",
                table: "Parcels",
                type: ResolveColumnType("varchar(96)", migrationBuilder),
                maxLength: 96,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "SourceParcelId",
                table: "Parcels",
                type: ResolveColumnType("bigint", migrationBuilder),
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceRunId",
                table: "Parcels",
                type: ResolveColumnType("varchar(96)", migrationBuilder),
                maxLength: 96,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TargetChuteCode",
                table: "Parcels",
                type: ResolveColumnType("varchar(128)", migrationBuilder),
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TaskCode",
                table: "Parcels",
                type: ResolveColumnType("varchar(256)", migrationBuilder),
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "VolumetricWeightGrams",
                table: "Parcels",
                type: ResolveColumnType("decimal(18,3)", migrationBuilder),
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Parcel_ProcessingRecords",
                columns: table => new
                {
                    Key = table.Column<string>(type: ResolveColumnType("varchar(64)", migrationBuilder), maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WorkstationName = table.Column<string>(type: ResolveColumnType("varchar(128)", migrationBuilder), maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PreviousCreationGapMilliseconds = table.Column<long>(type: ResolveColumnType("bigint", migrationBuilder), nullable: true),
                    IsSpacingViolation = table.Column<bool>(type: ResolveColumnType("tinyint(1)", migrationBuilder), nullable: true),
                    IsAwaitingWcsDecision = table.Column<bool>(type: ResolveColumnType("tinyint(1)", migrationBuilder), nullable: true),
                    RecordId = table.Column<string>(type: ResolveColumnType("varchar(128)", migrationBuilder), maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceInstanceId = table.Column<string>(type: ResolveColumnType("varchar(96)", migrationBuilder), maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceRunId = table.Column<string>(type: ResolveColumnType("varchar(96)", migrationBuilder), maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceParcelId = table.Column<long>(type: ResolveColumnType("bigint", migrationBuilder), nullable: true),
                    ParcelId = table.Column<long>(type: ResolveColumnType("bigint", migrationBuilder), nullable: true),
                    Stage = table.Column<int>(type: ResolveColumnType("int", migrationBuilder), nullable: false),
                    OccurredAt = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: false),
                    RecordedAt = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: false),
                    PartitionTime = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: false),
                    PayloadHash = table.Column<string>(type: ResolveColumnType("varchar(64)", migrationBuilder), maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSuccess = table.Column<bool>(type: ResolveColumnType("tinyint(1)", migrationBuilder), nullable: true),
                    AttemptNumber = table.Column<int>(type: ResolveColumnType("int", migrationBuilder), nullable: false),
                    Barcode = table.Column<string>(type: ResolveColumnType("varchar(1024)", migrationBuilder), maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BarcodesJson = table.Column<string>(type: ResolveColumnType("longtext", migrationBuilder), nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WeightGrams = table.Column<decimal>(type: ResolveColumnType("decimal(18,3)", migrationBuilder), precision: 18, scale: 3, nullable: true),
                    LengthMm = table.Column<decimal>(type: ResolveColumnType("decimal(18,3)", migrationBuilder), precision: 18, scale: 3, nullable: true),
                    WidthMm = table.Column<decimal>(type: ResolveColumnType("decimal(18,3)", migrationBuilder), precision: 18, scale: 3, nullable: true),
                    HeightMm = table.Column<decimal>(type: ResolveColumnType("decimal(18,3)", migrationBuilder), precision: 18, scale: 3, nullable: true),
                    VolumeMm3 = table.Column<decimal>(type: ResolveColumnType("decimal(18,3)", migrationBuilder), precision: 18, scale: 3, nullable: true),
                    VolumetricWeightGrams = table.Column<decimal>(type: ResolveColumnType("decimal(18,3)", migrationBuilder), precision: 18, scale: 3, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: true),
                    MeasuredAt = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: true),
                    HasReliableTimestamp = table.Column<bool>(type: ResolveColumnType("tinyint(1)", migrationBuilder), nullable: true),
                    HasReliableFrameBoundary = table.Column<bool>(type: ResolveColumnType("tinyint(1)", migrationBuilder), nullable: true),
                    CorrelationId = table.Column<long>(type: ResolveColumnType("bigint", migrationBuilder), nullable: true),
                    TriggerBatch = table.Column<string>(type: ResolveColumnType("varchar(128)", migrationBuilder), maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ScanSequence = table.Column<string>(type: ResolveColumnType("varchar(128)", migrationBuilder), maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MessageIdentity = table.Column<string>(type: ResolveColumnType("varchar(256)", migrationBuilder), maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BindingMode = table.Column<string>(type: ResolveColumnType("varchar(32)", migrationBuilder), maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CandidateSourceParcelId = table.Column<long>(type: ResolveColumnType("bigint", migrationBuilder), nullable: true),
                    FinalSourceParcelId = table.Column<long>(type: ResolveColumnType("bigint", migrationBuilder), nullable: true),
                    DeltaMilliseconds = table.Column<decimal>(type: ResolveColumnType("decimal(18,3)", migrationBuilder), precision: 18, scale: 3, nullable: true),
                    DecisionReason = table.Column<string>(type: ResolveColumnType("varchar(2048)", migrationBuilder), maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FifoRecoveryMode = table.Column<string>(type: ResolveColumnType("varchar(64)", migrationBuilder), maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Provider = table.Column<string>(type: ResolveColumnType("varchar(96)", migrationBuilder), maxLength: 96, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TaskCode = table.Column<string>(type: ResolveColumnType("varchar(256)", migrationBuilder), maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TargetChuteCode = table.Column<string>(type: ResolveColumnType("varchar(128)", migrationBuilder), maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DispatchedChuteCode = table.Column<string>(type: ResolveColumnType("varchar(128)", migrationBuilder), maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActualChuteCode = table.Column<string>(type: ResolveColumnType("varchar(128)", migrationBuilder), maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsFallback = table.Column<bool>(type: ResolveColumnType("tinyint(1)", migrationBuilder), nullable: true),
                    IsRoutingBlocked = table.Column<bool>(type: ResolveColumnType("tinyint(1)", migrationBuilder), nullable: true),
                    ExceptionCode = table.Column<string>(type: ResolveColumnType("varchar(128)", migrationBuilder), maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: ResolveColumnType("varchar(2048)", migrationBuilder), maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawPayload = table.Column<string>(type: ResolveColumnType("longtext", migrationBuilder), nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestUrl = table.Column<string>(type: ResolveColumnType("varchar(512)", migrationBuilder), maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestHeaders = table.Column<string>(type: ResolveColumnType("longtext", migrationBuilder), nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestBody = table.Column<string>(type: ResolveColumnType("longtext", migrationBuilder), nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseBody = table.Column<string>(type: ResolveColumnType("longtext", migrationBuilder), nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseStatusCode = table.Column<int>(type: ResolveColumnType("int", migrationBuilder), nullable: true),
                    RequestAt = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: true),
                    ResponseAt = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: true),
                    ElapsedMilliseconds = table.Column<int>(type: ResolveColumnType("int", migrationBuilder), nullable: true),
                    ImagePath = table.Column<string>(type: ResolveColumnType("varchar(1024)", migrationBuilder), maxLength: 1024, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageCamera = table.Column<string>(type: ResolveColumnType("varchar(128)", migrationBuilder), maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageContentHash = table.Column<string>(type: ResolveColumnType("varchar(128)", migrationBuilder), maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_ProcessingRecords", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ParcelLocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: ResolveColumnType("bigint", migrationBuilder), nullable: false),
                    SourceKey = table.Column<string>(type: ResolveColumnType("varchar(64)", migrationBuilder), maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Suffix = table.Column<string>(type: ResolveColumnType("varchar(32)", migrationBuilder), maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedTime = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelLocations", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ParcelPartitionCatalog",
                columns: table => new
                {
                    Suffix = table.Column<string>(type: ResolveColumnType("varchar(32)", migrationBuilder), maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Start = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: false),
                    End = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: false),
                    CreatedTime = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelPartitionCatalog", x => x.Suffix);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ParcelProcessingReceipts",
                columns: table => new
                {
                    Key = table.Column<string>(type: ResolveColumnType("varchar(64)", migrationBuilder), maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayloadHash = table.Column<string>(type: ResolveColumnType("varchar(64)", migrationBuilder), maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParcelId = table.Column<long>(type: ResolveColumnType("bigint", migrationBuilder), nullable: true),
                    Suffix = table.Column<string>(type: ResolveColumnType("varchar(32)", migrationBuilder), maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecordedAt = table.Column<DateTime>(type: ResolveColumnType("datetime(6)", migrationBuilder), nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelProcessingReceipts", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_MessageIdentity_ReceivedAt",
                table: "Parcel_ProcessingRecords",
                columns: CachedMessageIdentityReceivedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_ParcelId_OccurredAt",
                table: "Parcel_ProcessingRecords",
                columns: CachedParcelIdOccurredAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_SourceInstanceId_SourceRunId_Source~",
                table: "Parcel_ProcessingRecords",
                columns: CachedSourceInstanceIdSourceRunIdSourceParcelIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ParcelLocations_SourceKey",
                table: "ParcelLocations",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParcelProcessingReceipts_ParcelId_RecordedAt",
                table: "ParcelProcessingReceipts",
                columns: CachedParcelIdRecordedAtColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 步骤1：只允许回退没有新业务事实及空值语义的数据模型，避免静默删历史或伪造零值。
            var sqlServer = MigrationSchemaResolver.IsSqlServer(migrationBuilder);
            var unsafeRollback =
                "EXISTS (SELECT 1 FROM ParcelPartitionCatalog) OR EXISTS (SELECT 1 FROM ParcelLocations) OR " +
                "EXISTS (SELECT 1 FROM ParcelProcessingReceipts) OR EXISTS (SELECT 1 FROM Parcel_ProcessingRecords) OR " +
                "EXISTS (SELECT 1 FROM Parcels WHERE SourceInstanceId IS NOT NULL OR Weight IS NULL OR Length IS NULL OR Width IS NULL OR Height IS NULL OR Volume IS NULL OR TargetChuteId IS NULL OR ActualChuteId IS NULL OR DischargeTime IS NULL OR Weight <> ROUND(Weight, 3))";
            if (sqlServer) {
                // THROW终止整个SQL批次，离线脚本也不会在校验失败后继续删除数据。
                migrationBuilder.Sql("IF " + unsafeRollback + " THROW 51000, N'存在Fusion处理事实或无法无损回退的数据，禁止回退。', 1;");
            }
            else {
                migrationBuilder.Sql("CREATE TEMPORARY TABLE FusionRollbackGuard (Value int NOT NULL CHECK (Value = 0));");
                migrationBuilder.Sql("INSERT INTO FusionRollbackGuard (Value) SELECT 1 WHERE " + unsafeRollback + ";");
                migrationBuilder.Sql("DROP TABLE FusionRollbackGuard;");
            }

            migrationBuilder.DropTable(
                name: "Parcel_ProcessingRecords");

            migrationBuilder.DropTable(
                name: "ParcelLocations");

            migrationBuilder.DropTable(
                name: "ParcelPartitionCatalog");

            migrationBuilder.DropTable(
                name: "ParcelProcessingReceipts");

            migrationBuilder.DropColumn(
                name: "ActualChuteCode",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "DetectedTime",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "IsFallbackChuteAssigned",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "IsRoutingBlocked",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "MeasurementTime",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "SourceExceptionCode",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "SourceInstanceId",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "SourceParcelId",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "SourceRunId",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "TargetChuteCode",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "TaskCode",
                table: "Parcels");

            migrationBuilder.DropColumn(
                name: "VolumetricWeightGrams",
                table: "Parcels");

            migrationBuilder.AlterColumn<decimal>(
                name: "Width",
                table: "Parcels",
                type: ResolveColumnType("decimal(18,3)", migrationBuilder),
                precision: 18,
                scale: 3,
                nullable: false,                oldClrType: typeof(decimal),
                oldType: ResolveColumnType("decimal(18,3)", migrationBuilder),
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Weight",
                table: "Parcels",
                type: ResolveColumnType("decimal(18,3)", migrationBuilder),
                precision: 18,
                scale: 3,
                nullable: false,                oldClrType: typeof(decimal),
                oldType: ResolveColumnType("decimal(21,6)", migrationBuilder),
                oldPrecision: 21,
                oldScale: 6,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Volume",
                table: "Parcels",
                type: ResolveColumnType("decimal(18,3)", migrationBuilder),
                precision: 18,
                scale: 3,
                nullable: false,                oldClrType: typeof(decimal),
                oldType: ResolveColumnType("decimal(18,3)", migrationBuilder),
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "TargetChuteId",
                table: "Parcels",
                type: ResolveColumnType("bigint", migrationBuilder),
                nullable: false,                oldClrType: typeof(long),
                oldType: ResolveColumnType("bigint", migrationBuilder),
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Length",
                table: "Parcels",
                type: ResolveColumnType("decimal(18,3)", migrationBuilder),
                precision: 18,
                scale: 3,
                nullable: false,                oldClrType: typeof(decimal),
                oldType: ResolveColumnType("decimal(18,3)", migrationBuilder),
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Height",
                table: "Parcels",
                type: ResolveColumnType("decimal(18,3)", migrationBuilder),
                precision: 18,
                scale: 3,
                nullable: false,                oldClrType: typeof(decimal),
                oldType: ResolveColumnType("decimal(18,3)", migrationBuilder),
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "DischargeTime",
                table: "Parcels",
                type: ResolveColumnType("datetime(6)", migrationBuilder),
                nullable: false,                oldClrType: typeof(DateTime),
                oldType: ResolveColumnType("datetime(6)", migrationBuilder),
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "ActualChuteId",
                table: "Parcels",
                type: ResolveColumnType("bigint", migrationBuilder),
                nullable: false,                oldClrType: typeof(long),
                oldType: ResolveColumnType("bigint", migrationBuilder),
                oldNullable: true);
        }
        /// <summary>统一解析本次迁移的提供器列类型，SQL Server保留Unicode内容。</summary>
        private static string ResolveColumnType(string type, MigrationBuilder builder) {
            if (!MigrationSchemaResolver.IsSqlServer(builder)) return type;
            if (type == "datetime(6)") return "datetime2(6)";
            if (type == "tinyint(1)") return "bit";
            if (type == "longtext") return "nvarchar(max)";
            if (type.StartsWith("varchar(", StringComparison.Ordinal)) return "n" + type;
            return type;
        }

    }
}
