using Oracle.ManagedDataAccess.Client;
using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Backup;

/// <summary>Oracle Data Pump schema 备份运行手册，登录凭据通过钱包或交互输入提供。</summary>
public sealed class OracleBackupProvider : AdditionalDatabaseBackupProvider {
    /// <inheritdoc />
    public override string ProviderName => "Oracle";
    /// <inheritdoc />
    public override string ConfiguredProviderName => ConfiguredProviderNames.Oracle;
    /// <inheritdoc />
    public override string BackupFileExtension => ".dmp";
    /// <inheritdoc />
    public override string ResolveDatabaseName(string connectionString) => DatabaseIdentifierPolicy.NormalizeDatabaseName(new OracleConnectionStringBuilder(connectionString).UserID, nameof(connectionString)).ToUpperInvariant();
    /// <inheritdoc />
    protected override string BuildCommand(string databaseName, string filePath) => $"expdp SCHEMAS={DatabaseIdentifierPolicy.NormalizeDatabaseName(databaseName, nameof(databaseName))} DIRECTORY=ZEYE_BACKUP DUMPFILE={BackupCommandTextFormatter.QuotePosixShellArgument(Path.GetFileName(filePath))} FLASHBACK_TIME=SYSTIMESTAMP";
}
