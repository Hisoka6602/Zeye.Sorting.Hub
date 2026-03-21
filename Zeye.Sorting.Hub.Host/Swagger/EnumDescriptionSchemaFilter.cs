using System.ComponentModel;
using System.Reflection;
using Zeye.Sorting.Hub.Contracts.Enums.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;
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
            },
            [typeof(BarCodeInfoResponse)] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
                ["barCodeType"] = typeof(BarCodeType)
            },
            [typeof(CommandInfoResponse)] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
                ["protocolType"] = typeof(System.Net.Sockets.ProtocolType),
                ["actionType"] = typeof(ActionType),
                ["direction"] = typeof(CommandDirection)
            },
            [typeof(ImageInfoResponse)] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
                ["imageType"] = typeof(ImageType),
                ["captureType"] = typeof(ImageCaptureType)
            },
            [typeof(VideoInfoResponse)] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
                ["nodeType"] = typeof(VideoNodeType)
            },
            [typeof(ApiRequestInfoResponse)] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
                ["apiType"] = typeof(ApiRequestType),
                ["requestStatus"] = typeof(ApiRequestStatus)
            },
            [typeof(VolumeInfoResponse)] = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase) {
                ["sourceType"] = typeof(VolumeSourceType)
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
            var description = ResolveEnumDescription(enumType, field);
            lines[index] = string.IsNullOrWhiteSpace(description)
                ? $"{value} = {field.Name}"
                : $"{value} = {field.Name}（{description}）";
        }

        return lines;
    }

    /// <summary>
    /// 解析枚举项中文描述；若未配置 Description，则使用内置兜底文案。
    /// </summary>
    /// <param name="enumType">枚举类型。</param>
    /// <param name="field">枚举字段。</param>
    /// <returns>中文描述。</returns>
    private static string ResolveEnumDescription(Type enumType, FieldInfo field) {
        var attributeDescription = field.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (!string.IsNullOrWhiteSpace(attributeDescription)) {
            return attributeDescription;
        }

        if (enumType == typeof(System.Net.Sockets.ProtocolType)
            && TryResolveProtocolTypeDescription(field.Name, out var protocolDescription)) {
            return protocolDescription;
        }

        return "未提供中文描述";
    }

    /// <summary>
    /// 解析常见网络协议类型的中文说明。
    /// </summary>
    /// <param name="name">枚举名称。</param>
    /// <param name="description">解析出的中文描述。</param>
    /// <returns>解析成功返回 true。</returns>
    private static bool TryResolveProtocolTypeDescription(string name, out string description) {
        description = name switch {
            "IP" => "互联网协议",
            "IPv6" => "IPv6 协议",
            "Tcp" => "传输控制协议",
            "Udp" => "用户数据报协议",
            "Icmp" => "Internet 控制报文协议",
            "Igmp" => "Internet 组管理协议",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(description);
    }
}
