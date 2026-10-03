using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRetiredMessageStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 只删除空的遗留表；非空表保留历史数据，避免升级时销毁记录。
            migrationBuilder.Sql("""
                SET @'retired_storage_cleanup' = IF(
                    (SELECT COUNT(*) FROM `OutboxMessages`) = 0,
                    'DROP TABLE `OutboxMessages`',
                    'SELECT 1');
                PREPARE retired_storage_cleanup FROM @'retired_storage_cleanup';
                EXECUTE retired_storage_cleanup;
                DEALLOCATE PREPARE retired_storage_cleanup;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EventType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FailureMessage = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastAttemptedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PayloadJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
        }
    }
}
