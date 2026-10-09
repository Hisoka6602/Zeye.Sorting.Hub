using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServerSchema : Migration
    {
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusCreatedAtIdColumns = new[] { "Status", "CreatedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedTaskTypeCreatedAtIdColumns = new[] { "TaskType", "CreatedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceSystemOperationNameBusinessKeyPayloadHashColumns = new[] { "SourceSystem", "OperationName", "BusinessKey", "PayloadHash" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusCreatedAtColumns = new[] { "Status", "CreatedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedExpiresAtStatusIdColumns = new[] { "ExpiresAt", "Status", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedSourceSystemMessageIdColumns = new[] { "SourceSystem", "MessageId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedEventTypeCreatedAtIdColumns = new[] { "EventType", "CreatedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusLastAttemptedAtUpdatedAtIdColumns = new[] { "Status", "LastAttemptedAt", "UpdatedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedBarCodeParcelIdColumns = new[] { "BarCode", "ParcelId" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedMessageIdentityReceivedAtColumns = new[] { "MessageIdentity", "ReceivedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedParcelIdOccurredAtColumns = new[] { "ParcelId", "OccurredAt" };
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
            // 仅供空库启用；旧共享迁移历史必须先制定保留数据的过渡方案。
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.tables AS t
                    JOIN sys.schemas AS s ON s.schema_id = t.schema_id
                    WHERE NOT (s.name = N'dbo' AND t.name = N'__EFMigrationsHistory')
                ) THROW 51003, N'SQL Server专用基线只允许空库应用；已有库需先制定迁移历史过渡方案。', 1;
                """);

            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "ArchiveTasks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsDryRun = table.Column<bool>(type: "bit", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: false),
                    PlannedItemCount = table.Column<long>(type: "bigint", nullable: false),
                    ProcessedItemCount = table.Column<long>(type: "bigint", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    PlanSummary = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    CheckpointPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FailureMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Bags",
                schema: "dbo",
                columns: table => new
                {
                    BagId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChuteId = table.Column<long>(type: "bigint", nullable: false),
                    ChuteName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BagCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ParcelCount = table.Column<int>(type: "int", nullable: false),
                    BaggingTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bags", x => x.BagId);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSystem = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OperationName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BusinessKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailureMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceSystem = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    FailureMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    FailureMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_ProcessingRecords",
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WorkstationName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PreviousCreationGapMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    IsSpacingViolation = table.Column<bool>(type: "bit", nullable: true),
                    IsAwaitingWcsDecision = table.Column<bool>(type: "bit", nullable: true),
                    RecordId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SourceInstanceId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    SourceRunId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: false),
                    SourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    ParcelId = table.Column<long>(type: "bigint", nullable: true),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PartitionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: true),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    BarcodesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WeightGrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    LengthMm = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    WidthMm = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    HeightMm = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    VolumeMm3 = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    VolumetricWeightGrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasReliableTimestamp = table.Column<bool>(type: "bit", nullable: true),
                    HasReliableFrameBoundary = table.Column<bool>(type: "bit", nullable: true),
                    CorrelationId = table.Column<long>(type: "bigint", nullable: true),
                    TriggerBatch = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ScanSequence = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MessageIdentity = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BindingMode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CandidateSourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    FinalSourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    DeltaMilliseconds = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    FifoRecoveryMode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: true),
                    TaskCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TargetChuteCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    DispatchedChuteCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ActualChuteCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsFallback = table.Column<bool>(type: "bit", nullable: true),
                    IsRoutingBlocked = table.Column<bool>(type: "bit", nullable: true),
                    ExceptionCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    RawPayload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    RequestHeaders = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    RequestAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ElapsedMilliseconds = table.Column<int>(type: "int", nullable: true),
                    ImagePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ImageCamera = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ImageContentHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_ProcessingRecords", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "ParcelLocations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Suffix = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParcelPartitionCatalog",
                schema: "dbo",
                columns: table => new
                {
                    Suffix = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelPartitionCatalog", x => x.Suffix);
                });

            migrationBuilder.CreateTable(
                name: "ParcelProcessingReceipts",
                schema: "dbo",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: true),
                    Suffix = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParcelProcessingReceipts", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "WebRequestAuditLogs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TraceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SpanId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OperationName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RequestScheme = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RequestHost = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RequestPort = table.Column<int>(type: "int", nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RequestRouteTemplate = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsAuthenticated = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<long>(type: "bigint", nullable: true),
                    RequestPayloadType = table.Column<int>(type: "int", nullable: false),
                    RequestSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    HasRequestBody = table.Column<bool>(type: "bit", nullable: false),
                    IsRequestBodyTruncated = table.Column<bool>(type: "bit", nullable: false),
                    ResponsePayloadType = table.Column<int>(type: "int", nullable: false),
                    ResponseSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    HasResponseBody = table.Column<bool>(type: "bit", nullable: false),
                    IsResponseBodyTruncated = table.Column<bool>(type: "bit", nullable: false),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    HasException = table.Column<bool>(type: "bit", nullable: false),
                    AuditResourceType = table.Column<int>(type: "int", nullable: false),
                    ResourceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebRequestAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parcels",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    SourceInstanceId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: true),
                    SourceRunId = table.Column<string>(type: "nvarchar(96)", maxLength: 96, nullable: true),
                    SourceParcelId = table.Column<long>(type: "bigint", nullable: true),
                    DetectedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MeasurementTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetChuteCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ActualChuteCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    TaskCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    VolumetricWeightGrams = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    IsFallbackChuteAssigned = table.Column<bool>(type: "bit", nullable: true),
                    IsRoutingBlocked = table.Column<bool>(type: "bit", nullable: true),
                    SourceExceptionCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ParcelTimestamp = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ExceptionType = table.Column<int>(type: "int", nullable: true),
                    NoReadType = table.Column<int>(type: "int", nullable: false),
                    SorterCarrierId = table.Column<long>(type: "bigint", nullable: true),
                    SegmentCodes = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LifecycleMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    TargetChuteId = table.Column<long>(type: "bigint", nullable: true),
                    ActualChuteId = table.Column<long>(type: "bigint", nullable: true),
                    BarCodes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(21,6)", precision: 21, scale: 6, nullable: true),
                    RequestStatus = table.Column<int>(type: "int", nullable: false),
                    BagCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WorkstationName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsSticking = table.Column<bool>(type: "bit", nullable: false),
                    Length = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Width = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Height = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Volume = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ScannedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DischargeTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HasImages = table.Column<bool>(type: "bit", nullable: false),
                    HasVideos = table.Column<bool>(type: "bit", nullable: false),
                    Coordinate = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    BagId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModifyIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcels_Bags_BagId",
                        column: x => x.BagId,
                        principalSchema: "dbo",
                        principalTable: "Bags",
                        principalColumn: "BagId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WebRequestAuditLogDetails",
                schema: "dbo",
                columns: table => new
                {
                    WebRequestAuditLogId = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestQueryString = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestHeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseHeadersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestContentType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ResponseContentType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Accept = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Referer = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    AuthorizationType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CurlCommand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExceptionType = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExceptionStackTrace = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileMetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasFileAccess = table.Column<bool>(type: "bit", nullable: false),
                    FileOperationType = table.Column<int>(type: "int", nullable: false),
                    FileCount = table.Column<int>(type: "int", nullable: false),
                    FileTotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    ImageMetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasImageAccess = table.Column<bool>(type: "bit", nullable: false),
                    ImageCount = table.Column<int>(type: "int", nullable: false),
                    DatabaseOperationSummary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HasDatabaseAccess = table.Column<bool>(type: "bit", nullable: false),
                    DatabaseAccessCount = table.Column<int>(type: "int", nullable: false),
                    DatabaseDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ResourceCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ResourceName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActionDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    MiddlewareDurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtraPropertiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebRequestAuditLogDetails", x => x.WebRequestAuditLogId);
                    table.ForeignKey(
                        name: "FK_WebRequestAuditLogDetails_WebRequestAuditLogs_WebRequestAuditLogId",
                        column: x => x.WebRequestAuditLogId,
                        principalSchema: "dbo",
                        principalTable: "WebRequestAuditLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_ApiRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiType = table.Column<int>(type: "int", nullable: false),
                    RequestStatus = table.Column<int>(type: "int", nullable: false),
                    RequestUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    QueryParams = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Headers = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    RequestBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RequestTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ElapsedMilliseconds = table.Column<int>(type: "int", nullable: false),
                    Exception = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    RawData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FormattedMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_ApiRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_ApiRequests_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_BarCodeInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    BarCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    BarCodeType = table.Column<int>(type: "int", nullable: false),
                    CapturedTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_BarCodeInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_BarCodeInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_ChuteInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetChuteId = table.Column<long>(type: "bigint", nullable: true),
                    ActualChuteId = table.Column<long>(type: "bigint", nullable: true),
                    BackupChuteId = table.Column<long>(type: "bigint", nullable: true),
                    LandedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_ChuteInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_ChuteInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_CommandInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProtocolType = table.Column<int>(type: "int", nullable: false),
                    ProtocolName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ConnectionName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CommandPayload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GeneratedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    FormattedMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_CommandInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_CommandInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_DeviceInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    WorkstationName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    MachineCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CustomName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_DeviceInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_DeviceInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_GrayDetectorInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CarrierNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AttachBoxInfo = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    MainBoxInfo = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    LinkedCarrierCount = table.Column<int>(type: "int", nullable: false),
                    CenterPosition = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ResultTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RawResult = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_GrayDetectorInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_GrayDetectorInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_ImageInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    CameraName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CustomName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    CameraSerialNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ImageType = table.Column<int>(type: "int", nullable: false),
                    RelativePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    CaptureType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_ImageInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_ImageInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_PositionInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
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
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_SorterCarrierInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SorterCarrierId = table.Column<long>(type: "bigint", nullable: false),
                    LoadedTime = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_StickingParcelInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    IsSticking = table.Column<bool>(type: "bit", nullable: false),
                    ReceiveTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RawData = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ElapsedMilliseconds = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_StickingParcelInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_StickingParcelInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_VideoInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    NvrSerialNumber = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    NodeType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_VideoInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_VideoInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_VolumeInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    RawVolume = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EvidenceCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FormattedLength = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FormattedWidth = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FormattedHeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    FormattedVolume = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    AdjustedLength = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AdjustedWidth = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AdjustedHeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    AdjustedVolume = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    MeasurementTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BindTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_VolumeInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_VolumeInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Parcel_WeightInfos",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RawWeight = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EvidenceCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FormattedWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    WeighingTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AdjustedWeight = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    ParcelId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parcel_WeightInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parcel_WeightInfos_Parcels_ParcelId",
                        column: x => x.ParcelId,
                        principalSchema: "dbo",
                        principalTable: "Parcels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_Status_CreatedAt_Id",
                schema: "dbo",
                table: "ArchiveTasks",
                columns: CachedStatusCreatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_TaskType_CreatedAt_Id",
                schema: "dbo",
                table: "ArchiveTasks",
                columns: CachedTaskTypeCreatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Bags_BagCode",
                schema: "dbo",
                table: "Bags",
                column: "BagCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bags_ChuteId",
                schema: "dbo",
                table: "Bags",
                column: "ChuteId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_SourceSystem_OperationName_BusinessKey_PayloadHash",
                schema: "dbo",
                table: "IdempotencyRecords",
                columns: CachedSourceSystemOperationNameBusinessKeyPayloadHashColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Status_CreatedAt",
                schema: "dbo",
                table: "IdempotencyRecords",
                columns: CachedStatusCreatedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ExpiresAt_Status_Id",
                schema: "dbo",
                table: "InboxMessages",
                columns: CachedExpiresAtStatusIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_SourceSystem_MessageId",
                schema: "dbo",
                table: "InboxMessages",
                columns: CachedSourceSystemMessageIdColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_CreatedAt_Id",
                schema: "dbo",
                table: "InboxMessages",
                columns: CachedStatusCreatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EventType_CreatedAt_Id",
                schema: "dbo",
                table: "OutboxMessages",
                columns: CachedEventTypeCreatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_CreatedAt_Id",
                schema: "dbo",
                table: "OutboxMessages",
                columns: CachedStatusCreatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_LastAttemptedAt_UpdatedAt_Id",
                schema: "dbo",
                table: "OutboxMessages",
                columns: CachedStatusLastAttemptedAtUpdatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ApiRequests_ApiType",
                schema: "dbo",
                table: "Parcel_ApiRequests",
                column: "ApiType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ApiRequests_ParcelId",
                schema: "dbo",
                table: "Parcel_ApiRequests",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ApiRequests_RequestTime",
                schema: "dbo",
                table: "Parcel_ApiRequests",
                column: "RequestTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_BarCode_ParcelId",
                schema: "dbo",
                table: "Parcel_BarCodeInfos",
                columns: CachedBarCodeParcelIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_CapturedTime",
                schema: "dbo",
                table: "Parcel_BarCodeInfos",
                column: "CapturedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_BarCodeInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_BarCodeInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ChuteInfos_ActualChuteId",
                schema: "dbo",
                table: "Parcel_ChuteInfos",
                column: "ActualChuteId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ChuteInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_ChuteInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ChuteInfos_TargetChuteId",
                schema: "dbo",
                table: "Parcel_ChuteInfos",
                column: "TargetChuteId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_CommandInfos_ActionType",
                schema: "dbo",
                table: "Parcel_CommandInfos",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_CommandInfos_GeneratedTime",
                schema: "dbo",
                table: "Parcel_CommandInfos",
                column: "GeneratedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_CommandInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_CommandInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DeviceInfos_MachineCode",
                schema: "dbo",
                table: "Parcel_DeviceInfos",
                column: "MachineCode");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_DeviceInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_DeviceInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_GrayDetectorInfos_CarrierNumber",
                schema: "dbo",
                table: "Parcel_GrayDetectorInfos",
                column: "CarrierNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_GrayDetectorInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_GrayDetectorInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_ImageType",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                column: "ImageType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_PositionInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_PositionInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_MessageIdentity_ReceivedAt",
                schema: "dbo",
                table: "Parcel_ProcessingRecords",
                columns: CachedMessageIdentityReceivedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_OccurredAt",
                schema: "dbo",
                table: "Parcel_ProcessingRecords",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_ParcelId_OccurredAt",
                schema: "dbo",
                table: "Parcel_ProcessingRecords",
                columns: CachedParcelIdOccurredAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_SourceInstanceId_SourceRunId_SourceParcelId",
                schema: "dbo",
                table: "Parcel_ProcessingRecords",
                columns: CachedSourceInstanceIdSourceRunIdSourceParcelIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_SorterCarrierInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_SorterCarrierInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_SorterCarrierInfos_SorterCarrierId",
                schema: "dbo",
                table: "Parcel_SorterCarrierInfos",
                column: "SorterCarrierId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_StickingParcelInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_StickingParcelInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VideoInfos_NodeType",
                schema: "dbo",
                table: "Parcel_VideoInfos",
                column: "NodeType");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VideoInfos_NvrSerialNumber",
                schema: "dbo",
                table: "Parcel_VideoInfos",
                column: "NvrSerialNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VideoInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_VideoInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_VolumeInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_VolumeInfos",
                column: "ParcelId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_WeightInfos_ParcelId",
                schema: "dbo",
                table: "Parcel_WeightInfos",
                column: "ParcelId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_WeightInfos_WeighingTime",
                schema: "dbo",
                table: "Parcel_WeightInfos",
                column: "WeighingTime");

            migrationBuilder.CreateIndex(
                name: "IX_ParcelLocations_SourceKey",
                schema: "dbo",
                table: "ParcelLocations",
                column: "SourceKey",
                unique: true,
                filter: "[SourceKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ParcelProcessingReceipts_ParcelId_RecordedAt",
                schema: "dbo",
                table: "ParcelProcessingReceipts",
                columns: CachedParcelIdRecordedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_DischargeTime",
                schema: "dbo",
                table: "Parcels",
                columns: CachedActualChuteIdDischargeTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime_Id",
                schema: "dbo",
                table: "Parcels",
                columns: CachedActualChuteIdScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagCode_ScannedTime_Id",
                schema: "dbo",
                table: "Parcels",
                columns: CachedBagCodeScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagId",
                schema: "dbo",
                table: "Parcels",
                column: "BagId");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_CreatedTime",
                schema: "dbo",
                table: "Parcels",
                column: "CreatedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_NoReadType_ScannedTime_Id",
                schema: "dbo",
                table: "Parcels",
                columns: CachedNoReadTypeScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ParcelTimestamp",
                schema: "dbo",
                table: "Parcels",
                column: "ParcelTimestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime_Id",
                schema: "dbo",
                table: "Parcels",
                columns: CachedRequestStatusScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ScannedTime_Id",
                schema: "dbo",
                table: "Parcels",
                columns: CachedScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime_Id",
                schema: "dbo",
                table: "Parcels",
                columns: CachedStatusExceptionTypeScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ScannedTime_Id",
                schema: "dbo",
                table: "Parcels",
                columns: CachedStatusScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime_Id",
                schema: "dbo",
                table: "Parcels",
                columns: CachedTargetChuteIdScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime_Id",
                schema: "dbo",
                table: "Parcels",
                columns: CachedWorkstationNameScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogDetails_StartedAt",
                schema: "dbo",
                table: "WebRequestAuditLogDetails",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_AuditResourceType_ResourceId_StartedAt",
                schema: "dbo",
                table: "WebRequestAuditLogs",
                columns: CachedAuditResourceTypeResourceIdStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_CorrelationId",
                schema: "dbo",
                table: "WebRequestAuditLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_IsSuccess_StartedAt",
                schema: "dbo",
                table: "WebRequestAuditLogs",
                columns: CachedIsSuccessStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_OperationName_StartedAt",
                schema: "dbo",
                table: "WebRequestAuditLogs",
                columns: CachedOperationNameStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_RequestPath_StartedAt",
                schema: "dbo",
                table: "WebRequestAuditLogs",
                columns: CachedRequestPathStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_StartedAt",
                schema: "dbo",
                table: "WebRequestAuditLogs",
                columns: CachedStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_StatusCode_StartedAt",
                schema: "dbo",
                table: "WebRequestAuditLogs",
                columns: CachedStatusCodeStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_TenantId_StartedAt",
                schema: "dbo",
                table: "WebRequestAuditLogs",
                columns: CachedTenantIdStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_TraceId",
                schema: "dbo",
                table: "WebRequestAuditLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_UserId_StartedAt",
                schema: "dbo",
                table: "WebRequestAuditLogs",
                columns: CachedUserIdStartedAtIdColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 步骤1：只允许空库回退，避免基线回退删除任何业务数据或留下孤立物理分表。
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.tables AS t
                    JOIN sys.schemas AS s ON s.schema_id = t.schema_id
                    WHERE s.name = N'dbo'
                      AND (t.name LIKE N'Parcels[_][0-9]%' OR t.name LIKE N'Parcel[_]%[_][0-9]%')
                ) THROW 51001, N'检测到包裹物理分表，禁止回退SQL Server基线。', 1;

                DECLARE @dataCheck nvarchar(max) = N'';
                SELECT @dataCheck = @dataCheck +
                    N'IF EXISTS (SELECT TOP (1) 1 FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) +
                    N') THROW 51002, N''检测到业务数据，禁止回退SQL Server基线。'', 1;' + CHAR(10)
                FROM sys.tables AS t
                JOIN sys.schemas AS s ON s.schema_id = t.schema_id
                WHERE s.name = N'dbo' AND t.name <> N'__EFMigrationsHistory';
                EXEC sys.sp_executesql @dataCheck;
                """);

            // 步骤2：通过数据守卫后，才删除基线创建的空表。
            migrationBuilder.DropTable(
                name: "ArchiveTasks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_ApiRequests",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_BarCodeInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_ChuteInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_CommandInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_DeviceInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_GrayDetectorInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_ImageInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_PositionInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_ProcessingRecords",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_SorterCarrierInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_StickingParcelInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_VideoInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_VolumeInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcel_WeightInfos",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ParcelLocations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ParcelPartitionCatalog",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "ParcelProcessingReceipts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "WebRequestAuditLogDetails",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Parcels",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "WebRequestAuditLogs",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "Bags",
                schema: "dbo");
        }
    }
}
