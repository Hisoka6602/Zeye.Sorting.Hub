using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Host.Swagger;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Swagger 文档增强回归测试。
/// </summary>
public sealed class SwaggerDocumentationTests {
    /// <summary>
    /// 验证场景：枚举型 int 字段会在 Swagger 中输出“数值 + 枚举名 + 中文描述”。
    /// </summary>
    [Fact]
    public async Task SwaggerJson_ShouldContainChineseEnumDescriptions_ForIntEnumFields() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options => {
            options.SchemaFilter<EnumDescriptionSchemaFilter>();
        });

        var app = builder.Build();
        app.UseSwagger();
        app.MapPost("/swagger-enum-test", (ParcelUpdateRequest request) => Results.Ok(request));
        await app.StartAsync();

        using var client = app.GetTestClient();
        var swaggerJson = await client.GetStringAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(swaggerJson);
        var schemaText = document.RootElement.GetProperty("components")
            .GetProperty("schemas")
            .GetProperty("ParcelUpdateRequest")
            .ToString();

        Assert.Contains("可选值：1 = MarkCompleted（标记完结）", schemaText, StringComparison.Ordinal);
        Assert.Contains("2 = MarkSortingException（标记分拣异常）", schemaText, StringComparison.Ordinal);
        Assert.Contains("3 = UpdateRequestStatus（更新接口访问状态）", schemaText, StringComparison.Ordinal);
    }
}
