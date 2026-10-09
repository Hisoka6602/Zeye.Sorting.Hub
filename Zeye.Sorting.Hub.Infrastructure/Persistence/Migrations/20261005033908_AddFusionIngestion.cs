using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFusionIngestion : Migration
    {
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedProjectionStateNextProjectionAtColumns = new[] { "ProjectionState", "NextProjectionAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdJournalIdRecordIdColumns = new[] { "SourceInstanceId", "JournalId", "RecordId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdJournalIdSourceSequenceColumns = new[] { "SourceInstanceId", "JournalId", "SourceSequence" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedIsStoredModifiedAtColumns = new[] { "IsStored", "ModifiedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdSourceImageIdColumns = new[] { "SourceInstanceId", "SourceImageId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdJournalIdColumns = new[] { "SourceInstanceId", "JournalId" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FusionFactReceipts",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceInstanceId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JournalId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RecordId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceSequence = table.Column<long>(type: "bigint", nullable: false),
                    BodySha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BodyJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectionJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kind = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReceivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TenantId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoragePartitionId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectionState = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectionAttempts = table.Column<int>(type: "int", nullable: false),
                    NextProjectionAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ProjectionClaimId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProjectionClaimUntil = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProjectionError = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ParcelId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionFactReceipts", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FusionImageUploads",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceInstanceId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceImageId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UploadId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FileName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentSha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NextOffset = table.Column<long>(type: "bigint", nullable: false),
                    IsStored = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SourceRunId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    CameraName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionImageUploads", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FusionJournalHeartbeats",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceInstanceId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JournalId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReceivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PendingFacts = table.Column<long>(type: "bigint", nullable: false),
                    RejectedFacts = table.Column<long>(type: "bigint", nullable: false),
                    PendingImages = table.Column<long>(type: "bigint", nullable: false),
                    DroppedUnacknowledgedFacts = table.Column<long>(type: "bigint", nullable: false),
                    DroppedUnacknowledgedImages = table.Column<long>(type: "bigint", nullable: false),
                    ProtectUnacknowledgedData = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RetainedBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionJournalHeartbeats", x => x.Key);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FusionSourceLeases",
                columns: table => new
                {
                    SourceInstanceId = table.Column<string>(type: "varchar(96)", maxLength: 96, nullable: false, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    JournalId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ConnectionId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LeaseId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ServerId = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpiresAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionSourceLeases", x => x.SourceInstanceId);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FusionFactReceipts_ProjectionState_NextProjectionAt",
                table: "FusionFactReceipts",
                columns: CachedProjectionStateNextProjectionAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_FusionFactReceipts_SourceInstanceId_JournalId_RecordId",
                table: "FusionFactReceipts",
                columns: CachedSourceInstanceIdJournalIdRecordIdColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FusionFactReceipts_SourceInstanceId_JournalId_SourceSequence",
                table: "FusionFactReceipts",
                columns: CachedSourceInstanceIdJournalIdSourceSequenceColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FusionImageUploads_IsStored_ModifiedAt",
                table: "FusionImageUploads",
                columns: CachedIsStoredModifiedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_FusionImageUploads_SourceInstanceId_SourceImageId",
                table: "FusionImageUploads",
                columns: CachedSourceInstanceIdSourceImageIdColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FusionImageUploads_UploadId",
                table: "FusionImageUploads",
                column: "UploadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FusionJournalHeartbeats_SourceInstanceId_JournalId",
                table: "FusionJournalHeartbeats",
                columns: CachedSourceInstanceIdJournalIdColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FusionFactReceipts");

            migrationBuilder.DropTable(
                name: "FusionImageUploads");

            migrationBuilder.DropTable(
                name: "FusionJournalHeartbeats");

            migrationBuilder.DropTable(
                name: "FusionSourceLeases");
        }
    }
}
