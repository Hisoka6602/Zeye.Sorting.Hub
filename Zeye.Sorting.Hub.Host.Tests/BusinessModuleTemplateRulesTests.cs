using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Zeye.Sorting.Hub.Application.Utilities;
using Zeye.Sorting.Hub.Host.Routing;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 业务模块接入模板规则测试。
/// </summary>
public sealed class BusinessModuleTemplateRulesTests {
    /// <summary>
    /// Swagger 文档版本。
    /// </summary>
    private const string ApiVersion = "v1";

    /// <summary>
    /// 应用层统一结果模型应输出稳定错误码与状态码。
    /// </summary>
    [Fact]
    public void ApplicationResult_WhenUsingCommonFactories_ShouldExposeStableCodes() {
        var validation = ApplicationResult.ValidationFailed("名称不能为空");
        var conflict = ApplicationResult.Conflict("业务键冲突");
        var notFound = ApplicationResult.NotFound("未找到任务");

        Assert.False(validation.IsSuccess);
        Assert.Equal(ApplicationErrorCodes.ValidationFailed, validation.ErrorCode);
        Assert.Equal(ApplicationResult.BadRequestStatusCode, validation.StatusCode);
        Assert.Equal(ApplicationErrorCodes.Conflict, conflict.ErrorCode);
        Assert.Equal(ApplicationResult.ConflictStatusCode, conflict.StatusCode);
        Assert.Equal(ApplicationErrorCodes.NotFound, notFound.ErrorCode);
        Assert.Equal(ApplicationResult.NotFoundStatusCode, notFound.StatusCode);
    }

    /// <summary>
    /// 路由约定扩展应输出统一标签、ProblemDetails 声明与问题详情响应。
    /// </summary>
    /// <returns>异步任务。</returns>
    [Fact]
    public async Task EndpointRouteBuilderConventionExtensions_ShouldApplyModuleConventions() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options => {
            options.SwaggerDoc(ApiVersion, new OpenApiInfo {
                Title = "test",
                Version = ApiVersion
            });
        });

        await using var app = builder.Build();
        app.UseSwagger();
        var group = app.MapBusinessModuleGroup("/api/template-module", "TemplateModule");
        group.MapGet("/success", static () => Results.Ok(new { Ok = true }))
            .WithBusinessModuleEndpointConvention(
                "TemplateModuleSuccess",
                "模板模块成功接口",
                "用于验证业务模块路由约定是否附加统一名称、说明与错误声明。",
                ApplicationResult.BadRequestStatusCode,
                ApplicationResult.ConflictStatusCode);
        group.MapGet("/conflict", static () => ApplicationResult.Conflict("业务键已存在").ToProblemResult())
            .WithBusinessModuleEndpointConvention(
                "TemplateModuleConflict",
                "模板模块冲突接口",
                "用于验证应用层失败结果是否映射为统一 ProblemDetails。",
                ApplicationResult.BadRequestStatusCode,
                ApplicationResult.ConflictStatusCode);
        await app.StartAsync();

        using var client = app.GetTestClient();
        var swaggerJson = await client.GetStringAsync("/swagger/v1/swagger.json");
        var conflictResponse = await client.GetAsync("/api/template-module/conflict");
        var conflictText = await conflictResponse.Content.ReadAsStringAsync();
        using var conflictDocument = JsonDocument.Parse(conflictText);

        Assert.Contains("\"/api/template-module/success\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"TemplateModule\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"400\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"409\"", swaggerJson, StringComparison.Ordinal);
        Assert.Equal("application/problem+json", conflictResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(ApplicationResult.ConflictStatusCode, (int)conflictResponse.StatusCode);
        Assert.Equal("请求冲突", conflictDocument.RootElement.GetProperty("title").GetString());
        Assert.Equal("业务键已存在", conflictDocument.RootElement.GetProperty("detail").GetString());
        Assert.Equal(ApplicationErrorCodes.Conflict, conflictDocument.RootElement.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// 文档模板应覆盖业务模块接入的关键治理要求。
    /// </summary>
    [Fact]
    public void BusinessModuleTemplateDocuments_ShouldContainRequiredRules() {
        var repositoryRoot = LocateRepositoryRoot();
        var moduleConvention = File.ReadAllText(Path.Combine(repositoryRoot, "业务模块接入规范.md"));
        var copilotTemplate = File.ReadAllText(Path.Combine(repositoryRoot, "Copilot-业务模块新增模板.md"));

        Assert.Contains("高频列表必须优先游标分页", moduleConvention, StringComparison.Ordinal);
        Assert.Contains("写入必须考虑幂等", moduleConvention, StringComparison.Ordinal);
        Assert.Contains("需要业务事件持久化时必须优先使用 Outbox", moduleConvention, StringComparison.Ordinal);
        Assert.Contains("OperationalScopeNormalizer", moduleConvention, StringComparison.Ordinal);
        Assert.Contains("ApplicationResult", copilotTemplate, StringComparison.Ordinal);
        Assert.Contains("EndpointRouteBuilderConventionExtensions", copilotTemplate, StringComparison.Ordinal);
        Assert.Contains("先输出实施计划（Plan）", copilotTemplate, StringComparison.Ordinal);
    }

    /// <summary>
    /// 定位仓库根目录。
    /// </summary>
    /// <returns>仓库根目录绝对路径。</returns>
    private static string LocateRepositoryRoot() {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null) {
            var readmePath = Path.Combine(current.FullName, "README.md");
            var solutionPath = Path.Combine(current.FullName, "Zeye.Sorting.Hub.sln");
            if (File.Exists(readmePath) && File.Exists(solutionPath)) {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("未找到仓库根目录。");
    }
}
