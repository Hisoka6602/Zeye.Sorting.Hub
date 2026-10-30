using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqliteMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddDwsMessageLookupIndex : Migration
    {
        /// <summary>DWS消息检索索引的固定列顺序，供迁移只读复用。</summary>
        private static readonly string[] MessageLookupIndexColumns = ["MessageIdentity", "Stage", "IsSuccess"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Dws_Message_Stage_Success",
                table: "Parcel_DwsMeasurements",
                columns: MessageLookupIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Dws_Message_Stage_Success",
                table: "Parcel_DwsMeasurements");
        }
    }
}
