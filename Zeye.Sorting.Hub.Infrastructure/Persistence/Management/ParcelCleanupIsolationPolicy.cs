using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Management;

/// <summary>包裹清理与历史清单精简共用的危险动作隔离配置。</summary>
public static class ParcelCleanupIsolationPolicy {
    /// <summary>守卫开关，可填写 true/false，默认 true。</summary>
    public const string EnableGuardConfigKey = "Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:EnableGuard";
    /// <summary>允许执行，可填写 true/false，默认 true，页面删除仍须验证当前密码。</summary>
    public const string AllowExecutionConfigKey = "Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:AllowDangerousActionExecution";
    /// <summary>仅演练，可填写 true/false，默认 false。</summary>
    public const string DryRunConfigKey = "Persistence:RepositoryDangerousActions:ParcelRemoveExpired:Isolator:DryRun";

    /// <summary>统一计算阻断、演练或执行决策，避免历史升级绕过清理隔离器。</summary>
    public static ActionIsolationDecision Evaluate(IConfiguration configuration) => ActionIsolationPolicy.Evaluate(
        AutoTuningConfigurationReader.GetBoolOrDefault(configuration, EnableGuardConfigKey, true),
        AutoTuningConfigurationReader.GetBoolOrDefault(configuration, AllowExecutionConfigKey, true),
        AutoTuningConfigurationReader.GetBoolOrDefault(configuration, DryRunConfigKey, false),
        dangerousAction: true, isRollback: false);
}
