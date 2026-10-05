using Microsoft.AspNetCore.Mvc;
using Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Host.Queries;
using Zeye.Sorting.Hub.Host.Utilities;

namespace Zeye.Sorting.Hub.Host.Routing;

/// <summary>按需查询包裹图片，沿用包裹读取权限。</summary>
public static class ParcelImageApiRouteExtensions {
    /// <summary>记录包裹图片查询和临时地址生成异常。</summary>
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>注册包裹图片读取接口。</summary>
    /// <param name="routeBuilder">路由构建器。</param>
    /// <returns>路由构建器。</returns>
    public static IEndpointRouteBuilder MapParcelImageApis(this IEndpointRouteBuilder routeBuilder) {
        routeBuilder.MapGet("/api/parcels/{id:long}/images", GetImagesAsync)
            .WithTags("Parcels").WithName("GetParcelImages")
            .WithSummary("读取包裹图片及预览地址")
            .WithDescription("按包裹编号读取所有已关联图片及可用预览地址，用于多图浏览；沿用包裹读取权限，必要时生成临时签名地址，不将来源本地路径转换为任意文件读取。")
            .Produces<ParcelImagesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest);
        return routeBuilder;
    }

    /// <summary>按包裹编号读取已保存图片并生成临时访问地址。</summary>
    private static async Task<IResult> GetImagesAsync(long id, HttpContext context, GetParcelByIdQueryService queryService, [FromServices] IConfiguration configuration, CancellationToken cancellationToken) {
        ParcelDetailResponse? parcel;
        try { parcel = await queryService.ExecuteAsync(id, cancellationToken); }
        catch (ArgumentException exception) { Logger.Debug(exception, "包裹图片查询参数无效。"); return LocalDateTimeParsing.CreateBadRequestProblem("请求参数无效", exception.Message); }
        if (parcel is null) return LocalDateTimeParsing.CreateParcelMissingProblem(id);
        // 临时签名不缓存；不会把来源本地路径转换成任意文件读取接口。
        context.Response.Headers.CacheControl = "private, no-store";
        Func<string, string, CancellationToken, Task<string>>? signer = null;
        if (configuration.GetValue<bool?>("ObjectStorage:Minio:IsEnabled") != false) {
            signer = async (bucket, key, token) => {
                try {
                    var storage = context.RequestServices.GetRequiredService<IObjectStorageService>();
                    var session = await storage.CreateReadSessionAsync(new() { BucketName = bucket, ObjectKey = key }, token);
                    return session.Url;
                }
                catch (Exception exception) when (exception is not OperationCanceledException) {
                    Logger.Warn(exception, "获取包裹 {0} 的图片地址失败。", id);
                    throw;
                }
            };
        }
        return Results.Ok(await ParcelImageCatalog.BuildAsync(parcel, signer, cancellationToken));
    }
}
