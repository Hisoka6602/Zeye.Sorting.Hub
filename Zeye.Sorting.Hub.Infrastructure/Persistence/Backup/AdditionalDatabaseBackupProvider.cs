namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>Oracle 与 SQLite 共用的原生备份运行手册计划构造，不伪装成已执行的备份。</summary>
public abstract class AdditionalDatabaseBackupProvider : IBackupProvider {
    /// <inheritdoc />
    public abstract string ProviderName { get; }
    /// <inheritdoc />
    public abstract string ConfiguredProviderName { get; }
    /// <inheritdoc />
    public abstract string BackupFileExtension { get; }
    /// <inheritdoc />
    public abstract string ResolveDatabaseName(string connectionString);
    /// <summary>生成不含凭据的原生备份命令。</summary>
    protected abstract string BuildCommand(string databaseName, string filePath);
    /// <inheritdoc />
    public BackupPlan BuildPlan(BackupOptions options, string backupDirectoryPath, string databaseName, DateTime generatedAtLocal) {
        var filePath = Path.Combine(backupDirectoryPath, ConfiguredProviderName,
            $"{BackupFileNamePolicy.SanitizeSegment(options.BackupFilePrefix)}-{generatedAtLocal:yyyyMMddHHmmss}-{BackupFileNamePolicy.SanitizeSegment(Path.GetFileNameWithoutExtension(databaseName))}{BackupFileExtension}");
        return new BackupPlan { GeneratedAtLocal = generatedAtLocal, IsEnabled = options.IsEnabled, IsDryRun = options.DryRun,
            ProviderName = ProviderName, ConfiguredProviderName = ConfiguredProviderName, DatabaseName = databaseName,
            BackupDirectoryPath = backupDirectoryPath, PlannedBackupFilePath = filePath, CommandText = BuildCommand(databaseName, filePath) };
    }
}
