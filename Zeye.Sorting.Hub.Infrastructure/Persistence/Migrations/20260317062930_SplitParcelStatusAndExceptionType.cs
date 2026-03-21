using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitParcelStatusAndExceptionType : Migration
    {
        /// <summary>
        /// MySQL Provider 名称。
        /// </summary>
        private const string MySqlProvider = "Pomelo.EntityFrameworkCore.MySql";

        /// <summary>
        /// SQL Server Provider 名称。
        /// </summary>
        private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

        /// <summary>
        /// SQL Server 默认 schema。
        /// </summary>
        private const string SqlServerDefaultSchema = "dbo";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExceptionType",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels",
                type: "int",
                nullable: true);

            if (migrationBuilder.ActiveProvider == MySqlProvider)
            {
                // 旧状态回填映射：
                // InterfaceError(2)->InterfaceResponseException(1)
                // DwsTimeout(3)->WaitDwsDataTimeout(2)
                // ChuteMismatch(5)->InvalidTargetChute(4)
                // SpeedMismatch(6)->SpeedMismatch(5)
                // MultipleParcels(7)->StickingParcel(7)
                // LockedChute(8)->LockedChute(6)
                // GrayScaleSensorError(10)->GrayDetectorResponseException(8)
                // PositionError(11)->PositionDetectionException(9)
                // 其余旧异常状态归并为 SortingException，ExceptionType 保持 null
                migrationBuilder.Sql(
                    """
                    UPDATE `Parcels`
                    SET `ExceptionType` = CASE `Status`
                        WHEN 2 THEN 1
                        WHEN 3 THEN 2
                        WHEN 5 THEN 4
                        WHEN 6 THEN 5
                        WHEN 7 THEN 7
                        WHEN 8 THEN 6
                        WHEN 10 THEN 8
                        WHEN 11 THEN 9
                        ELSE NULL
                    END,
                    `Status` = CASE
                        WHEN `Status` IN (0, 1) THEN `Status`
                        ELSE 2
                    END;
                    """);
            }
            else if (migrationBuilder.ActiveProvider == SqlServerProvider)
            {
                // 旧状态回填映射同 MySQL 分支，确保跨 Provider 行为一致。
                migrationBuilder.Sql(
                    """
                    UPDATE [dbo].[Parcels]
                    SET [ExceptionType] = CASE [Status]
                        WHEN 2 THEN 1
                        WHEN 3 THEN 2
                        WHEN 5 THEN 4
                        WHEN 6 THEN 5
                        WHEN 7 THEN 7
                        WHEN 8 THEN 6
                        WHEN 10 THEN 8
                        WHEN 11 THEN 9
                        ELSE NULL
                    END,
                    [Status] = CASE
                        WHEN [Status] IN (0, 1) THEN [Status]
                        ELSE 2
                    END;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExceptionType",
                schema: ResolveSchema(migrationBuilder),
                table: "Parcels");
        }

        /// <summary>
        /// 按 Provider 解析迁移使用的 schema：
        /// - SQL Server 使用 dbo；
        /// - MySQL 不使用 schema（返回 null）。
        /// </summary>
        private static string ResolveSchema(MigrationBuilder migrationBuilder)
        {
            return migrationBuilder.ActiveProvider == SqlServerProvider
                ? SqlServerDefaultSchema
                : null;
        }
    }
}
