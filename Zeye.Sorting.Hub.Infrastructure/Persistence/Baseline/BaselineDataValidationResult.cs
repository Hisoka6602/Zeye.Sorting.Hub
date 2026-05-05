using Zeye.Sorting.Hub.Domain.Enums;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.Baseline;

/// <summary>
/// 基线数据校验结果。
/// </summary>
public sealed record class BaselineDataValidationResult {
    /// <summary>
    /// 校验完成时间（本地时间）。
    /// </summary>
    public required DateTime ValidatedAtLocal { get; init; }

    /// <summary>
    /// 是否启用基线校验。
    /// </summary>
    public required bool IsValidationEnabled { get; init; }

    /// <summary>
    /// 是否启用种子入口。
    /// </summary>
    public required bool IsSeedEnabled { get; init; }

    /// <summary>
    /// 失败模式。
    /// </summary>
    public required MigrationFailureMode FailureMode { get; init; }

    /// <summary>
    /// 是否通过校验。
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// 是否应阻断启动。
    /// </summary>
    public required bool ShouldBlockStartup { get; init; }

    /// <summary>
    /// 是否已尝试执行种子入口。
    /// </summary>
    public required bool WasSeedAttempted { get; init; }

    /// <summary>
    /// 种子写入记录数。
    /// </summary>
    public required int SeededRecordCount { get; init; }

    /// <summary>
    /// 结果摘要。
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// 错误清单。
    /// </summary>
    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>
    /// 告警清单。
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// 种子执行说明。
    /// </summary>
    public required IReadOnlyList<string> SeedMessages { get; init; }

    /// <summary>
    /// 创建“已禁用”结果。
    /// </summary>
    /// <param name="options">配置项。</param>
    /// <returns>校验结果。</returns>
    public static BaselineDataValidationResult CreateDisabled(BaselineDataOptions options) {
        ArgumentNullException.ThrowIfNull(options);

        return new BaselineDataValidationResult {
            ValidatedAtLocal = DateTime.Now,
            IsValidationEnabled = false,
            IsSeedEnabled = options.IsSeedEnabled,
            FailureMode = options.ResolveFailureMode(),
            IsValid = true,
            ShouldBlockStartup = false,
            WasSeedAttempted = false,
            SeededRecordCount = 0,
            Summary = "基线数据校验未启用。",
            Errors = [],
            Warnings = [],
            SeedMessages = []
        };
    }

    /// <summary>
    /// 创建“执行异常”结果。
    /// </summary>
    /// <param name="options">配置项。</param>
    /// <param name="failureMessage">失败消息。</param>
    /// <returns>校验结果。</returns>
    public static BaselineDataValidationResult CreateFailed(BaselineDataOptions options, string failureMessage) {
        ArgumentNullException.ThrowIfNull(options);

        var failureMode = options.ResolveFailureMode();
        var normalizedMessage = string.IsNullOrWhiteSpace(failureMessage) ? "基线数据校验执行失败。" : failureMessage.Trim();
        return new BaselineDataValidationResult {
            ValidatedAtLocal = DateTime.Now,
            IsValidationEnabled = options.IsValidationEnabled,
            IsSeedEnabled = options.IsSeedEnabled,
            FailureMode = failureMode,
            IsValid = false,
            ShouldBlockStartup = failureMode == MigrationFailureMode.FailFast,
            WasSeedAttempted = false,
            SeededRecordCount = 0,
            Summary = $"基线数据校验执行异常：{normalizedMessage}",
            Errors = [normalizedMessage],
            Warnings = [],
            SeedMessages = []
        };
    }
}
