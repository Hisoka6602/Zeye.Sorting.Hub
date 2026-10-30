using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.OracleMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddEndpointReliabilityCoveringIndex : Migration
    {
        /// <summary>端点、完整来源身份与同刻重试所需的覆盖索引列顺序。</summary>
        private static readonly string[] EndpointIndexColumns = ["ParcelId", "OccurredAt", "Stage", "HasReliableTimestamp",
            "SourceInstanceId", "SourceRunId", "SourceParcelId", "AttemptNumber", "RecordId", "IsSuccess"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Processing_Endpoint_Reliability_Covering",
                table: "Parcel_ProcessingRecords",
                columns: EndpointIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Processing_Endpoint_Reliability_Covering",
                table: "Parcel_ProcessingRecords");
        }
    }
}
