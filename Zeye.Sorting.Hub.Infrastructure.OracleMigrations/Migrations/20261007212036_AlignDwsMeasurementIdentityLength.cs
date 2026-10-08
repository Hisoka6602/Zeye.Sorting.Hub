using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.OracleMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AlignDwsMeasurementIdentityLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MessageIdentity",
                table: "Parcel_DwsMeasurements",
                type: "NVARCHAR2(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(128)",
                oldMaxLength: 128,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MessageIdentity",
                table: "Parcel_DwsMeasurements",
                type: "NVARCHAR2(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "NVARCHAR2(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
