using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.OracleMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddPartitionSchemaVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PartitionSchemaVersions",
                columns: table => new
                {
                    Key = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    MigrationId = table.Column<string>(type: "NVARCHAR2(150)", maxLength: 150, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartitionSchemaVersions", x => x.Key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartitionSchemaVersions");
        }
    }
}
