using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddFusionIngestionSqlServer : Migration
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
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceInstanceId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false, collation: "Latin1_General_100_BIN2"),
                    JournalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SourceSequence = table.Column<long>(type: "bigint", nullable: false),
                    BodySha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BodyJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProjectionJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    StoragePartitionId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    ProjectionState = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ProjectionAttempts = table.Column<int>(type: "int", nullable: false),
                    NextProjectionAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProjectionClaimId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ProjectionClaimUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProjectionError = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ParcelId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionFactReceipts", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "FusionImageUploads",
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceInstanceId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false, collation: "Latin1_General_100_BIN2"),
                    SourceImageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false, collation: "Latin1_General_100_BIN2"),
                    UploadId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NextOffset = table.Column<long>(type: "bigint", nullable: false),
                    IsStored = table.Column<bool>(type: "bit", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceRunId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    CameraName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionImageUploads", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "FusionJournalHeartbeats",
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceInstanceId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false, collation: "Latin1_General_100_BIN2"),
                    JournalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PendingFacts = table.Column<long>(type: "bigint", nullable: false),
                    RejectedFacts = table.Column<long>(type: "bigint", nullable: false),
                    PendingImages = table.Column<long>(type: "bigint", nullable: false),
                    DroppedUnacknowledgedFacts = table.Column<long>(type: "bigint", nullable: false),
                    DroppedUnacknowledgedImages = table.Column<long>(type: "bigint", nullable: false),
                    ProtectUnacknowledgedData = table.Column<bool>(type: "bit", nullable: false),
                    RetainedBytes = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionJournalHeartbeats", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "FusionSourceLeases",
                schema: "dbo",
                columns: table => new
                {
                    SourceInstanceId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false, collation: "Latin1_General_100_BIN2"),
                    JournalId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConnectionId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LeaseId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ServerId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionSourceLeases", x => x.SourceInstanceId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FusionFactReceipts_ProjectionState_NextProjectionAt",
                schema: "dbo",
                table: "FusionFactReceipts",
                columns: CachedProjectionStateNextProjectionAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_FusionFactReceipts_SourceInstanceId_JournalId_RecordId",
                schema: "dbo",
                table: "FusionFactReceipts",
                columns: CachedSourceInstanceIdJournalIdRecordIdColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FusionFactReceipts_SourceInstanceId_JournalId_SourceSequence",
                schema: "dbo",
                table: "FusionFactReceipts",
                columns: CachedSourceInstanceIdJournalIdSourceSequenceColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FusionImageUploads_IsStored_ModifiedAt",
                schema: "dbo",
                table: "FusionImageUploads",
                columns: CachedIsStoredModifiedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_FusionImageUploads_SourceInstanceId_SourceImageId",
                schema: "dbo",
                table: "FusionImageUploads",
                columns: CachedSourceInstanceIdSourceImageIdColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FusionImageUploads_UploadId",
                schema: "dbo",
                table: "FusionImageUploads",
                column: "UploadId",
                unique: true,
                filter: "[UploadId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FusionJournalHeartbeats_SourceInstanceId_JournalId",
                schema: "dbo",
                table: "FusionJournalHeartbeats",
                columns: CachedSourceInstanceIdJournalIdColumns,
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FusionFactReceipts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FusionImageUploads",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FusionJournalHeartbeats",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "FusionSourceLeases",
                schema: "dbo");
        }
    }
}
