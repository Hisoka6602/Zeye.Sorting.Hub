using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Admin;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Host.Swagger;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// Swagger 文档增强回归测试。
/// </summary>
public sealed class SwaggerDocumentationTests {
    /// <summary>
    /// Swagger 文档版本。
    /// </summary>
    private const string ApiVersion = "v1";

    /// <summary>
    /// 验证场景：枚举型 int 字段会在 Swagger 中输出“数值 + 枚举名 + 中文描述”。
    /// </summary>
    [Fact]
    public async Task SwaggerJson_ShouldContainChineseEnumDescriptions_ForIntEnumFields() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options => {
            options.SwaggerDoc(ApiVersion, new OpenApiInfo {
                Title = "test",
                Version = ApiVersion
            });
            options.SchemaFilter<EnumDescriptionSchemaFilter>();
        });

        await using var app = builder.Build();
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

    /// <summary>
    /// 验证场景：值对象响应中的枚举型 int 字段会在 Swagger 中输出中文枚举说明。
    /// </summary>
    [Fact]
    public async Task SwaggerJson_ShouldContainChineseEnumDescriptions_ForValueObjectIntEnumFields() {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options => {
            options.SwaggerDoc(ApiVersion, new OpenApiInfo {
                Title = "test",
                Version = ApiVersion
            });
            options.SchemaFilter<EnumDescriptionSchemaFilter>();
        });

        await using var app = builder.Build();
        app.UseSwagger();
        app.MapPost("/swagger-enum-vo-barcode", (BarCodeInfoResponse request) => Results.Ok(request));
        app.MapPost("/swagger-enum-vo-command", (CommandInfoResponse request) => Results.Ok(request));
        app.MapPost("/swagger-enum-vo-image", (ImageInfoResponse request) => Results.Ok(request));
        app.MapPost("/swagger-enum-vo-video", (VideoInfoResponse request) => Results.Ok(request));
        await app.StartAsync();

        using var client = app.GetTestClient();
        var swaggerJson = await client.GetStringAsync("/swagger/v1/swagger.json");
        Assert.Contains("\"BarCodeInfoResponse\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"barCodeType\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("可选值：0 = ExpressSheet（快递面单条码）", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"CommandInfoResponse\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"protocolType\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("可选值：0 = IP（互联网协议）", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"actionType\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("0 = None（无）", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"direction\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("0 = Receive（接收）", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"ImageInfoResponse\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"imageType\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("可选值：0 = Scan（扫码图）", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"captureType\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("0 = Camera（相机获取）", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"VideoInfoResponse\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("\"nodeType\"", swaggerJson, StringComparison.Ordinal);
        Assert.Contains("可选值：0 = Scan（扫码）", swaggerJson, StringComparison.Ordinal);
    }
}
