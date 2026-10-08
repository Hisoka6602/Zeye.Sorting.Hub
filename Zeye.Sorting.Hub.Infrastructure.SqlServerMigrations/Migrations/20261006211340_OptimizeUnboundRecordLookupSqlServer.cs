using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeUnboundRecordLookupSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_ParcelId_RecordedAt_Key",
                schema: "dbo",
                table: "Parcel_ProcessingRecords",
                columns: new[] { "ParcelId", "RecordedAt", "Key" },
                descending: new[] { false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcel_ProcessingRecords_ParcelId_RecordedAt_Key",
                schema: "dbo",
                table: "Parcel_ProcessingRecords");
        }
    }
}
