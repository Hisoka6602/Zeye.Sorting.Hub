using Microsoft.Extensions.Diagnostics.HealthChecks;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Infrastructure.Persistence.Baseline;

namespace Zeye.Sorting.Hub.Host.HealthChecks;

/// <summary>
/// 基线数据健康检查。
/// </summary>
public sealed class BaselineDataHealthCheck : IHealthCheck {
    /// <summary>
    /// 基线校验器。
    /// </summary>
    private readonly BaselineDataValidator _baselineDataValidator;

    /// <summary>
    /// 初始化基线数据健康检查。
    /// </summary>
    /// <param name="baselineDataValidator">基线校验器。</param>
    public BaselineDataHealthCheck(BaselineDataValidator baselineDataValidator) {
        _baselineDataValidator = baselineDataValidator;
    }

    /// <summary>
    /// 执行健康检查。
    /// </summary>
    /// <param name="context">健康检查上下文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>健康检查结果。</returns>
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var result = _baselineDataValidator.GetLatestResult();
        var data = BuildHealthData(result);
        if (result is null) {
            return Task.FromResult(HealthCheckResult.Degraded("基线数据校验尚未生成结果。", data: data));
        }

        if (!result.IsValidationEnabled) {
            return Task.FromResult(HealthCheckResult.Healthy("基线数据校验未启用。", data: data));
        }

        if (result.IsValid) {
            return Task.FromResult(HealthCheckResult.Healthy(result.Summary, data: data));
        }

        return result.FailureMode == MigrationFailureMode.FailFast
            ? Task.FromResult(HealthCheckResult.Unhealthy(result.Summary, data: data))
            : Task.FromResult(HealthCheckResult.Degraded(result.Summary, data: data));
    }

    /// <summary>
    /// 构建健康检查附加数据。
    /// </summary>
    /// <param name="result">校验结果。</param>
    /// <returns>附加数据。</returns>
    private static IReadOnlyDictionary<string, object> BuildHealthData(BaselineDataValidationResult? result) {
        var data = new Dictionary<string, object> {
            ["hasValidationResult"] = result is not null
        };
        if (result is null) {
            return data;
        }

        data["validatedAtLocal"] = result.ValidatedAtLocal.ToString("yyyy-MM-dd HH:mm:ss");
        data["failureMode"] = result.FailureMode.ToString();
        data["isValid"] = result.IsValid;
        data["errorCount"] = result.Errors.Count;
        data["warningCount"] = result.Warnings.Count;
        data["wasSeedAttempted"] = result.WasSeedAttempted;
        data["seededRecordCount"] = result.SeededRecordCount;
        data["summary"] = result.Summary;
        return data;
    }
}
