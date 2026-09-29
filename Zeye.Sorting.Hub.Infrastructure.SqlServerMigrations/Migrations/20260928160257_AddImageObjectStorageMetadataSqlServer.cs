using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddImageObjectStorageMetadataSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 旧图片物理分表需先受控补建新字段和索引。
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.tables AS t
                    JOIN sys.schemas AS s ON s.schema_id = t.schema_id
                    WHERE s.name = N'dbo' AND t.name LIKE N'Parcel[_]ImageInfos[_]%'
                ) THROW 51004, N'检测到图片物理分表，需先制定对象存储元数据补建方案。', 1;
                """);

            migrationBuilder.AddColumn<string>(
                name: "BucketName",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ETag",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ObjectSizeBytes",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StorageProvider",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAtLocal",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                type: "datetime2",
                nullable: true,
                comment: "上传完成时间（本地时间）");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_BucketName",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                column: "BucketName");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_StorageProvider",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                column: "StorageProvider");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_UploadedAtLocal",
                schema: "dbo",
                table: "Parcel_ImageInfos",
                column: "UploadedAtLocal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回退前检查物理分表及对象定位数据，避免静默丢失。
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.tables AS t
                    JOIN sys.schemas AS s ON s.schema_id = t.schema_id
                    WHERE s.name = N'dbo' AND t.name LIKE N'Parcel[_]ImageInfos[_]%'
                ) THROW 51005, N'检测到图片物理分表，禁止回退对象存储元数据迁移。', 1;
                IF EXISTS (
                    SELECT 1 FROM dbo.Parcel_ImageInfos
                    WHERE BucketName IS NOT NULL OR ContentType IS NOT NULL OR ETag IS NOT NULL
                       OR ObjectKey IS NOT NULL OR ObjectSizeBytes IS NOT NULL
                       OR OriginalFileName IS NOT NULL OR Sha256 IS NOT NULL
                       OR StorageProvider IS NOT NULL OR UploadedAtLocal IS NOT NULL
                ) THROW 51006, N'检测到图片对象存储元数据，禁止删除对应字段。', 1;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Parcel_ImageInfos_BucketName",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_ImageInfos_StorageProvider",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_ImageInfos_UploadedAtLocal",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "BucketName",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "ContentType",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "ETag",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "ObjectKey",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "ObjectSizeBytes",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "Sha256",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "StorageProvider",
                schema: "dbo",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "UploadedAtLocal",
                schema: "dbo",
                table: "Parcel_ImageInfos");
        }
    }
}
