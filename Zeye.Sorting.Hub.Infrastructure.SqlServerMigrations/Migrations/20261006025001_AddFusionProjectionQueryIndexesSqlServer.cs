using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.SqlServerMigrations.Migrations;

/// <summary>投影队列与来源进度查询使用覆盖索引，避免读取全部原文页。</summary>
public partial class AddFusionProjectionQueryIndexesSqlServer : Migration {
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedProjectionStateReceivedAtSourceSequenceNextProjectionAtColumns = new[] { "ProjectionState", "ReceivedAt", "SourceSequence", "NextProjectionAt", "ProjectionClaimUntil" };
    /// <summary>重复调用共用的固定参数，使用方按只读方式消费。</summary>
    private static readonly string[] CachedSourceInstanceIdProjectionStateProjectionErrorColumns = new[] { "SourceInstanceId", "ProjectionState", "ProjectionError" };

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.CreateIndex(name: "IX_FusionFacts_ProjectionQueue", schema: "dbo", table: "FusionFactReceipts",
            columns: CachedProjectionStateReceivedAtSourceSequenceNextProjectionAtColumns);
        migrationBuilder.CreateIndex(name: "IX_FusionFacts_SourceProgress", schema: "dbo", table: "FusionFactReceipts",
            columns: CachedSourceInstanceIdProjectionStateProjectionErrorColumns);
    }
    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropIndex(name: "IX_FusionFacts_ProjectionQueue", schema: "dbo", table: "FusionFactReceipts");
        migrationBuilder.DropIndex(name: "IX_FusionFacts_SourceProgress", schema: "dbo", table: "FusionFactReceipts");
    }
}
