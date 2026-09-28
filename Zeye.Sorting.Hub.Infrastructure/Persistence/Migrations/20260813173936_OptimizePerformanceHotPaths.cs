using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizePerformanceHotPaths : Migration
    {
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
                columns: new[] { "AuditResourceType", "ResourceId", "StartedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_IsSuccess_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "IsSuccess", "StartedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_OperationName_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "OperationName", "StartedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_RequestPath_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "RequestPath", "StartedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "StartedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_StatusCode_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "StatusCode", "StartedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_TenantId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "TenantId", "StartedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_WebRequestAuditLogs_UserId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "UserId", "StartedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime_Id",
                table: "Parcels",
                columns: new[] { "ActualChuteId", "ScannedTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagCode_ScannedTime_Id",
                table: "Parcels",
                columns: new[] { "BagCode", "ScannedTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_NoReadType_ScannedTime_Id",
                table: "Parcels",
                columns: new[] { "NoReadType", "ScannedTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_RequestStatus_ScannedTime_Id",
                table: "Parcels",
                columns: new[] { "RequestStatus", "ScannedTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ScannedTime_Id",
                table: "Parcels",
                columns: new[] { "ScannedTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ExceptionType_ScannedTime_Id",
                table: "Parcels",
                columns: new[] { "Status", "ExceptionType", "ScannedTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_Status_ScannedTime_Id",
                table: "Parcels",
                columns: new[] { "Status", "ScannedTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_TargetChuteId_ScannedTime_Id",
                table: "Parcels",
                columns: new[] { "TargetChuteId", "ScannedTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_WorkstationName_ScannedTime_Id",
                table: "Parcels",
                columns: new[] { "WorkstationName", "ScannedTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EventType_CreatedAt_Id",
                table: "OutboxMessages",
                columns: new[] { "EventType", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_CreatedAt_Id",
                table: "OutboxMessages",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_LastAttemptedAt_UpdatedAt_Id",
                table: "OutboxMessages",
                columns: new[] { "Status", "LastAttemptedAt", "UpdatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ExpiresAt_Status_Id",
                table: "InboxMessages",
                columns: new[] { "ExpiresAt", "Status", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_CreatedAt_Id",
                table: "InboxMessages",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_Status_CreatedAt_Id",
                table: "ArchiveTasks",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_TaskType_CreatedAt_Id",
                table: "ArchiveTasks",
                columns: new[] { "TaskType", "CreatedAt", "Id" });
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
                columns: new[] { "AuditResourceType", "ResourceId", "StartedAt" });

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
                name: "IX_WebRequestAuditLogs_UserId_StartedAt",
                table: "WebRequestAuditLogs",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_ActualChuteId_ScannedTime",
                table: "Parcels",
                columns: new[] { "ActualChuteId", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_BagCode_ScannedTime",
                table: "Parcels",
                columns: new[] { "BagCode", "ScannedTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Parcels_NoReadType_ScannedTime",
                table: "Parcels",
                columns: new[] { "NoReadType", "ScannedTime" });

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
                name: "IX_OutboxMessages_EventType_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_ExpiresAt_Status",
                table: "InboxMessages",
                columns: new[] { "ExpiresAt", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InboxMessages_Status_CreatedAt",
                table: "InboxMessages",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_Status_CreatedAt",
                table: "ArchiveTasks",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveTasks_TaskType_CreatedAt",
                table: "ArchiveTasks",
                columns: new[] { "TaskType", "CreatedAt" });
        }
    }
}
