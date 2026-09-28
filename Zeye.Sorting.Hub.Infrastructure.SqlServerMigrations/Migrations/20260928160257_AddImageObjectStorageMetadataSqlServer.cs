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
