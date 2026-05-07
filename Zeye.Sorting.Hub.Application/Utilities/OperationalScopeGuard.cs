using NLog;
using Zeye.Sorting.Hub.Contracts.Models.Common;
using Zeye.Sorting.Hub.Domain.ValueObjects;

namespace Zeye.Sorting.Hub.Application.Utilities;

/// <summary>
/// 运营边界守卫工具。
/// 统一负责站点、产线、设备与工作站维度的标准化、边界校验与合同映射，避免后续业务模块重复实现同义规则。
/// </summary>
public static class OperationalScopeGuard {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 从请求合同构建运营边界值对象。
    /// </summary>
    /// <param name="request">运营边界请求合同。</param>
    /// <param name="logContext">日志上下文。</param>
    /// <returns>标准化后的运营边界值对象。</returns>
    public static OperationalScope Create(OperationalScopeRequest request, string logContext) {
        if (request is null) {
            Logger.Error("{LogContext}运营边界请求为空。", logContext);
            throw new ArgumentNullException(nameof(request), "运营边界请求不能为空。");
        }

        return Create(
            request.SiteCode,
            request.LineCode,
            request.DeviceCode,
            request.WorkstationName,
            logContext);
    }

    /// <summary>
    /// 从原始维度字段构建运营边界值对象。
    /// </summary>
    /// <param name="siteCode">站点编码。</param>
    /// <param name="lineCode">产线编码。</param>
    /// <param name="deviceCode">设备编码。</param>
    /// <param name="workstationName">工作站名称。</param>
    /// <param name="logContext">日志上下文。</param>
    /// <returns>标准化后的运营边界值对象。</returns>
    public static OperationalScope Create(string siteCode, string? lineCode, string? deviceCode, string workstationName, string logContext) {
        var normalizedSiteCode = NormalizeRequiredText(siteCode, nameof(siteCode), "SiteCode", SiteIdentity.MaxCodeLength, logContext);
        var normalizedLineCode = NormalizeOptionalText(lineCode, nameof(lineCode), "LineCode", LineIdentity.MaxCodeLength, logContext);
        var normalizedDeviceCode = NormalizeOptionalText(deviceCode, nameof(deviceCode), "DeviceCode", DeviceIdentity.MaxCodeLength, logContext);
        var normalizedWorkstationName = NormalizeRequiredText(workstationName, nameof(workstationName), "WorkstationName", OperationalScope.MaxWorkstationNameLength, logContext);

        return new OperationalScope {
            SiteIdentity = new SiteIdentity {
                SiteCode = normalizedSiteCode
            },
            LineIdentity = normalizedLineCode is null
                ? null
                : new LineIdentity {
                    LineCode = normalizedLineCode
                },
            DeviceIdentity = normalizedDeviceCode is null
                ? null
                : new DeviceIdentity {
                    DeviceCode = normalizedDeviceCode
                },
            WorkstationName = normalizedWorkstationName
        };
    }

    /// <summary>
    /// 将运营边界值对象映射为响应合同。
    /// </summary>
    /// <param name="scope">运营边界值对象。</param>
    /// <returns>响应合同。</returns>
    public static OperationalScopeResponse ToResponse(OperationalScope scope) {
        if (scope is null) {
            Logger.Error("运营边界响应映射失败，scope 为空。");
            throw new ArgumentNullException(nameof(scope), "运营边界值对象不能为空。");
        }

        return new OperationalScopeResponse {
            SiteCode = scope.SiteCode,
            LineCode = scope.LineCode,
            DeviceCode = scope.DeviceCode,
            WorkstationName = scope.WorkstationName
        };
    }

    /// <summary>
    /// 标准化必填文本。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <param name="paramName">参数名。</param>
    /// <param name="fieldName">业务字段名。</param>
    /// <param name="maxLength">最大长度。</param>
    /// <param name="logContext">日志上下文。</param>
    /// <returns>标准化后的文本。</returns>
    private static string NormalizeRequiredText(string value, string paramName, string fieldName, int maxLength, string logContext) {
        if (string.IsNullOrWhiteSpace(value)) {
            Logger.Warn("{LogContext}运营边界字段不能为空，{FieldName} 为空。", logContext, fieldName);
            throw new ArgumentException($"运营边界字段 {fieldName} 不能为空。", paramName);
        }

        var normalizedValue = value.Trim();
        EnsureLengthWithinLimit(normalizedValue, paramName, fieldName, maxLength, logContext);
        return normalizedValue;
    }

    /// <summary>
    /// 标准化可选文本。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <param name="paramName">参数名。</param>
    /// <param name="fieldName">业务字段名。</param>
    /// <param name="maxLength">最大长度。</param>
    /// <param name="logContext">日志上下文。</param>
    /// <returns>标准化后的文本；若为空白则返回 null。</returns>
    private static string? NormalizeOptionalText(string? value, string paramName, string fieldName, int maxLength, string logContext) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        var normalizedValue = value.Trim();
        EnsureLengthWithinLimit(normalizedValue, paramName, fieldName, maxLength, logContext);
        return normalizedValue;
    }

    /// <summary>
    /// 校验文本长度上限。
    /// </summary>
    /// <param name="value">标准化后的值。</param>
    /// <param name="paramName">参数名。</param>
    /// <param name="fieldName">业务字段名。</param>
    /// <param name="maxLength">最大长度。</param>
    /// <param name="logContext">日志上下文。</param>
    private static void EnsureLengthWithinLimit(string value, string paramName, string fieldName, int maxLength, string logContext) {
        if (value.Length > maxLength) {
            Logger.Warn("{LogContext}运营边界字段超长，{FieldName}Length={FieldLength}, MaxLength={MaxLength}。", logContext, fieldName, value.Length, maxLength);
            throw new ArgumentOutOfRangeException(paramName, $"运营边界字段 {fieldName} 长度不能超过 {maxLength}。");
        }
    }
}
