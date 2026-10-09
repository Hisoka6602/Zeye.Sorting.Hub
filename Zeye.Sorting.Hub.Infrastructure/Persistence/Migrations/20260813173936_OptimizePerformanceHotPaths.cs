using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizePerformanceHotPaths : Migration
    {
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
        private static readonly string[] CachedEventTypeCreatedAtIdColumns = new[] { "EventType", "CreatedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusCreatedAtIdColumns = new[] { "Status", "CreatedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusLastAttemptedAtUpdatedAtIdColumns = new[] { "Status", "LastAttemptedAt", "UpdatedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedExpiresAtStatusIdColumns = new[] { "ExpiresAt", "Status", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedTaskTypeCreatedAtIdColumns = new[] { "TaskType", "CreatedAt", "Id" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedAuditResourceTypeResourceIdStartedAtColumns = new[] { "AuditResourceType", "ResourceId", "StartedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedIsSuccessStartedAtColumns = new[] { "IsSuccess", "StartedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedOperationNameStartedAtColumns = new[] { "OperationName", "StartedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedRequestPathStartedAtColumns = new[] { "RequestPath", "StartedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusCodeStartedAtColumns = new[] { "StatusCode", "StartedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedTenantIdStartedAtColumns = new[] { "TenantId", "StartedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedUserIdStartedAtColumns = new[] { "UserId", "StartedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedActualChuteIdScannedTimeColumns = new[] { "ActualChuteId", "ScannedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedBagCodeScannedTimeColumns = new[] { "BagCode", "ScannedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedNoReadTypeScannedTimeColumns = new[] { "NoReadType", "ScannedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedRequestStatusScannedTimeColumns = new[] { "RequestStatus", "ScannedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusExceptionTypeScannedTimeColumns = new[] { "Status", "ExceptionType", "ScannedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusScannedTimeColumns = new[] { "Status", "ScannedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedTargetChuteIdScannedTimeColumns = new[] { "TargetChuteId", "ScannedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedWorkstationNameScannedTimeColumns = new[] { "WorkstationName", "ScannedTime" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedEventTypeCreatedAtColumns = new[] { "EventType", "CreatedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedStatusCreatedAtColumns = new[] { "Status", "CreatedAt" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedExpiresAtStatusColumns = new[] { "ExpiresAt", "Status" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedTaskTypeCreatedAtColumns = new[] { "TaskType", "CreatedAt" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_AuditResourceType_ResourceId_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_IsSuccess_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_OperationName_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_RequestPath_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_StatusCode_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_TenantId_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_UserId_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_BagCode_ScannedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_NoReadType_ScannedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_ScannedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_ScannedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_EventType_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_ExpiresAt_Status",
                table: "InboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_Status_CreatedAt",
                table: "InboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_ArchiveTasks_Status_CreatedAt",
                table: "ArchiveTasks");

            migrationBuilder.DropIndex(
                name: "IX_ArchiveTasks_TaskType_CreatedAt",
                table: "ArchiveTasks");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_AuditResourceType_ResourceId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedAuditResourceTypeResourceIdStartedAtIdColumns);

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
                name: "IX_WebRequestAuditLogs_UserId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedUserIdStartedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime_Id",
                table: "Parcels",
                columns: CachedActualChuteIdScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagCode_ScannedTime_Id",
                table: "Parcels",
                columns: CachedBagCodeScannedTimeIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_NoReadType_ScannedTime_Id",
                table: "Parcels",
                columns: CachedNoReadTypeScannedTimeIdColumns);

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
                name: "IX_OutboxMessages_EventType_CreatedAt_Id",
                table: "OutboxMessages",
                columns: CachedEventTypeCreatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_CreatedAt_Id",
                table: "OutboxMessages",
                columns: CachedStatusCreatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_LastAttemptedAt_UpdatedAt_Id",
                table: "OutboxMessages",
                columns: CachedStatusLastAttemptedAtUpdatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ExpiresAt_Status_Id",
                table: "InboxMessages",
                columns: CachedExpiresAtStatusIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_CreatedAt_Id",
                table: "InboxMessages",
                columns: CachedStatusCreatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_Status_CreatedAt_Id",
                table: "ArchiveTasks",
                columns: CachedStatusCreatedAtIdColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_TaskType_CreatedAt_Id",
                table: "ArchiveTasks",
                columns: CachedTaskTypeCreatedAtIdColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_AuditResourceType_ResourceId_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_IsSuccess_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_OperationName_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_RequestPath_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_StatusCode_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_TenantId_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_WebRequestAuditLogs_UserId_StartedAt",
                table: "WebRequestAuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_BagCode_ScannedTime_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_NoReadType_ScannedTime_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_ScannedTime_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_Status_ScannedTime_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime_Id",
                table: "Parcels");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_EventType_CreatedAt_Id",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_CreatedAt_Id",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_LastAttemptedAt_UpdatedAt_Id",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_ExpiresAt_Status_Id",
                table: "InboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_InboxMessages_Status_CreatedAt_Id",
                table: "InboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_ArchiveTasks_Status_CreatedAt_Id",
                table: "ArchiveTasks");

            migrationBuilder.DropIndex(
                name: "IX_ArchiveTasks_TaskType_CreatedAt_Id",
                table: "ArchiveTasks");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_AuditResourceType_ResourceId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedAuditResourceTypeResourceIdStartedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_IsSuccess_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedIsSuccessStartedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_OperationName_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedOperationNameStartedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_RequestPath_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedRequestPathStartedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_StartedAt",
                table: "WebRequestAuditLogs",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_StatusCode_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedStatusCodeStartedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_TenantId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedTenantIdStartedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_UserId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: CachedUserIdStartedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime",
                table: "Parcels",
                columns: CachedActualChuteIdScannedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagCode_ScannedTime",
                table: "Parcels",
                columns: CachedBagCodeScannedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_NoReadType_ScannedTime",
                table: "Parcels",
                columns: CachedNoReadTypeScannedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime",
                table: "Parcels",
                columns: CachedRequestStatusScannedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ScannedTime",
                table: "Parcels",
                column: "ScannedTime");

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime",
                table: "Parcels",
                columns: CachedStatusExceptionTypeScannedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ScannedTime",
                table: "Parcels",
                columns: CachedStatusScannedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime",
                table: "Parcels",
                columns: CachedTargetChuteIdScannedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime",
                table: "Parcels",
                columns: CachedWorkstationNameScannedTimeColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EventType_CreatedAt",
                table: "OutboxMessages",
                columns: CachedEventTypeCreatedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_CreatedAt",
                table: "OutboxMessages",
                columns: CachedStatusCreatedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ExpiresAt_Status",
                table: "InboxMessages",
                columns: CachedExpiresAtStatusColumns);

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_CreatedAt",
                table: "InboxMessages",
                columns: CachedStatusCreatedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_Status_CreatedAt",
                table: "ArchiveTasks",
                columns: CachedStatusCreatedAtColumns);

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_TaskType_CreatedAt",
                table: "ArchiveTasks",
                columns: CachedTaskTypeCreatedAtColumns);
        }
    }
}
