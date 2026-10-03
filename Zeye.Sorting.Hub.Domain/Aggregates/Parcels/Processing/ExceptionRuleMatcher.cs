using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Zeye.Sorting.Hub.Domain.Enums;
namespace Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
/// <summary>与前端条件编辑器共用字段语义的服务端异常分类器。</summary>
public static class ExceptionRuleMatcher {
    /// <summary>允许的比较运算符。</summary>
    private static readonly HashSet<string> Operators = ["等于", "不等于", "包含", "不包含", "大于", "小于", "大于或等于", "小于或等于", "为空", "不为空"];
    /// <summary>字段白名单。</summary>
    private static readonly HashSet<string> Fields = ["包裹重量", "包裹体积（长×宽×高）", "包裹长度", "包裹宽度", "包裹高度", "包裹类型", "条码", "Provider 名称", "Provider 响应内容", "响应状态码", "来源异常代码", "异常信息", "处理阶段", "目标格口", "实际格口", "路由阻断", "叠包标记", "包裹间距违规"];
    /// <summary>只接受非负十进制及科学记数法阈值。</summary>
    private static readonly Regex NumberPattern = new(@"^[+]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?$", RegexOptions.CultureInvariant);
    /// <summary>校验条件，拒绝无效字段、单位和缺失阈值。</summary>
    public static bool IsValid(ClassificationCondition condition) {
        if (condition is null || !Fields.Contains(condition.Field) || !Operators.Contains(condition.Operator) || condition.Value?.Length > 4096 || condition.Values?.Any(x => x is null || x.Length > 4096) == true) return false;
        if (condition.Operator is "为空" or "不为空") return true;
        if (IsNumeric(condition.Field)) return condition.Operator is not ("包含" or "不包含") && TryNumber(condition.Value, out var value) && UnitFactor(condition) is decimal factor && CanMultiply(value, factor);
        return condition.Operator is "等于" or "不等于" or "包含" or "不包含"
            && (condition.Values ?? [condition.Value ?? ""]).Any(x => !string.IsNullOrWhiteSpace(x))
            && condition.Values is not { Length: > 100 } && condition.Unit is null;
    }
    /// <summary>按规则顺序选择第一个已发布分类；系统兜底不参与正常比较。</summary>
    public static ParcelExceptionType? Match(IReadOnlyList<ClassificationRule> rules, IReadOnlyDictionary<string, object?> facts, string? workstation, string? sourceInstance) {
        foreach (var rule in rules) {
            if (rule.Status != "已发布" || rule.SystemRule == "unknown-fallback" || rule.ExceptionType is not > 0 || !Enum.IsDefined(typeof(ParcelExceptionType), rule.ExceptionType.Value) || rule.Conditions.Length == 0) continue;
            if (RuleMatches(rule, facts, workstation, sourceInstance)) return (ParcelExceptionType)rule.ExceptionType.Value;
        }
        return null;
    }
    /// <summary>按相同单位、条件关系和范围选择首个已发布的包裹类型规则。</summary>
    public static ParcelType? MatchParcel(IReadOnlyList<ClassificationRule> rules, IReadOnlyDictionary<string, object?> facts, string? workstation, string? sourceInstance) {
        foreach (var rule in rules) {
            if (rule.ExceptionType is not null || rule.ParcelType is not int type || !Enum.IsDefined(typeof(ParcelType), type)) continue;
            if (RuleMatches(rule, facts, workstation, sourceInstance)) return (ParcelType)type;
        }
        return null;
    }
    /// <summary>应用发布状态、字段校验和范围约束，避免空条件或无效配置命中。</summary>
    private static bool RuleMatches(ClassificationRule rule, IReadOnlyDictionary<string, object?> facts, string? workstation, string? sourceInstance) =>
        rule.Status == "已发布" && rule.Conditions.Length > 0 && rule.Conditions.All(IsValid) && ScopeMatches(rule.Scope, workstation, sourceInstance)
        && (rule.MatchMode == "any" ? rule.Conditions.Any(x => Matches(x, facts)) : rule.Conditions.All(x => Matches(x, facts)));
    /// <summary>收集当前包裹快照与最近一次 Provider 响应的匹配事实。</summary>
    public static IReadOnlyDictionary<string, object?> Facts(Parcel parcel, ParcelProcessingRecord record, ParcelProcessingRecord? provider) {
        var barcodes = new List<string>();
        if (!string.IsNullOrWhiteSpace(parcel.BarCodes)) barcodes.Add(parcel.BarCodes);
        if (!string.IsNullOrWhiteSpace(record.Barcode)) barcodes.Add(record.Barcode);
        if (!string.IsNullOrWhiteSpace(record.BarcodesJson)) {
            try { using var json = JsonDocument.Parse(record.BarcodesJson); if (json.RootElement.ValueKind == JsonValueKind.Array) barcodes.AddRange(json.RootElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!)); }
            catch (JsonException) { /* 旧历史中的异常 JSON 不产生文本匹配。 */ }
        }
        decimal? volume = parcel.Volume;
        if (volume is null && parcel.Length is decimal length && parcel.Width is decimal width && parcel.Height is decimal height) {
            try { volume = checked(length * width * height); } catch (OverflowException) { volume = null; }
        }
        return new Dictionary<string, object?> {
            ["包裹重量"] = parcel.Weight, ["包裹长度"] = parcel.Length, ["包裹宽度"] = parcel.Width, ["包裹高度"] = parcel.Height,
            ["包裹体积（长×宽×高）"] = volume, ["包裹类型"] = new[] { parcel.Type.ToString(), TypeName(parcel.Type) }, ["条码"] = barcodes,
            ["Provider 名称"] = provider?.Provider, ["Provider 响应内容"] = provider?.ResponseBody,
            ["响应状态码"] = provider?.ResponseStatusCode, ["来源异常代码"] = record.ExceptionCode,
            ["异常信息"] = record.ErrorMessage, ["处理阶段"] = record.Stage.ToString(), ["目标格口"] = parcel.TargetChuteCode,
            ["实际格口"] = parcel.ActualChuteCode, ["路由阻断"] = parcel.IsRoutingBlocked, ["叠包标记"] = parcel.IsSticking,
            ["包裹间距违规"] = record.IsSpacingViolation
        };
    }
    /// <summary>类型匹配同时接受页面中文名称及原始协议枚举名称。</summary>
    private static string TypeName(ParcelType type) => type switch { ParcelType.Normal => "普通包裹", ParcelType.Large => "大型包裹", ParcelType.Aggregated => "聚合包裹", ParcelType.UltraThin => "超薄包裹", ParcelType.Irregular => "异形件", ParcelType.Liquid => "流体包裹", ParcelType.Fragile => "易碎品", _ => type.ToString() };
    /// <summary>范围是全部产线，或工作站及来源实例的明确名称列表。</summary>
    private static bool ScopeMatches(string scope, string? workstation, string? sourceInstance) => scope is "全部产线" or "全部" or "全国"
        || scope.Split(['/', ',', '，', '、'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Any(x => x == workstation || x == sourceInstance);
    /// <summary>判断数值字段。</summary>
    private static bool IsNumeric(string field) => field is "包裹重量" or "包裹体积（长×宽×高）" or "包裹长度" or "包裹宽度" or "包裹高度" or "响应状态码";
    /// <summary>按字段获取基准单位换算因子。</summary>
    private static decimal? UnitFactor(ClassificationCondition c) => c.Field switch {
        "包裹重量" => c.Unit switch { null or "kg" => 1m, "g" => 0.001m, _ => null },
        "包裹体积（长×宽×高）" => c.Unit switch { null or "mm³" => 1m, "cm³" => 1000m, "m³" => 1000000000m, _ => null },
        "包裹长度" or "包裹宽度" or "包裹高度" => c.Unit switch { null or "mm" => 1m, "cm" => 10m, "m" => 1000m, _ => null },
        _ => c.Unit is null ? 1m : null
    };
    /// <summary>拒绝缺失、负数及无效数值，避免把未知量测转换为零。</summary>
    private static bool TryNumber(object? value, out decimal result) {
        var text = value is bool ? "" : Convert.ToString(value, CultureInfo.InvariantCulture);
        return text is not null && NumberPattern.IsMatch(text.Trim()) && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && result >= 0 || Fail(out result);
    }
    /// <summary>在短路分支回填输出值。</summary>
    private static bool Fail(out decimal value) { value = 0; return false; }
    /// <summary>在发布前拒绝会溢出的单位换算。</summary>
    private static bool CanMultiply(decimal value, decimal factor) { try { _ = checked(value * factor); return true; } catch (OverflowException) { return false; } }
    /// <summary>在准确的基准单位内比较数值和文本。</summary>
    private static bool Matches(ClassificationCondition c, IReadOnlyDictionary<string, object?> facts) {
        facts.TryGetValue(c.Field, out var actual);
        var texts = (actual is IEnumerable<string> list ? list : actual is null ? [] : new[] { actual is bool flag ? flag ? "true" : "false" : Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "" }).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        if (c.Operator == "为空") return texts.Length == 0;
        if (c.Operator == "不为空") return texts.Length > 0;
        if (texts.Length == 0) return false;
        if (IsNumeric(c.Field)) {
            if (!TryNumber(actual, out var value) || !TryNumber(c.Value, out var threshold) || UnitFactor(c) is not decimal factor || !CanMultiply(threshold, factor)) return false;
            var expected = threshold * factor;
            return c.Operator switch { "等于" => value == expected, "不等于" => value != expected, "大于" => value > expected, "小于" => value < expected, "大于或等于" => value >= expected, "小于或等于" => value <= expected, _ => false };
        }
        var comparison = c.Field == "来源异常代码" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var targets = (c.Values ?? [c.Value ?? ""]).Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
        if (targets.Length == 0) return false;
        var equal = texts.Any(x => targets.Any(y => x.Equals(y, comparison)));
        var contains = texts.Any(x => targets.Any(y => x.Contains(y, comparison)));
        return c.Operator switch { "等于" => equal, "不等于" => !equal, "包含" => contains, "不包含" => !contains, _ => false };
    }
}
