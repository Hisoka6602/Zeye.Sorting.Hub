using System.Text.Json;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Host.Queries;
namespace Zeye.Sorting.Hub.Host.Routing;
/// <summary>规则集中持久化与发布入口，服务端保护系统异常兜底。</summary>
public static class RuleManagementApiRouteExtensions {
    /// <summary>规则存储采用前端合同一致的驼峰字段。</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>注册分类规则读取及带版本的提交接口。</summary>
    public static IEndpointRouteBuilder MapRuleManagementApis(this IEndpointRouteBuilder routes) {
        var group = routes.MapGroup("/api/operations/rules");
        group.MapGet("/{category}", async (string category, ManagedDocumentService store, CancellationToken ct) => {
            if (category is not ("parcel" or "exception")) return Results.NotFound();
            var document = await store.ReadAsync("rules-" + category, ct);
            var rules = document is null ? category == "exception" ? ClassificationRuleDefaults.Create() : [] : JsonSerializer.Deserialize<ClassificationRule[]>(document.Json, JsonOptions)!;
            return Results.Ok(new { revision = document?.Revision ?? 0, rules });
        }).WithSummary("读取包裹或异常分类规则")
            .WithDescription("category 为 parcel 或 exception，返回对应分类规则及版本；异常分类默认包含分拣机异常和不可删除的未知异常兜底。");
        group.MapPut("/{category}", async (string category, RuleWriteRequest request, ManagedDocumentService store, HttpContext context, CancellationToken ct) => {
            if (category is not ("parcel" or "exception")) return Results.NotFound();
            if (request.Rules is null || request.Rules.Any(x => x is null) || request.Rules.Length > 200 || request.ExpectedRevision < 0 || request.Rules.Select(x => x.Id).Distinct().Count() != request.Rules.Length)
                return Results.Problem(statusCode: 400, detail: "规则数量、版本号或编号无效。");
            if (category == "exception") {
                foreach (var system in ClassificationRuleDefaults.Create()) {
                    var supplied = request.Rules.SingleOrDefault(x => x.Id == system.Id);
                    if (supplied is null || JsonSerializer.Serialize(supplied, JsonOptions) != JsonSerializer.Serialize(system, JsonOptions))
                        return Results.Problem(statusCode: 400, detail: "系统默认异常及未知异常兜底不可修改或删除。");
                }
            }
            foreach (var rule in request.Rules.Where(x => x.SystemRule is null)) {
                if (rule.Id <= 0 || string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Length > 100 || string.IsNullOrWhiteSpace(rule.Scope)
                    || rule.Scope.Length > 256 || rule.Description is null || rule.Description.Length > 2048 || rule.Conditions is null || rule.Conditions.Length is < 1 or > 50 || rule.Conditions.Any(x => x is null) || rule.Actions is null || rule.Actions.Length > 20 || rule.MatchMode is not ("all" or "any") || rule.Status is not ("草稿" or "已发布"))
                    return Results.Problem(statusCode: 400, detail: "请检查规则名称、范围、状态和条件。");
                if (category == "exception" && (rule.ExceptionType is not > 0 || !Enum.IsDefined(typeof(ParcelExceptionType), rule.ExceptionType.Value) || rule.Conditions.Any(x => !ExceptionRuleMatcher.IsValid(x))))
                    return Results.Problem(statusCode: 400, detail: "异常类型、匹配字段、运算符或单位无效。");
                if (category == "parcel" && (rule.Status == "已发布" || rule.ParcelType is not null) && (rule.ExceptionType is not null || rule.ParcelType is not int type || !Enum.IsDefined(typeof(ParcelType), type) || rule.Conditions.Any(x => !ExceptionRuleMatcher.IsValid(x))))
                    return Results.Problem(statusCode: 400, detail: "包裹分类、匹配字段、运算符或单位无效。");
                if (category == "exception" && rule.ParcelType is not null)
                    return Results.Problem(statusCode: 400, detail: "异常规则不能同时设置包裹分类。");
                if (rule.Status == "已发布" && (rule.Actions.Length == 0 || (category == "exception" ? rule.Actions.Any(x => x is not ("标记分拣异常" or "记录异常原因")) : rule.Actions.Any(x => x != "标记包裹类型"))))
                    return Results.Problem(statusCode: 400, detail: "请选择当前支持的分类标记动作。格口命令仍由 Fusion 分拣机链路执行。");
            }
            if (request.Rules.Any(x => x.SystemRule is not null && !ClassificationRuleDefaults.Create().Any(s => s.Id == x.Id)))
                return Results.Problem(statusCode: 400, detail: "不能伪造系统规则。");
            var rules = request.Rules.Select(x => x.SystemRule is null ? x with { Modified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), Editor = context.User.Identity?.Name ?? "操作员" } : x).ToArray();
            var saved = await store.WriteAsync("rules-" + category, JsonSerializer.Serialize(rules, JsonOptions), request.ExpectedRevision, ct);
            return saved is null ? Results.Problem(statusCode: 409, detail: "规则已被其他操作更新，请刷新后重试。") : Results.Ok(new { revision = saved.Revision, rules });
        }).WithSummary("保存包裹或异常分类规则")
            .WithDescription("按期望版本校验并保存分类条件、标记动作及发布状态；系统默认异常和未知异常兜底不可修改或删除，格口命令仍由 Fusion 分拣链路执行。");
        return routes;
    }
}
