using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Zeye.Sorting.Hub.Infrastructure.Persistence;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (MigrationSchemaResolver.IsSqlServer(migrationBuilder))
            {
                migrationBuilder.EnsureSchema(
                    name: MigrationSchemaResolver.SqlServerDefaultSchema);
            }

            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Bags",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    BagId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ChuteId = table.Column<long>(type: "bigint", nullable: false),
                    ChuteName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BagCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParcelCount = table.Column<int>(type: "int", nullable: false),
                    BaggingTime = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bags", x => x.BagId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcels",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ParcelTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NoReadType = table.Column<int>(type: "int", nullable: false),
                    SorterCarrierId = table.Column<int>(type: "int", nullable: true),
                    SegmentCodes = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LifecycleMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    TargetChuteId = table.Column<long>(type: "bigint", nullable: false),
                    ActualChuteId = table.Column<long>(type: "bigint", nullable: false),
                    BarCodes = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Weight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RequestStatus = table.Column<int>(type: "int", nullable: false),
                    BagCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WorkstationName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsSticking = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Length = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Width = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Height = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Volume = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ScannedTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DischargeTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    HasImages = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HasVideos = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Coordinate = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BagId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ModifyTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ModifyIp = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcels_Bags_BagId",
                        column: x => x.BagId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Bags",
                        principalColumn: "BagId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_ApiRequests",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ApiType = table.Column<int>(type: "int", nullable: false),
                    RequestStatus = table.Column<int>(type: "int", nullable: false),
                    RequestUrl = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    QueryParams = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Headers = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestBody = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseBody = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResponseTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ElapsedMilliseconds = table.Column<int>(type: "int", nullable: false),
                    Exception = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RawData = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FormattedMessage = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_ApiRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_ApiRequests_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_BarCodeInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BarCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BarCodeType = table.Column<int>(type: "int", nullable: false),
                    CapturedTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_BarCodeInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_BarCodeInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_ChuteInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TargetChuteId = table.Column<long>(type: "bigint", nullable: true),
                    ActualChuteId = table.Column<long>(type: "bigint", nullable: true),
                    BackupChuteId = table.Column<long>(type: "bigint", nullable: true),
                    LandedTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_ChuteInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_ChuteInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_CommandInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ProtocolType = table.Column<int>(type: "int", nullable: false),
                    ProtocolName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConnectionName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CommandPayload = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeneratedTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    FormattedMessage = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_CommandInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_CommandInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_DeviceInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WorkstationName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MachineCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DeviceInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_DeviceInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_GrayDetectorInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CarrierNumber = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AttachBoxInfo = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MainBoxInfo = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LinkedCarrierCount = table.Column<int>(type: "int", nullable: false),
                    CenterPosition = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResultTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RawResult = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_GrayDetectorInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_GrayDetectorInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_ImageInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CameraName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CameraSerialNumber = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageType = table.Column<int>(type: "int", nullable: false),
                    RelativePath = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CaptureType = table.Column<int>(type: "int", nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_ImageInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_ImageInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_PositionInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    X1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    X2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Y1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Y2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BackgroundX1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BackgroundX2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BackgroundY1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BackgroundY2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_PositionInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_PositionInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_SorterCarrierInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SorterCarrierId = table.Column<int>(type: "int", nullable: false),
                    LoadedTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ConveyorSpeedWhenLoaded = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    LinkedCarrierCount = table.Column<int>(type: "int", nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_SorterCarrierInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_SorterCarrierInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_StickingParcelInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IsSticking = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ReceiveTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RawData = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ElapsedMilliseconds = table.Column<int>(type: "int", nullable: true),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_StickingParcelInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_StickingParcelInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_VideoInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    NvrSerialNumber = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NodeType = table.Column<int>(type: "int", nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_VideoInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_VideoInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_VolumeInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    RawVolume = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvidenceCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FormattedLength = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FormattedWidth = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FormattedHeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FormattedVolume = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AdjustedLength = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AdjustedWidth = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AdjustedHeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AdjustedVolume = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    MeasurementTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    BindTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_VolumeInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_VolumeInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_WeightInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RawWeight = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EvidenceCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FormattedWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    WeighingTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AdjustedWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_WeightInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_WeightInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Bags_BagCode",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Bags",
                column: "BagCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bags_ChuteId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Bags",
                column: "ChuteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ApiRequests_ApiType",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_ApiRequests",
                column: "ApiType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ApiRequests_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_ApiRequests",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ApiRequests_RequestTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_ApiRequests",
                column: "RequestTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos",
                column: "BarCode");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_CapturedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos",
                column: "CapturedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_BarCodeInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ChuteInfos_ActualChuteId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_ChuteInfos",
                column: "ActualChuteId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ChuteInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_ChuteInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ChuteInfos_TargetChuteId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_ChuteInfos",
                column: "TargetChuteId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_CommandInfos_ActionType",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_CommandInfos",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_CommandInfos_GeneratedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_CommandInfos",
                column: "GeneratedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_CommandInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_CommandInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DeviceInfos_MachineCode",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_DeviceInfos",
                column: "MachineCode");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DeviceInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_DeviceInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_GrayDetectorInfos_CarrierNumber",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_GrayDetectorInfos",
                column: "CarrierNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_GrayDetectorInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_GrayDetectorInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_ImageType",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_ImageInfos",
                column: "ImageType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_ImageInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_PositionInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_PositionInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_SorterCarrierInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_SorterCarrierInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_SorterCarrierInfos_SorterCarrierId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_SorterCarrierInfos",
                column: "SorterCarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_StickingParcelInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_StickingParcelInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VideoInfos_NodeType",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_VideoInfos",
                column: "NodeType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VideoInfos_NvrSerialNumber",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_VideoInfos",
                column: "NvrSerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VideoInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_VideoInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VolumeInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_VolumeInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_WeightInfos_ParcelId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_WeightInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_WeightInfos_WeighingTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcel_WeightInfos",
                column: "WeighingTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagCode",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                column: "BagCode");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagId",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                column: "BagId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ParcelTimestamp",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                column: "ParcelTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ScannedTime",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                column: "ScannedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder),
                table: "Parcels",
                column: "WorkstationName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Parcel_ApiRequests",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_BarCodeInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_ChuteInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_CommandInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_DeviceInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_GrayDetectorInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_ImageInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_PositionInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_SorterCarrierInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_StickingParcelInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_VideoInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_VolumeInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcel_WeightInfos",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Parcels",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));

            migrationBuilder.DropTable(
                name: "Bags",
                schema: MigrationSchemaResolver.ResolveSchema(migrationBuilder));
        }
    }

}
