using System.ComponentModel;
using System.Reflection;
using Zeye.Sorting.Hub.Contracts.Enums.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Domain.Enums;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using ContractParcelExceptionType = Zeye.Sorting.Hub.Contracts.Enums.Parcels.ParcelExceptionType;

namespace Zeye.Sorting.Hub.Host.Swagger;

/// <summary>
/// 枚举中文说明增强过滤器（展示 数值 + 枚举名 + 中文描述）。
/// </summary>
public sealed class EnumDescriptionSchemaFilter : ISchemaFilter {
    /// <summary>
    /// 合同字段到枚举类型映射（用于 int 字段的枚举说明增强）。
    /// </summary>
    private static readonly IReadOnlyDictionary<Type, IReadOnlyDictionary<string, Type>> IntEnumPropertyMappings =
        new Dictionary<Type, IReadOnlyDictionary<string, Type>> {
            [typeof(ParcelCreateRequest)] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
                ["type"] = typeof(ParcelType),
                ["requestStatus"] = typeof(ApiRequestStatus),
                ["noReadType"] = typeof(NoReadType)
            },
            [typeof(ParcelUpdateRequest)] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
                ["operation"] = typeof(ParcelUpdateOperation),
                ["exceptionType"] = typeof(ContractParcelExceptionType),
                ["requestStatus"] = typeof(ApiRequestStatus)
            },
            [typeof(ParcelListRequest)] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
                ["status"] = typeof(ParcelStatus),
                ["exceptionType"] = typeof(ContractParcelExceptionType)
            },
            [typeof(ParcelListItemResponse)] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
                ["type"] = typeof(ParcelType),
                ["status"] = typeof(ParcelStatus),
                ["exceptionType"] = typeof(ContractParcelExceptionType),
                ["noReadType"] = typeof(NoReadType),
                ["requestStatus"] = typeof(ApiRequestStatus)
            }
        };

    /// <summary>
    /// 为枚举 Schema 注入中文描述。
    /// </summary>
    /// <param name="schema">OpenAPI 架构。</param>
    /// <param name="context">Schema 上下文。</param>
    public void Apply(OpenApiSchema schema, SchemaFilterContext context) {
        var enumType = Nullable.GetUnderlyingType(context.Type) ?? context.Type;
        if (!enumType.IsEnum) {
            ApplyIntEnumPropertyDescriptions(schema, context);
            return;
        }

        var members = BuildEnumLines(enumType);
        if (members.Length == 0) {
            return;
        }

        var enumTip = $"可选值：{string.Join("；", members)}";
        schema.Description = string.IsNullOrWhiteSpace(schema.Description)
            ? enumTip
            : $"{schema.Description}{Environment.NewLine}{enumTip}";
        var extension = new OpenApiArray();
        foreach (var member in members) {
            extension.Add(new OpenApiString(member));
        }

        schema.Extensions["x-enum-descriptions"] = extension;
    }

    /// <summary>
    /// 为 int 类型的“枚举值字段”追加中文可选值说明。
    /// </summary>
    /// <param name="schema">OpenAPI 架构。</param>
    /// <param name="context">Schema 上下文。</param>
    private static void ApplyIntEnumPropertyDescriptions(OpenApiSchema schema, SchemaFilterContext context) {
        if (!IntEnumPropertyMappings.TryGetValue(context.Type, out var propertyMappings)
            || schema.Properties.Count == 0) {
            return;
        }

        foreach (var property in schema.Properties) {
            if (!propertyMappings.TryGetValue(property.Key, out var mappedEnumType)) {
                continue;
            }

            var members = BuildEnumLines(mappedEnumType);
            if (members.Length == 0) {
                continue;
            }

            var enumTip = $"可选值：{string.Join("；", members)}";
            property.Value.Description = string.IsNullOrWhiteSpace(property.Value.Description)
                ? enumTip
                : $"{property.Value.Description}{Environment.NewLine}{enumTip}";
            var extension = new OpenApiArray();
            foreach (var member in members) {
                extension.Add(new OpenApiString(member));
            }

            property.Value.Extensions["x-enum-descriptions"] = extension;
        }
    }

    /// <summary>
    /// 构建枚举显示行。
    /// </summary>
    /// <param name="enumType">枚举类型。</param>
    /// <returns>“数值 = 名称（中文描述）”行列表。</returns>
    private static string[] BuildEnumLines(Type enumType) {
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
        var lines = new string[fields.Length];
        for (var index = 0; index < fields.Length; index++) {
            var field = fields[index];
            var value = Convert.ToInt64(field.GetRawConstantValue());
            var description = field.GetCustomAttribute<DescriptionAttribute>()?.Description;
            lines[index] = string.IsNullOrWhiteSpace(description)
                ? $"{value} = {field.Name}"
                : $"{value} = {field.Name}（{description}）";
        }

        return lines;
    }
}
