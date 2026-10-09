using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeUnboundRecordLookup : Migration
    {
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly string[] CachedParcelIdRecordedAtKeyColumns = new[] { "ParcelId", "RecordedAt", "Key" };
        /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
        private static readonly bool[] CachedFalseTrueFalseDescending = new[] { false, true, false };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ProcessingRecords_ParcelId_RecordedAt_Key",
                table: "Parcel_ProcessingRecords",
                columns: CachedParcelIdRecordedAtKeyColumns,
                descending: CachedFalseTrueFalseDescending);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcel_ProcessingRecords_ParcelId_RecordedAt_Key",
                table: "Parcel_ProcessingRecords");
        }
    }
}
