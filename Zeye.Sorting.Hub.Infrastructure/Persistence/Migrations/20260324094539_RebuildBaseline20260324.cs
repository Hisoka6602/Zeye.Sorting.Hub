using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RebuildBaseline20260324 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Bags",
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
                name: "WebRequestAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TraceId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CorrelationId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SpanId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OperationName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestMethod = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestScheme = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestHost = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestPort = table.Column<int>(type: "int", nullable: true),
                    RequestPath = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestRouteTemplate = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    UserName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsAuthenticated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    RequestPayloadType = table.Column<int>(type: "int", nullable: false),
                    RequestSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    HasRequestBody = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsRequestBodyTruncated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ResponsePayloadType = table.Column<int>(type: "int", nullable: false),
                    ResponseSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    HasResponseBody = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsResponseBodyTruncated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    IsSuccess = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    HasException = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AuditResourceType = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebRequestAuditLogs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    ParcelTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExceptionType = table.Column<int>(type: "int", nullable: true),
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
                        principalTable: "Bags",
                        principalColumn: "BagId",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WebRequestAuditLogDetails",
                columns: table => new
                {
                    WebRequestAuditLogId = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RequestUrl = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestQueryString = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestHeadersJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseHeadersJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestContentType = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseContentType = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Accept = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Referer = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Origin = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AuthorizationType = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserAgent = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestBody = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseBody = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurlCommand = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExceptionType = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExceptionStackTrace = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileMetadataJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HasFileAccess = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FileOperationType = table.Column<int>(type: "int", nullable: false),
                    FileCount = table.Column<int>(type: "int", nullable: false),
                    FileTotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    ImageMetadataJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HasImageAccess = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ImageCount = table.Column<int>(type: "int", nullable: false),
                    DatabaseOperationSummary = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HasDatabaseAccess = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DatabaseAccessCount = table.Column<int>(type: "int", nullable: false),
                    DatabaseDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ResourceCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResourceName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ActionDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    MiddlewareDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Tags = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExtraPropertiesJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remark = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebRequestAuditLogDetails", x => x.WebRequestAuditLogId);
                    table.ForeignKey(
                        name: "FK_WebRequestAuditLogDetails_WebRequestAuditLogs_WebRequestAudi~",
                        column: x => x.WebRequestAuditLogId,
                        principalTable: "WebRequestAuditLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_ApiRequests",
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
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_BarCodeInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    BarCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BarCodeType = table.Column<int>(type: "int", nullable: false),
                    CapturedTime = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_BarCodeInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_BarCodeInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_ChuteInfos",
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
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_CommandInfos",
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
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_DeviceInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    WorkstationName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MachineCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DeviceInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_DeviceInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_GrayDetectorInfos",
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
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_ImageInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    CameraName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CustomName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CameraSerialNumber = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImageType = table.Column<int>(type: "int", nullable: false),
                    RelativePath = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CaptureType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_ImageInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_ImageInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_PositionInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    X1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    X2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Y1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Y2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BackgroundX1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BackgroundX2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BackgroundY1 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    BackgroundY2 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_PositionInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_PositionInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_SorterCarrierInfos",
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
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_StickingParcelInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    IsSticking = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ReceiveTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RawData = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ElapsedMilliseconds = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_StickingParcelInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_StickingParcelInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_VideoInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    NvrSerialNumber = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NodeType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_VideoInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_VideoInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_VolumeInfos",
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
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Parcel_WeightInfos",
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
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Bags_BagCode",
                table: "Bags",
                column: "BagCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bags_ChuteId",
                table: "Bags",
                column: "ChuteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ApiRequests_ApiType",
                table: "Parcel_ApiRequests",
                column: "ApiType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ApiRequests_ParcelId",
                table: "Parcel_ApiRequests",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ApiRequests_RequestTime",
                table: "Parcel_ApiRequests",
                column: "RequestTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode_ParcelId",
                table: "Parcel_BarCodeInfos",
                columns: new[] { "BarCode", "ParcelId" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_CapturedTime",
                table: "Parcel_BarCodeInfos",
                column: "CapturedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_ParcelId",
                table: "Parcel_BarCodeInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ChuteInfos_ActualChuteId",
                table: "Parcel_ChuteInfos",
                column: "ActualChuteId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ChuteInfos_ParcelId",
                table: "Parcel_ChuteInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ChuteInfos_TargetChuteId",
                table: "Parcel_ChuteInfos",
                column: "TargetChuteId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_CommandInfos_ActionType",
                table: "Parcel_CommandInfos",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_CommandInfos_GeneratedTime",
                table: "Parcel_CommandInfos",
                column: "GeneratedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_CommandInfos_ParcelId",
                table: "Parcel_CommandInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DeviceInfos_MachineCode",
                table: "Parcel_DeviceInfos",
                column: "MachineCode");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DeviceInfos_ParcelId",
                table: "Parcel_DeviceInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_GrayDetectorInfos_CarrierNumber",
                table: "Parcel_GrayDetectorInfos",
                column: "CarrierNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_GrayDetectorInfos_ParcelId",
                table: "Parcel_GrayDetectorInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_ImageType",
                table: "Parcel_ImageInfos",
                column: "ImageType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_ParcelId",
                table: "Parcel_ImageInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_PositionInfos_ParcelId",
                table: "Parcel_PositionInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_SorterCarrierInfos_ParcelId",
                table: "Parcel_SorterCarrierInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_SorterCarrierInfos_SorterCarrierId",
                table: "Parcel_SorterCarrierInfos",
                column: "SorterCarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_StickingParcelInfos_ParcelId",
                table: "Parcel_StickingParcelInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VideoInfos_NodeType",
                table: "Parcel_VideoInfos",
                column: "NodeType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VideoInfos_NvrSerialNumber",
                table: "Parcel_VideoInfos",
                column: "NvrSerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VideoInfos_ParcelId",
                table: "Parcel_VideoInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VolumeInfos_ParcelId",
                table: "Parcel_VolumeInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_WeightInfos_ParcelId",
                table: "Parcel_WeightInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_WeightInfos_WeighingTime",
                table: "Parcel_WeightInfos",
                column: "WeighingTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_DischargeTime",
                table: "Parcels",
                columns: new[] { "ActualChuteId", "DischargeTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime",
                table: "Parcels",
                columns: new[] { "ActualChuteId", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagCode_ScannedTime",
                table: "Parcels",
                columns: new[] { "BagCode", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagId",
                table: "Parcels",
                column: "BagId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedTime",
                table: "Parcels",
                column: "CreatedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_NoReadType_ScannedTime",
                table: "Parcels",
                columns: new[] { "NoReadType", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ParcelTimestamp",
                table: "Parcels",
                column: "ParcelTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime",
                table: "Parcels",
                columns: new[] { "RequestStatus", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ScannedTime",
                table: "Parcels",
                column: "ScannedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime",
                table: "Parcels",
                columns: new[] { "Status", "ExceptionType", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ScannedTime",
                table: "Parcels",
                columns: new[] { "Status", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime",
                table: "Parcels",
                columns: new[] { "TargetChuteId", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime",
                table: "Parcels",
                columns: new[] { "WorkstationName", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogDetails_StartedAt",
                table: "WebRequestAuditLogDetails",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_AuditResourceType_ResourceId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "AuditResourceType", "ResourceId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_CorrelationId",
                table: "WebRequestAuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_IsSuccess_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "IsSuccess", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_OperationName_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "OperationName", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_RequestPath_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "RequestPath", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_StartedAt",
                table: "WebRequestAuditLogs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_StatusCode_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "StatusCode", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_TenantId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "TenantId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_TraceId",
                table: "WebRequestAuditLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_UserId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "UserId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Parcel_ApiRequests");

            migrationBuilder.DropTable(
                name: "Parcel_BarCodeInfos");

            migrationBuilder.DropTable(
                name: "Parcel_ChuteInfos");

            migrationBuilder.DropTable(
                name: "Parcel_CommandInfos");

            migrationBuilder.DropTable(
                name: "Parcel_DeviceInfos");

            migrationBuilder.DropTable(
                name: "Parcel_GrayDetectorInfos");

            migrationBuilder.DropTable(
                name: "Parcel_ImageInfos");

            migrationBuilder.DropTable(
                name: "Parcel_PositionInfos");

            migrationBuilder.DropTable(
                name: "Parcel_SorterCarrierInfos");

            migrationBuilder.DropTable(
                name: "Parcel_StickingParcelInfos");

            migrationBuilder.DropTable(
                name: "Parcel_VideoInfos");

            migrationBuilder.DropTable(
                name: "Parcel_VolumeInfos");

            migrationBuilder.DropTable(
                name: "Parcel_WeightInfos");

            migrationBuilder.DropTable(
                name: "WebRequestAuditLogDetails");

            migrationBuilder.DropTable(
                name: "Parcels");

            migrationBuilder.DropTable(
                name: "WebRequestAuditLogs");

            migrationBuilder.DropTable(
                name: "Bags");
        }
    }
}
