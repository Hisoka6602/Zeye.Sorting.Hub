using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Zeye.Sorting.Hub.Domain.Aggregates.Parcels.Processing;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Enums.Parcels;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Host.Routing;
namespace Zeye.Sorting.Hub.Host.Tests;
/// <summary>规则持久化、系统兜底保护与实际处理分类的关系数据库回归测试。</summary>
public sealed class ManagedRuleApiTests {
    /// <summary>包裹规则可发布且作用于真实量测，草稿不执行，也不篡改分拣机格口事实。</summary>
    [Fact]
    public async Task ParcelRulesPublishClassifyAndPreserveSorterFacts() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db); using var client = app.GetTestClient();
        var rule = new ClassificationRule { Id = 1, Name = "重量大件", Description = "按真实 DWS 重量分类", Status = "已发布", ParcelType = 1, TargetType = "大型包裹", Actions = ["标记包裹类型"], Conditions = [new() { Field = "包裹重量", Operator = "大于", Value = "1000", Unit = "g" }] };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/operations/rules/parcel", new RuleWriteRequest { Rules = [rule] })).StatusCode);
        var exception = WeightRule("1", "kg") with { Conditions = [new() { Field = "包裹类型", Operator = "等于", Values = ["大型包裹"] }] };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/operations/rules/exception", new RuleWriteRequest { Rules = [.. ClassificationRuleDefaults.Create(), exception] })).StatusCode);
        var first = Fact("parcel-type-detected"); var created = await db.Processing.AppendAsync(first, default); Assert.True(created.IsSuccess);
        var measured = first with { RecordId = "parcel-type-dws", Stage = ParcelProcessingStage.DwsBound, OccurredAt = first.OccurredAt.AddSeconds(1), WeightGrams = 1000.01m, FinalSourceParcelId = first.SourceParcelId };
        Assert.True((await db.Processing.AppendAsync(measured, default)).IsSuccess);
        var parcel = await db.Parcels.GetByIdAsync(created.Value!.ParcelId!.Value, default); Assert.Equal(ParcelType.Large, parcel!.Type); Assert.Null(parcel.TargetChuteCode);
        Assert.Equal(ParcelExceptionType.MechanicalFailure, parcel.ExceptionType);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/operations/rules/parcel", new RuleWriteRequest { ExpectedRevision = 1, Rules = [rule with { Status = "草稿" }] })).StatusCode);
        Assert.True((await db.Processing.AppendAsync(measured with { RecordId = "parcel-type-remeasure", OccurredAt = first.OccurredAt.AddSeconds(2) }, default)).IsSuccess);
        Assert.Equal(ParcelType.Normal, (await db.Parcels.GetByIdAsync(created.Value.ParcelId.Value, default))!.Type);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/operations/rules/parcel", new RuleWriteRequest { ExpectedRevision = 2, Rules = [rule with { Actions = ["分流至专用分拣口"] }] })).StatusCode);
    }
    /// <summary>默认异常完整，拒绝删改兜底，版本冲突不覆盖用户规则。</summary>
    [Fact]
    public async Task RulesPersistProtectFallbackAndRejectStaleWrites() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await CreateAsync(db);
        using var client = app.GetTestClient();
        var initial = await client.GetFromJsonAsync<JsonElement>("/api/operations/rules/exception");
        Assert.Equal(4, initial.GetProperty("rules").GetArrayLength());
        var system = ClassificationRuleDefaults.Create();
        var missing = await client.PutAsJsonAsync("/api/operations/rules/exception", new RuleWriteRequest { Rules = system.Where(x => x.ExceptionType != 0).ToArray() });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var custom = WeightRule("1.25", "kg");
        var request = new RuleWriteRequest { Rules = [.. system, custom] };
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/operations/rules/exception", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/operations/rules/exception", request)).StatusCode);
        var reloaded = await client.GetFromJsonAsync<JsonElement>("/api/operations/rules/exception");
        Assert.Equal(1, reloaded.GetProperty("revision").GetInt32());
        Assert.Equal(5, reloaded.GetProperty("rules").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/operations/rules/exception", new RuleWriteRequest { ExpectedRevision = 1, Rules = [.. system, custom with { Name = "更新后的重量异常" }] })).StatusCode);
        var updated = await client.GetFromJsonAsync<JsonElement>("/api/operations/rules/exception");
        Assert.Equal(2, updated.GetProperty("revision").GetInt32());
        Assert.Contains("更新后的重量异常", updated.GetProperty("rules").EnumerateArray().Select(x => x.GetProperty("name").GetString()));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/operations/rules/exception", new RuleWriteRequest { ExpectedRevision = 1, Rules = [.. system, custom with { Conditions = [new() { Field = "包裹重量", Operator = "大于", Value = "0xFF", Unit = "kg" }] }] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/operations/rules/exception", new RuleWriteRequest { ExpectedRevision = 1, Rules = [.. system, custom with { Actions = ["发送异常告警"] }] })).StatusCode);
    }
    /// <summary>发布规则会分类真实 DWS 事实，重建服务与数据库连接后仍有效，完成后晚到异常不倒退。</summary>
    [Fact]
    public async Task PublishedRulesClassifyRealMeasurementsAndSurviveReload() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        var store = new ManagedDocumentService(db.Factory);
        Assert.NotNull(await store.WriteAsync("rules-exception", JsonSerializer.Serialize<ClassificationRule[]>([.. ClassificationRuleDefaults.Create(), WeightRule("1250.25", "g")], RuleManagementApiRouteExtensions.JsonOptions), 0, default));
        var detected = Fact("detected");
        var created = await db.Processing.AppendAsync(detected, default); Assert.True(created.IsSuccess, created.ErrorMessage);
        var bound = detected with { RecordId = "measurement", Stage = ParcelProcessingStage.DwsBound, OccurredAt = detected.OccurredAt.AddSeconds(1), WeightGrams = 1250.25m, FinalSourceParcelId = 42 };
        var measured = await db.Processing.AppendAsync(bound, default); Assert.True(measured.IsSuccess, measured.ErrorMessage);
        var parcel = await db.Parcels.GetByIdAsync(created.Value!.ParcelId!.Value, default);
        Assert.Equal(ParcelExceptionType.MechanicalFailure, parcel!.ExceptionType);
        Assert.Equal(1.25025m, parcel.Weight);
        var completed = detected with { RecordId = "completed", Stage = ParcelProcessingStage.SortingCompleted, OccurredAt = detected.OccurredAt.AddSeconds(2), ActualChuteCode = "01" };
        Assert.True((await db.Processing.AppendAsync(completed, default)).IsSuccess);
        Assert.True((await db.Processing.AppendAsync(detected with { RecordId = "late", Stage = ParcelProcessingStage.ParcelException, OccurredAt = detected.OccurredAt.AddSeconds(3), ExceptionCode = "FutureFault" }, default)).IsSuccess);
        parcel = await db.Parcels.GetByIdAsync(created.Value.ParcelId.Value, default);
        Assert.Equal(ParcelStatus.Completed, parcel!.Status); Assert.Null(parcel.ExceptionType);
        var saved = await new ManagedDocumentService(db.Factory).ReadAsync("rules-exception", default);
        Assert.Contains("1250.25", saved!.Json);
    }
    /// <summary>各种数值条件使用精确单位，缺失值和非法阈值不能误判。</summary>
    [Theory]
    [InlineData("小于", "2", "kg", true)]
    [InlineData("大于", "1250", "g", true)]
    [InlineData("等于", "1.25025", "kg", true)]
    [InlineData("等于", "1250.25", "g", true)]
    [InlineData("不等于", "1.25025", "kg", false)]
    [InlineData("大于", "2", "kg", false)]
    public void DecimalUnitsAndComparisons(string op, string value, string unit, bool expected) {
        var rule = WeightRule(value, unit) with { Conditions = [new() { Field = "包裹重量", Operator = op, Value = value, Unit = unit }] };
        Assert.Equal(expected, ExceptionRuleMatcher.Match([rule], new Dictionary<string, object?> { ["包裹重量"] = 1.25025m }, "站点1", "sorter") is not null);
        Assert.Null(ExceptionRuleMatcher.Match([rule], new Dictionary<string, object?>(), "站点1", "sorter"));
    }
    /// <summary>体积、尺寸、多个条码、原始 Provider 正文、且或关系和产线范围正确匹配。</summary>
    [Fact]
    public void VolumeBarcodeProviderAndScopeUseRealFacts() {
        var conditions = new ClassificationCondition[] {
            new() { Field = "包裹体积（长×宽×高）", Operator = "等于", Value = "6", Unit = "cm³" },
            new() { Field = "包裹长度", Operator = "等于", Value = "1", Unit = "cm" },
            new() { Field = "条码", Operator = "包含", Values = ["SF", "不存在"] },
            new() { Field = "Provider 名称", Operator = "等于", Values = ["供应商甲"] },
            new() { Field = "Provider 响应内容", Operator = "包含", Values = ["无路由"] },
        };
        var rule = WeightRule("1", "kg") with { Scope = "站点1 / 站点2", Conditions = conditions };
        var facts = new Dictionary<string, object?> { ["包裹体积（长×宽×高）"] = 6000m, ["包裹长度"] = 10m, ["条码"] = new[] { "001", "SF0001" }, ["Provider 名称"] = "供应商甲", ["Provider 响应内容"] = "{\"code\":\"失败\",\"message\":\"无路由\"}" };
        Assert.Equal(ParcelExceptionType.MechanicalFailure, ExceptionRuleMatcher.Match([rule], facts, "站点1", "sorter"));
        Assert.Null(ExceptionRuleMatcher.Match([rule], facts, "其他站点", "sorter"));
        facts["Provider 名称"] = "其他供应商";
        Assert.Null(ExceptionRuleMatcher.Match([rule], facts, "站点1", "sorter"));
        Assert.NotNull(ExceptionRuleMatcher.Match([rule with { MatchMode = "any" }], facts, "站点1", "sorter"));
        Assert.Null(ExceptionRuleMatcher.Match([rule with { Status = "草稿" }], facts, "站点1", "sorter"));
    }
    /// <summary>无效数值、单位、操作符和空匹配值不能发布。</summary>
    [Theory]
    [InlineData("包裹重量", "大于", "-1", "kg")]
    [InlineData("包裹重量", "大于", "0x10", "kg")]
    [InlineData("包裹重量", "包含", "1", "kg")]
    [InlineData("包裹长度", "等于", "1", "kg")]
    [InlineData("包裹重量", "大于", "1e999", "kg")]
    [InlineData("条码", "等于", "", null)]
    public void InvalidConditionsAreRejected(string field, string op, string value, string? unit) => Assert.False(ExceptionRuleMatcher.IsValid(new() { Field = field, Operator = op, Value = value, Unit = unit }));
    /// <summary>建立与生产相同存储、路由的隔离规则宿主。</summary>
    private static async Task<WebApplication> CreateAsync(RelationalParcelTestDatabase database) {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddSingleton(database.Factory); builder.Services.AddScoped<ManagedDocumentService>();
        var app = builder.Build(); app.MapRuleManagementApis(); await app.StartAsync(); return app;
    }
    /// <summary>测试规则仅执行实际支持的分类标记。</summary>
    private static ClassificationRule WeightRule(string value, string unit) => new() { Id = 1, Name = "测试重量异常", Description = "测试发布规则", Status = "已发布", ExceptionType = 11, TargetType = "机械故障", Conditions = [new() { Field = "包裹重量", Operator = "等于", Value = value, Unit = unit }], Actions = ["标记分拣异常"] };
    /// <summary>建立独立来源的有效处理事实。</summary>
    private static ParcelProcessingRecord Fact(string id) => new() { RecordId = id, SourceInstanceId = "managed-rule-test", SourceRunId = "run", SourceParcelId = 42, Stage = ParcelProcessingStage.Detected, OccurredAt = new(2026, 10, 2, 10, 0, 0), RecordedAt = new(2026, 10, 2, 10, 0, 0), PartitionTime = new(2026, 10, 2, 10, 0, 0), PayloadHash = id, IsSuccess = true };
}
