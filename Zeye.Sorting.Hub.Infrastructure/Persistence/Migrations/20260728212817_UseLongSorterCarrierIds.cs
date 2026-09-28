using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseLongSorterCarrierIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "SorterCarrierId",
                table: "Parcels",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "SorterCarrierId",
                table: "Parcel_SorterCarrierInfos",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "SorterCarrierId",
                table: "Parcels",
                type: "int",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "SorterCarrierId",
                table: "Parcel_SorterCarrierInfos",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
