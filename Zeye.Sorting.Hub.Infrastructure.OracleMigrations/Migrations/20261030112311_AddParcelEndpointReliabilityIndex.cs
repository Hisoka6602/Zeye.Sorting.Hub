using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.OracleMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddParcelEndpointReliabilityIndex : Migration
    {
        /// <summary>端点可靠性索引的固定列顺序，供迁移只读复用。</summary>
        private static readonly string[] EndpointReliabilityIndexColumns = ["ParcelId", "OccurredAt", "Stage", "HasReliableTimestamp"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Processing_Parcel_Endpoint_Reliability",
                table: "Parcel_ProcessingRecords",
                columns: EndpointReliabilityIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processing_Parcel_Endpoint_Reliability",
                table: "Parcel_ProcessingRecords");
        }
    }
}
