namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Migrations;

/// <summary>Oracle、SQLite 的独立 Code First 迁移程序集名称。</summary>
internal static class AdditionalMigrationAssemblies {
    /// <summary>Oracle 模型迁移与快照。</summary>
    internal const string Oracle = "Zeye.Sorting.Hub.Infrastructure.OracleMigrations";
    /// <summary>SQLite 模型迁移与快照。</summary>
    internal const string SQLite = "Zeye.Sorting.Hub.Infrastructure.SqliteMigrations";
}
