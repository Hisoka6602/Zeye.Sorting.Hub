using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqliteSchema : Migration
    {
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusCreatedAtIdColumns = new[] { "Status", "CreatedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedTaskTypeCreatedAtIdColumns = new[] { "TaskType", "CreatedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedProjectionStateNextProjectionAtColumns = new[] { "ProjectionState", "NextProjectionAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdJournalIdRecordIdColumns = new[] { "SourceInstanceId", "JournalId", "RecordId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdJournalIdSourceSequenceColumns = new[] { "SourceInstanceId", "JournalId", "SourceSequence" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedProjectionStateReceivedAtSourceSequenceNextProjectionAtColumns = new[] { "ProjectionState", "ReceivedAt", "SourceSequence", "NextProjectionAt", "ProjectionClaimUntil" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdProjectionStateProjectionErrorColumns = new[] { "SourceInstanceId", "ProjectionState", "ProjectionError" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedIsStoredModifiedAtColumns = new[] { "IsStored", "ModifiedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdSourceImageIdColumns = new[] { "SourceInstanceId", "SourceImageId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdJournalIdColumns = new[] { "SourceInstanceId", "JournalId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceSystemOperationNameBusinessKeyPayloadHashColumns = new[] { "SourceSystem", "OperationName", "BusinessKey", "PayloadHash" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusCreatedAtColumns = new[] { "Status", "CreatedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedExpiresAtStatusIdColumns = new[] { "ExpiresAt", "Status", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceSystemMessageIdColumns = new[] { "SourceSystem", "MessageId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedBarCodeParcelIdColumns = new[] { "BarCode", "ParcelId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedBucketNameObjectKeyColumns = new[] { "BucketName", "ObjectKey" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedMessageIdentityReceivedAtColumns = new[] { "MessageIdentity", "ReceivedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedOccurredAtIsSuccessParcelIdStageColumns = new[] { "OccurredAt", "IsSuccess", "ParcelId", "Stage" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedParcelIdOccurredAtColumns = new[] { "ParcelId", "OccurredAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedParcelIdRecordedAtKeyColumns = new[] { "ParcelId", "RecordedAt", "Key" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly bool[] CachedFalseTrueFalseDescending = new[] { false, true, false };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceInstanceIdSourceRunIdSourceParcelIdColumns = new[] { "SourceInstanceId", "SourceRunId", "SourceParcelId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedParcelIdRecordedAtColumns = new[] { "ParcelId", "RecordedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedActualChuteIdDischargeTimeColumns = new[] { "ActualChuteId", "DischargeTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedActualChuteIdScannedTimeIdColumns = new[] { "ActualChuteId", "ScannedTime", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedBagCodeScannedTimeIdColumns = new[] { "BagCode", "ScannedTime", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedCompletedTimeStatusSourceParcelIdDetectedTimeColumns = new[] { "CompletedTime", "Status", "SourceParcelId", "DetectedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedCreatedTimeIdSourceParcelIdDetectedTimeColumns = new[] { "CreatedTime", "Id", "SourceParcelId", "DetectedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedNoReadTypeScannedTimeIdColumns = new[] { "NoReadType", "ScannedTime", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedRequestStatusScannedTimeIdColumns = new[] { "RequestStatus", "ScannedTime", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedScannedTimeIdColumns = new[] { "ScannedTime", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusExceptionTypeScannedTimeIdColumns = new[] { "Status", "ExceptionType", "ScannedTime", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusScannedTimeIdColumns = new[] { "Status", "ScannedTime", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedTargetChuteIdScannedTimeIdColumns = new[] { "TargetChuteId", "ScannedTime", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedWorkstationNameScannedTimeIdColumns = new[] { "WorkstationName", "ScannedTime", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedAuditResourceTypeResourceIdStartedAtIdColumns = new[] { "AuditResourceType", "ResourceId", "StartedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedIsSuccessStartedAtIdColumns = new[] { "IsSuccess", "StartedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedOperationNameStartedAtIdColumns = new[] { "OperationName", "StartedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedRequestPathStartedAtIdColumns = new[] { "RequestPath", "StartedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStartedAtIdColumns = new[] { "StartedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusCodeStartedAtIdColumns = new[] { "StatusCode", "StartedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedTenantIdStartedAtIdColumns = new[] { "TenantId", "StartedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedUserIdStartedAtIdColumns = new[] { "UserId", "StartedAt", "Id" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchiveTasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TaskType = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDryRun = table.Column<bool>(type: "INTEGER", nullable: false),
                    RetentionDays = table.Column<int>(type: "INTEGER", nullable: false),
                    PlannedItemCount = table.Column<long>(type: "INTEGER", nullable: false),
                    ProcessedItemCount = table.Column<long>(type: "INTEGER", nullable: false),
                    RequestedBy = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    PlanSummary = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CheckpointPayload = table.Column<string>(type: "TEXT", nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAttemptedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bags",
                columns: table => new
                {
                    BagId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ChuteId = table.Column<long>(type: "INTEGER", nullable: false),
                    ChuteName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    BagCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ParcelCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BaggingTime = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bags", x => x.BagId);
                });

            migrationBuilder.CreateTable(
                name: "FusionFactReceipts",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceInstanceId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    JournalId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RecordId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    SourceSequence = table.Column<long>(type: "INTEGER", nullable: false),
                    BodySha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    BodyJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectionJson = table.Column<string>(type: "TEXT", nullable: true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TenantId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    StoragePartitionId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    ProjectionState = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ProjectionAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    NextProjectionAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProjectionClaimId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    ProjectionClaimUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProjectionError = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ParcelId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionFactReceipts", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "FusionImageUploads",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceInstanceId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    SourceImageId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UploadId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ContentSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    NextOffset = table.Column<long>(type: "INTEGER", nullable: false),
                    IsStored = table.Column<bool>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SourceRunId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    SourceParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    CameraName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionImageUploads", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "FusionJournalHeartbeats",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SourceInstanceId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    JournalId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SentAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PendingFacts = table.Column<long>(type: "INTEGER", nullable: false),
                    RejectedFacts = table.Column<long>(type: "INTEGER", nullable: false),
                    PendingImages = table.Column<long>(type: "INTEGER", nullable: false),
                    DroppedUnacknowledgedFacts = table.Column<long>(type: "INTEGER", nullable: false),
                    DroppedUnacknowledgedImages = table.Column<long>(type: "INTEGER", nullable: false),
                    ProtectUnacknowledgedData = table.Column<bool>(type: "INTEGER", nullable: false),
                    RetainedBytes = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionJournalHeartbeats", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "FusionSourceLeases",
                columns: table => new
                {
                    SourceInstanceId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    JournalId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ConnectionId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LeaseId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ServerId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FusionSourceLeases", x => x.SourceInstanceId);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceSystem = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OperationName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    BusinessKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceSystem = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MessageId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastAttemptedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ManagedDocuments",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Json = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManagedDocuments", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_ProcessingRecords",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    WorkstationName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PreviousCreationGapMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    IsSpacingViolation = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsAwaitingWcsDecision = table.Column<bool>(type: "INTEGER", nullable: true),
                    RecordId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SourceInstanceId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    SourceRunId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: false),
                    SourceParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    Stage = table.Column<int>(type: "INTEGER", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PartitionTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsSuccess = table.Column<bool>(type: "INTEGER", nullable: true),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Barcode = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    BarcodesJson = table.Column<string>(type: "TEXT", nullable: true),
                    WeightGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    LengthMm = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    WidthMm = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    HeightMm = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    VolumeMm3 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    VolumetricWeightGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MeasuredAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasReliableTimestamp = table.Column<bool>(type: "INTEGER", nullable: true),
                    HasReliableFrameBoundary = table.Column<bool>(type: "INTEGER", nullable: true),
                    CorrelationId = table.Column<long>(type: "INTEGER", nullable: true),
                    TriggerBatch = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ScanSequence = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    MessageIdentity = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    BindingMode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    CandidateSourceParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    FinalSourceParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    DeltaMilliseconds = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    DecisionReason = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    FifoRecoveryMode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Provider = table.Column<string>(type: "TEXT", maxLength: 96, nullable: true),
                    TaskCode = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    TargetChuteCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    DispatchedChuteCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ActualChuteCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IsFallback = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsRoutingBlocked = table.Column<bool>(type: "INTEGER", nullable: true),
                    ExceptionCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    RawPayload = table.Column<string>(type: "TEXT", nullable: true),
                    RequestUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    RequestHeaders = table.Column<string>(type: "TEXT", nullable: true),
                    RequestBody = table.Column<string>(type: "TEXT", nullable: true),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "INTEGER", nullable: true),
                    RequestAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResponseAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ElapsedMilliseconds = table.Column<int>(type: "INTEGER", nullable: true),
                    ImagePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ImageCamera = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ImageContentHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_ProcessingRecords", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ParcelLocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Suffix = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParcelPartitionCatalog",
                columns: table => new
                {
                    Suffix = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Start = table.Column<DateTime>(type: "TEXT", nullable: false),
                    End = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelPartitionCatalog", x => x.Suffix);
                });

            migrationBuilder.CreateTable(
                name: "ParcelProcessingReceipts",
                columns: table => new
                {
                    Key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    Suffix = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelProcessingReceipts", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "WebRequestAuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TraceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    SpanId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    OperationName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RequestMethod = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    RequestScheme = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    RequestHost = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    RequestPort = table.Column<int>(type: "INTEGER", nullable: true),
                    RequestPath = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    RequestRouteTemplate = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    UserId = table.Column<long>(type: "INTEGER", nullable: true),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsAuthenticated = table.Column<bool>(type: "INTEGER", nullable: false),
                    TenantId = table.Column<long>(type: "INTEGER", nullable: true),
                    RequestPayloadType = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    HasRequestBody = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRequestBodyTruncated = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResponsePayloadType = table.Column<int>(type: "INTEGER", nullable: false),
                    ResponseSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    HasResponseBody = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsResponseBodyTruncated = table.Column<bool>(type: "INTEGER", nullable: false),
                    StatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSuccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasException = table.Column<bool>(type: "INTEGER", nullable: false),
                    AuditResourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    ResourceId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebRequestAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parcels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceInstanceId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: true),
                    SourceRunId = table.Column<string>(type: "TEXT", maxLength: 96, nullable: true),
                    SourceParcelId = table.Column<long>(type: "INTEGER", nullable: true),
                    DetectedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MeasurementTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TargetChuteCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ActualChuteCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    TaskCode = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    VolumetricWeightGrams = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    IsFallbackChuteAssigned = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsRoutingBlocked = table.Column<bool>(type: "INTEGER", nullable: true),
                    SourceExceptionCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ParcelTimestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ExceptionType = table.Column<int>(type: "INTEGER", nullable: true),
                    NoReadType = table.Column<int>(type: "INTEGER", nullable: false),
                    SorterCarrierId = table.Column<long>(type: "INTEGER", nullable: true),
                    SegmentCodes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    LifecycleMilliseconds = table.Column<long>(type: "INTEGER", nullable: true),
                    TargetChuteId = table.Column<long>(type: "INTEGER", nullable: true),
                    ActualChuteId = table.Column<long>(type: "INTEGER", nullable: true),
                    BarCodes = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Weight = table.Column<decimal>(type: "TEXT", precision: 21, scale: 6, nullable: true),
                    RequestStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    BagCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    WorkstationName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    IsSticking = table.Column<bool>(type: "INTEGER", nullable: false),
                    Length = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    Width = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    Height = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    Volume = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    ScannedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DischargeTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasImages = table.Column<bool>(type: "INTEGER", nullable: false),
                    HasVideos = table.Column<bool>(type: "INTEGER", nullable: false),
                    Coordinate = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    BagId = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifyTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifyIp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "WebRequestAuditLogDetails",
                columns: table => new
                {
                    WebRequestAuditLogId = table.Column<long>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RequestUrl = table.Column<string>(type: "TEXT", nullable: false),
                    RequestQueryString = table.Column<string>(type: "TEXT", nullable: false),
                    RequestHeadersJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseHeadersJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequestContentType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ResponseContentType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Accept = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Referer = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Origin = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    AuthorizationType = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    RequestBody = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: false),
                    CurlCommand = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    ExceptionType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ErrorCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ExceptionStackTrace = table.Column<string>(type: "TEXT", nullable: false),
                    FileMetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    HasFileAccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    FileOperationType = table.Column<int>(type: "INTEGER", nullable: false),
                    FileCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FileTotalBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    ImageMetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    HasImageAccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseOperationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    HasDatabaseAccess = table.Column<bool>(type: "INTEGER", nullable: false),
                    DatabaseAccessCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DatabaseDurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    ResourceCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ResourceName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    ActionDurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    MiddlewareDurationMs = table.Column<long>(type: "INTEGER", nullable: false),
                    Tags = table.Column<string>(type: "TEXT", nullable: false),
                    ExtraPropertiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebRequestAuditLogDetails", x => x.WebRequestAuditLogId);
                    table.ForeignKey(
                        name: "FK_WebRequestAuditLogDetails_WebRequestAuditLogs_WebRequestAuditLogId",
                        column: x => x.WebRequestAuditLogId,
                        principalTable: "WebRequestAuditLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_ApiRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApiType = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestUrl = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    QueryParams = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Headers = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    RequestBody = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseBody = table.Column<string>(type: "TEXT", nullable: false),
                    RequestTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResponseTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ElapsedMilliseconds = table.Column<int>(type: "INTEGER", nullable: false),
                    Exception = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    RawData = table.Column<string>(type: "TEXT", nullable: false),
                    FormattedMessage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_BarCodeInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false),
                    BarCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    BarCodeType = table.Column<int>(type: "INTEGER", nullable: false),
                    CapturedTime = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_ChuteInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TargetChuteId = table.Column<long>(type: "INTEGER", nullable: true),
                    ActualChuteId = table.Column<long>(type: "INTEGER", nullable: true),
                    BackupChuteId = table.Column<long>(type: "INTEGER", nullable: true),
                    LandedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_CommandInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProtocolType = table.Column<int>(type: "INTEGER", nullable: false),
                    ProtocolName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ConnectionName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CommandPayload = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ActionType = table.Column<int>(type: "INTEGER", nullable: false),
                    FormattedMessage = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_DeviceInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false),
                    WorkstationName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    MachineCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CustomName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_GrayDetectorInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CarrierNumber = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AttachBoxInfo = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    MainBoxInfo = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    LinkedCarrierCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CenterPosition = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ResultTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RawResult = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_ImageInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false),
                    CameraName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CustomName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    CameraSerialNumber = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ImageType = table.Column<int>(type: "INTEGER", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    StorageProvider = table.Column<int>(type: "INTEGER", nullable: true),
                    BucketName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ObjectKey = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ObjectSizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    ETag = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Sha256 = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    UploadedAtLocal = table.Column<DateTime>(type: "TEXT", nullable: true, comment: "上传完成时间（本地时间）"),
                    OriginalFileName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    CaptureType = table.Column<int>(type: "INTEGER", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_PositionInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false),
                    X1 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    X2 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    Y1 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    Y2 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    BackgroundX1 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    BackgroundX2 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    BackgroundY1 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    BackgroundY2 = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_SorterCarrierInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SorterCarrierId = table.Column<long>(type: "INTEGER", nullable: false),
                    LoadedTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ConveyorSpeedWhenLoaded = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    LinkedCarrierCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_StickingParcelInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false),
                    IsSticking = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReceiveTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RawData = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ElapsedMilliseconds = table.Column<int>(type: "INTEGER", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_VideoInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false),
                    Channel = table.Column<int>(type: "INTEGER", nullable: false),
                    NvrSerialNumber = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    NodeType = table.Column<int>(type: "INTEGER", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_VolumeInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceType = table.Column<int>(type: "INTEGER", nullable: false),
                    RawVolume = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    EvidenceCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FormattedLength = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    FormattedWidth = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    FormattedHeight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    FormattedVolume = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    AdjustedLength = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    AdjustedWidth = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    AdjustedHeight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    AdjustedVolume = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    MeasurementTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    BindTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "Parcel_WeightInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RawWeight = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    EvidenceCode = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    FormattedWeight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: false),
                    WeighingTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AdjustedWeight = table.Column<decimal>(type: "TEXT", precision: 18, scale: 3, nullable: true),
                    ParcelId = table.Column<long>(type: "INTEGER", nullable: false)
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
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_Status_CreatedAt_Id",
                table: "ArchiveTasks",
                columns: CachedStatusCreatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_TaskType_CreatedAt_Id",
                table: "ArchiveTasks",
                columns: CachedTaskTypeCreatedAtIdColumns);

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
                name: "IX_FusionFacts_ProjectionQueue",
                table: "FusionFactReceipts",
                columns: CachedProjectionStateReceivedAtSourceSequenceNextProjectionAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_FusionFacts_SourceProgress",
                table: "FusionFactReceipts",
                columns: CachedSourceInstanceIdProjectionStateProjectionErrorColumns);

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

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_SourceSystem_OperationName_BusinessKey_PayloadHash",
                table: "IdempotencyRecords",
                columns: CachedSourceSystemOperationNameBusinessKeyPayloadHashColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Status_CreatedAt",
                table: "IdempotencyRecords",
                columns: CachedStatusCreatedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ExpiresAt_Status_Id",
                table: "InboxMessages",
                columns: CachedExpiresAtStatusIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_SourceSystem_MessageId",
                table: "InboxMessages",
                columns: CachedSourceSystemMessageIdColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_CreatedAt_Id",
                table: "InboxMessages",
                columns: CachedStatusCreatedAtIdColumns);

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
                columns: CachedBarCodeParcelIdColumns);

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
                name: "IX_Parcel_ImageInfos_BucketName_ObjectKey",
                table: "Parcel_ImageInfos",
                columns: CachedBucketNameObjectKeyColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_ImageType",
                table: "Parcel_ImageInfos",
                column: "ImageType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_ParcelId",
                table: "Parcel_ImageInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_StorageProvider",
                table: "Parcel_ImageInfos",
                column: "StorageProvider");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_UploadedAtLocal",
                table: "Parcel_ImageInfos",
                column: "UploadedAtLocal");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_PositionInfos_ParcelId",
                table: "Parcel_PositionInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_MessageIdentity_ReceivedAt",
                table: "Parcel_ProcessingRecords",
                columns: CachedMessageIdentityReceivedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_OccurredAt",
                table: "Parcel_ProcessingRecords",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_OccurredAt_IsSuccess_ParcelId_Stage",
                table: "Parcel_ProcessingRecords",
                columns: CachedOccurredAtIsSuccessParcelIdStageColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_ParcelId_OccurredAt",
                table: "Parcel_ProcessingRecords",
                columns: CachedParcelIdOccurredAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_ParcelId_RecordedAt_Key",
                table: "Parcel_ProcessingRecords",
                columns: CachedParcelIdRecordedAtKeyColumns,
                descending: CachedFalseTrueFalseDescending);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_SourceInstanceId_SourceRunId_SourceParcelId",
                table: "Parcel_ProcessingRecords",
                columns: CachedSourceInstanceIdSourceRunIdSourceParcelIdColumns);

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
                name: "IX_ParcelLocations_SourceKey",
                table: "ParcelLocations",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParcelProcessingReceipts_ParcelId_RecordedAt",
                table: "ParcelProcessingReceipts",
                columns: CachedParcelIdRecordedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_DischargeTime",
                table: "Parcels",
                columns: CachedActualChuteIdDischargeTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime_Id",
                table: "Parcels",
                columns: CachedActualChuteIdScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagCode_ScannedTime_Id",
                table: "Parcels",
                columns: CachedBagCodeScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagId",
                table: "Parcels",
                column: "BagId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CompletedTime_Status_SourceParcelId_DetectedTime",
                table: "Parcels",
                columns: CachedCompletedTimeStatusSourceParcelIdDetectedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedTime",
                table: "Parcels",
                column: "CreatedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedTime_Id_SourceParcelId_DetectedTime",
                table: "Parcels",
                columns: CachedCreatedTimeIdSourceParcelIdDetectedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_NoReadType_ScannedTime_Id",
                table: "Parcels",
                columns: CachedNoReadTypeScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ParcelTimestamp",
                table: "Parcels",
                column: "ParcelTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime_Id",
                table: "Parcels",
                columns: CachedRequestStatusScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ScannedTime_Id",
                table: "Parcels",
                columns: CachedScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime_Id",
                table: "Parcels",
                columns: CachedStatusExceptionTypeScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ScannedTime_Id",
                table: "Parcels",
                columns: CachedStatusScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime_Id",
                table: "Parcels",
                columns: CachedTargetChuteIdScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime_Id",
                table: "Parcels",
                columns: CachedWorkstationNameScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogDetails_StartedAt",
                table: "WebRequestAuditLogDetails",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_AuditResourceType_ResourceId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedAuditResourceTypeResourceIdStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_CorrelationId",
                table: "WebRequestAuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_IsSuccess_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedIsSuccessStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_OperationName_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedOperationNameStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_RequestPath_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedRequestPathStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_StatusCode_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedStatusCodeStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_TenantId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedTenantIdStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_TraceId",
                table: "WebRequestAuditLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_UserId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedUserIdStartedAtIdColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveTasks");

            migrationBuilder.DropTable(
                name: "FusionFactReceipts");

            migrationBuilder.DropTable(
                name: "FusionImageUploads");

            migrationBuilder.DropTable(
                name: "FusionJournalHeartbeats");

            migrationBuilder.DropTable(
                name: "FusionSourceLeases");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "InboxMessages");

            migrationBuilder.DropTable(
                name: "ManagedDocuments");

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
                name: "Parcel_ProcessingRecords");

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
                name: "ParcelLocations");

            migrationBuilder.DropTable(
                name: "ParcelPartitionCatalog");

            migrationBuilder.DropTable(
                name: "ParcelProcessingReceipts");

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
