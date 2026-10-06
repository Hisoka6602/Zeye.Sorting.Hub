using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations;

/// <summary>投影队列与来源进度查询使用覆盖索引，避免读取全部原文页。</summary>
public partial class AddFusionProjectionQueryIndexes : Migration {
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.CreateIndex(name: "IX_FusionFacts_ProjectionQueue", table: "FusionFactReceipts",
            columns: new[] { "ProjectionState", "ReceivedAt", "SourceSequence", "NextProjectionAt", "ProjectionClaimUntil" });
        migrationBuilder.CreateIndex(name: "IX_FusionFacts_SourceProgress", table: "FusionFactReceipts",
            columns: new[] { "SourceInstanceId", "ProjectionState", "ProjectionError" });
    }
    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropIndex(name: "IX_FusionFacts_ProjectionQueue", table: "FusionFactReceipts");
        migrationBuilder.DropIndex(name: "IX_FusionFacts_SourceProgress", table: "FusionFactReceipts");
    }
}
