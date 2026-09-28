using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddParcelProcessingOccurredAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_OccurredAt",
                table: "Parcel_ProcessingRecords",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcel_ProcessingRecords_OccurredAt",
                table: "Parcel_ProcessingRecords");
        }
    }
}
