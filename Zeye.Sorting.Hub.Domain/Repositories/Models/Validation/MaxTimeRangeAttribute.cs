using System.ComponentModel.DataAnnotations;

namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Validation;

/// <summary>
/// 限制起止时间字段最大时间跨度的校验特性。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class MaxTimeRangeAttribute : ValidationAttribute {
    /// <summary>
    /// 字段：_startPropertyName。
    /// </summary>
    private readonly string _startPropertyName;

    /// <summary>
    /// 字段：_endPropertyName。
    /// </summary>
    private readonly string _endPropertyName;

    /// <summary>
    /// 字段：_maxMonths。
    /// </summary>
    private readonly int _maxMonths;

    /// <summary>
    /// 初始化时间跨度校验特性。
    /// </summary>
    public MaxTimeRangeAttribute(string startPropertyName, string endPropertyName, int maxMonths = 3) {
        _startPropertyName = startPropertyName;
        _endPropertyName = endPropertyName;
        _maxMonths = maxMonths;
        ErrorMessage = $"时间跨度不能超过 {maxMonths} 个月";
    }

    /// <summary>
    /// 执行时间跨度校验。
    /// </summary>
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) {
        if (value is null) {
            return ValidationResult.Success;
        }

        var startProperty = validationContext.ObjectType.GetProperty(_startPropertyName);
        var endProperty = validationContext.ObjectType.GetProperty(_endPropertyName);

        if (startProperty is null || endProperty is null) {
            return new ValidationResult($"时间跨度校验字段不存在：{_startPropertyName}/{_endPropertyName}");
        }

        var startValue = startProperty.GetValue(value) as DateTime?;
        var endValue = endProperty.GetValue(value) as DateTime?;

        if (!startValue.HasValue || !endValue.HasValue) {
            return ValidationResult.Success;
        }

        if (endValue.Value < startValue.Value) {
            return new ValidationResult("结束时间不能早于开始时间");
        }

        var maxEndTime = startValue.Value.AddMonths(_maxMonths);
        if (endValue.Value > maxEndTime) {
            return new ValidationResult(ErrorMessage);
        }

        return ValidationResult.Success;
    }
}
