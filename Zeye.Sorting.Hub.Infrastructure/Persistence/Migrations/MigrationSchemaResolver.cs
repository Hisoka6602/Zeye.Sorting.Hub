using Microsoft.EntityFrameworkCore.Migrations;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations;

/// <summary>
/// 迁移脚本共享的 schema 解析器：统一处理 SQL Server 与 MySQL 的 schema 差异。
/// </summary>
/// <remarks>
/// 该解析器集中封装 Provider 名称与默认 schema 常量，避免多个迁移文件复制相同字符串与判断逻辑，
/// 防止后续调整 schema 策略时发生“部分迁移遗漏更新”的漂移问题。
/// 采用 <c>internal static</c> 以确保仅在 Infrastructure 迁移边界内复用，且无实例化开销。
/// </remarks>
internal static class MigrationSchemaResolver {
    /// <summary>
    /// SQL Server 默认 schema。
    /// </summary>
    internal const string SqlServerDefaultSchema = "dbo";

    /// <summary>
    /// 判定当前迁移是否运行在 SQL Server Provider。
    /// </summary>
    /// <param name="migrationBuilder">迁移构建器。</param>
    /// <returns>是 SQL Server 返回 true，否则返回 false。</returns>
    internal static bool IsSqlServer(MigrationBuilder migrationBuilder) {
        return migrationBuilder.ActiveProvider == DbProviderNames.SqlServer;
    }

    /// <summary>
    /// 按 Provider 解析迁移使用的 schema：
    /// - SQL Server 使用 dbo；
    /// - MySQL 不使用 schema（返回 null）。
    /// </summary>
    /// <param name="migrationBuilder">迁移构建器。</param>
    /// <returns>SQL Server 返回 dbo，其他 Provider 返回 null。</returns>
    internal static string? ResolveSchema(MigrationBuilder migrationBuilder) {
        return IsSqlServer(migrationBuilder)
            ? SqlServerDefaultSchema
            : null;
    }
}
