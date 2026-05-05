using Microsoft.Extensions.Configuration;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Baseline;

/// <summary>
/// 基线数据校验与种子入口配置。
/// </summary>
public sealed class BaselineDataOptions {
    /// <summary>
    /// 配置节路径。
    /// </summary>
    public const string SectionPath = "Persistence:BaselineData";

    /// <summary>
    /// 校验开关配置键。可填写值:true / false。
    /// </summary>
    public const string ValidationEnabledConfigKey = "Persistence:BaselineData:IsValidationEnabled";

    /// <summary>
    /// 种子开关配置键。可填写值:true / false。
    /// </summary>
    public const string SeedEnabledConfigKey = "Persistence:BaselineData:IsSeedEnabled";

    /// <summary>
    /// 失败模式配置键。可填写值:Degraded / FailFast。
    /// </summary>
    public const string FailureModeConfigKey = "Persistence:BaselineData:FailureMode";

    /// <summary>
    /// 是否启用基线校验。
    /// </summary>
    public bool IsValidationEnabled { get; set; } = true;

    /// <summary>
    /// 是否启用可选种子入口。
    /// </summary>
    public bool IsSeedEnabled { get; set; }

    /// <summary>
    /// 校验失败模式文本。
    /// </summary>
    public string FailureMode { get; set; } = nameof(MigrationFailureMode.Degraded);

    /// <summary>
    /// 从配置源应用选项值。
    /// </summary>
    /// <param name="options">目标选项。</param>
    /// <param name="configuration">配置源。</param>
    public static void Apply(BaselineDataOptions options, IConfiguration configuration) {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        options.IsValidationEnabled = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, ValidationEnabledConfigKey, true);
        options.IsSeedEnabled = AutoTuningConfigurationReader.GetBoolOrDefault(configuration, SeedEnabledConfigKey, false);
        options.FailureMode = NormalizeFailureModeText(configuration[FailureModeConfigKey]);
    }

    /// <summary>
    /// 判断失败模式配置是否受支持。
    /// </summary>
    /// <param name="failureMode">失败模式文本。</param>
    /// <returns>受支持返回 true。</returns>
    public static bool IsSupportedFailureMode(string? failureMode) {
        return string.Equals(failureMode, nameof(MigrationFailureMode.Degraded), StringComparison.OrdinalIgnoreCase)
            || string.Equals(failureMode, nameof(MigrationFailureMode.FailFast), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 解析失败模式。
    /// </summary>
    /// <returns>失败模式枚举。</returns>
    public MigrationFailureMode ResolveFailureMode() {
        if (string.Equals(FailureMode, nameof(MigrationFailureMode.FailFast), StringComparison.OrdinalIgnoreCase)) {
            return MigrationFailureMode.FailFast;
        }

        return MigrationFailureMode.Degraded;
    }

    /// <summary>
    /// 归一化失败模式文本。
    /// </summary>
    /// <param name="failureMode">原始文本。</param>
    /// <returns>归一化后的文本。</returns>
    private static string NormalizeFailureModeText(string? failureMode) {
        if (string.Equals(failureMode, nameof(MigrationFailureMode.FailFast), StringComparison.OrdinalIgnoreCase)) {
            return nameof(MigrationFailureMode.FailFast);
        }

        return nameof(MigrationFailureMode.Degraded);
    }
}
