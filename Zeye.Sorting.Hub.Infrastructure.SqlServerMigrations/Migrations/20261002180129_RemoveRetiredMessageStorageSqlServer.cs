using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRetiredMessageStorageSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 只删除空的遗留表；非空表保留历史数据，避免升级时销毁记录。
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[dbo].[OutboxMessages]', N'U') IS NOT NULL
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM [dbo].[OutboxMessages])
                        DROP TABLE [dbo].[OutboxMessages];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FailureMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    LastAttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_EventType_CreatedAt_Id",
                schema: "dbo",
                table: "OutboxMessages",
                columns: new[] { "EventType", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_CreatedAt_Id",
                schema: "dbo",
                table: "OutboxMessages",
                columns: new[] { "Status", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_LastAttemptedAt_UpdatedAt_Id",
                schema: "dbo",
                table: "OutboxMessages",
                columns: new[] { "Status", "LastAttemptedAt", "UpdatedAt", "Id" });
        }
    }
}
