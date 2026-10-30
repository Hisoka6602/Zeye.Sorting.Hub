using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

/// <summary>已有业务库的普通索引随程序自动升级，结构变更仍由原有迁移治理决策。</summary>
public static class AutomaticIndexUpgradePolicy {
    /// <summary>普通索引自动升级开关，缺省启用以兼容尚未包含此字段的旧配置。</summary>
    public const string ConfigurationKey = "Persistence:MigrationGovernance:AutomaticIndexUpgradeEnabled";

    /// <summary>只接受非空且全部为非唯一索引创建的操作集，不把原始SQL或表结构操作视为索引升级。</summary>
    public static bool Allows(IConfiguration configuration, IReadOnlyList<MigrationOperation> operations) =>
        AutoTuningConfigurationReader.GetBoolOrDefault(configuration, ConfigurationKey, true) && operations.Count > 0
        && operations.All(operation => operation is CreateIndexOperation { IsUnique: false });
}
