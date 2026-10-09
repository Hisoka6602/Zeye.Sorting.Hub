using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Services.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.Processing;
using Zeye.Sorting.Hub.Contracts.Models.Parcels.ValueObjects;
using Zeye.Sorting.Hub.Host.Queries;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>图片预览使用已持久化的来源事实，保留无法访问的本地路径。</summary>
public sealed class ParcelImageApiTests {
    /// <summary>同一图片的登记与上传去重，失败记录排除，浏览器仅收到 HTTP(S) 地址。</summary>
    [Fact]
    public async Task ImageApiUsesPersistedFactsAndDeduplicatesRegistrationAndUpload() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(db); using var client = app.GetTestClient();
        var created = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(0, "detection"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var parcelId = (await created.Content.ReadFromJsonAsync<ParcelProcessingWriteResponse>())!.ParcelId;
        var records = new[] {
            Request(9, "registered") with { ImagePath = "https://images.example.test/parcel-42.jpg", ImageCamera = "顶部相机", IsSuccess = true },
            Request(10, "uploaded") with { ImagePath = "https://images.example.test/parcel-42.jpg", ImageCamera = "顶部相机", IsSuccess = true },
            Request(9, "local") with { ImagePath = @"D:\Fusion\images\42.jpg", ImageCamera = "侧面相机", IsSuccess = true },
            Request(10, "failed") with { ImagePath = "https://images.example.test/failed.jpg", IsSuccess = false }
        };
        foreach (var record in records) Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/admin/parcels/processing-records", record)).StatusCode);
        var response = await client.GetAsync($"/api/parcels/{parcelId}/images");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var images = (await response.Content.ReadFromJsonAsync<ParcelImagesResponse>())!;
        Assert.Equal(parcelId, images.ParcelId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(images.HasImages); Assert.Equal(2, images.Images.Count);
        var top = Assert.Single(images.Images, x => x.CameraName == "顶部相机");
        Assert.Equal("https://images.example.test/parcel-42.jpg", top.Url);
        var side = Assert.Single(images.Images, x => x.CameraName == "侧面相机");
        Assert.Null(side.Url); Assert.Equal(@"D:\Fusion\images\42.jpg", side.SourcePath);
        Assert.Contains("来源尚未提供", side.UnavailableReason);
    }

    /// <summary>没有图片的包裹返回空集合；不存在和无效编号分别返回404和400。</summary>
    [Fact]
    public async Task EmptyMissingAndInvalidParcelsHaveExplicitResults() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(db); using var client = app.GetTestClient();
        var created = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(0, "detection"));
        var parcelId = (await created.Content.ReadFromJsonAsync<ParcelProcessingWriteResponse>())!.ParcelId;
        var images = await client.GetFromJsonAsync<ParcelImagesResponse>($"/api/parcels/{parcelId}/images");
        Assert.False(images!.HasImages); Assert.Empty(images.Images);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/parcels/9223372036854775806/images")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/parcels/0/images")).StatusCode);
    }

    /// <summary>对象存储元数据优先于本地来源路径，签名失败保留图片信息供重试。</summary>
    [Fact]
    public async Task ObjectStorageMetadataUsesReadSignaturesAndReportsFailures() {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(db); using var client = app.GetTestClient();
        var created = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(0, "detection"));
        var parcelId = (await created.Content.ReadFromJsonAsync<ParcelProcessingWriteResponse>())!.ParcelId;
        await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(9, "local") with { ImagePath = "images/42.jpg" });
        using var scope = app.Services.CreateScope();
        var original = (await scope.ServiceProvider.GetRequiredService<GetParcelByIdQueryService>().ExecuteAsync(long.Parse(parcelId!, System.Globalization.CultureInfo.InvariantCulture), default))!;
        var parcel = original with { ImageInfos = [new ImageInfoResponse { CameraName = "顶部相机", CustomName = "", CameraSerialNumber = "camera-01", ImageType = 0, CaptureType = 0, RelativePath = "images/42.jpg", StorageProvider = 1, BucketName = "parcel-images", ObjectKey = "sorter-01/42.jpg" }] };
        var signed = await ParcelImageCatalog.BuildAsync(parcel, (bucket, key, _) => {
            Assert.Equal("parcel-images", bucket); Assert.Equal("sorter-01/42.jpg", key);
            return Task.FromResult("https://storage.example.test/parcel-images/sorter-01/42.jpg?signature=test");
        }, default);
        Assert.Single(signed.Images); Assert.Contains("signature=test", signed.Images[0].Url);
        var failed = await ParcelImageCatalog.BuildAsync(parcel, (_, _, _) => throw new InvalidOperationException("test storage offline"), default);
        Assert.Null(Assert.Single(failed.Images).Url); Assert.Contains("请重试", failed.Images[0].UnavailableReason);
        var disabled = await ParcelImageCatalog.BuildAsync(parcel, null, default);
        Assert.Null(Assert.Single(disabled.Images).Url); Assert.Contains("尚未启用", disabled.Images[0].UnavailableReason);
    }

    /// <summary>来源路径不能成为脚本、文件或携带访问凭据的浏览器地址。</summary>
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/images/42.jpg")]
    [InlineData("https://user:secret@images.example.test/42.jpg")]
    public async Task NonWebAndCredentialedPathsAreMetadataOnly(string path) {
        await using var db = new RelationalParcelTestDatabase(); await db.InitializeAsync();
        await using var app = await FusionApiTestHost.CreateAsync(db); using var client = app.GetTestClient();
        var created = await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(0, "detection"));
        var parcelId = (await created.Content.ReadFromJsonAsync<ParcelProcessingWriteResponse>())!.ParcelId;
        await client.PostAsJsonAsync("/api/admin/parcels/processing-records", Request(9, "image") with { ImagePath = path });
        var images = await client.GetFromJsonAsync<ParcelImagesResponse>($"/api/parcels/{parcelId}/images");
        Assert.Null(Assert.Single(images!.Images).Url);
    }

    /// <summary>使用同一来源身份构造隔离图片测试记录。</summary>
    private static ParcelProcessingRecordRequest Request(int stage, string recordId) => new() {
        RecordId = recordId, SourceInstanceId = "image-api-test", SourceRunId = "image-session", SourceParcelId = 42,
        Stage = stage, OccurredAt = DateTime.Now.Date.AddHours(10).AddSeconds(stage)
    };
}
