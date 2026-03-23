using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebRequestAuditLogHotColdTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "WebRequestAuditLogDetails",
                columns: table => new
                {
                    WebRequestAuditLogId = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RequestUrl = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestQueryString = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestHeadersJson = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseHeadersJson = table.Column<string>(nullable: false)
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
                    RequestBody = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseBody = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurlCommand = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorMessage = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExceptionType = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErrorCode = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExceptionStackTrace = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileMetadataJson = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HasFileAccess = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    FileOperationType = table.Column<int>(type: "int", nullable: false),
                    FileCount = table.Column<int>(type: "int", nullable: false),
                    FileTotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    ImageMetadataJson = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    HasImageAccess = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ImageCount = table.Column<int>(type: "int", nullable: false),
                    DatabaseOperationSummary = table.Column<string>(nullable: false)
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
                    Tags = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExtraPropertiesJson = table.Column<string>(nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Remark = table.Column<string>(nullable: false)
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
                name: "WebRequestAuditLogDetails");

            migrationBuilder.DropTable(
                name: "WebRequestAuditLogs");
        }
    }
}
