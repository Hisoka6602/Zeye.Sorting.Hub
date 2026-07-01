using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImageObjectStorageMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BucketName",
                table: "Parcel_ImageInfos",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Parcel_ImageInfos",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ETag",
                table: "Parcel_ImageInfos",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ObjectKey",
                table: "Parcel_ImageInfos",
                type: "varchar(1024)",
                maxLength: 1024,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<long>(
                name: "ObjectSizeBytes",
                table: "Parcel_ImageInfos",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "Parcel_ImageInfos",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "Parcel_ImageInfos",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "StorageProvider",
                table: "Parcel_ImageInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UploadedAtLocal",
                table: "Parcel_ImageInfos",
                type: "datetime(6)",
                nullable: true,
                comment: "上传完成时间（本地时间）");

            if (migrationBuilder.ActiveProvider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
            {
                migrationBuilder.Sql(
                    "CREATE INDEX `IX_Parcel_ImageInfos_BucketName_ObjectKey` ON `Parcel_ImageInfos` (`BucketName`(128), `ObjectKey`(512));");
            }
            else
            {
                migrationBuilder.CreateIndex(
                    name: "IX_Parcel_ImageInfos_BucketName_ObjectKey",
                    table: "Parcel_ImageInfos",
                    columns: new[] { "BucketName", "ObjectKey" });
            }

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_StorageProvider",
                table: "Parcel_ImageInfos",
                column: "StorageProvider");

            migrationBuilder.CreateIndex(
                name: "IX_Parcel_ImageInfos_UploadedAtLocal",
                table: "Parcel_ImageInfos",
                column: "UploadedAtLocal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Parcel_ImageInfos_BucketName_ObjectKey",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_ImageInfos_StorageProvider",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropIndex(
                name: "IX_Parcel_ImageInfos_UploadedAtLocal",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "BucketName",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "ETag",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "ObjectKey",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "ObjectSizeBytes",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "StorageProvider",
                table: "Parcel_ImageInfos");

            migrationBuilder.DropColumn(
                name: "UploadedAtLocal",
                table: "Parcel_ImageInfos");
        }
    }
}
