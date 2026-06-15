using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;
using Zeye.Sorting.Hub.Infrastructure.DependencyInjection;
using Zeye.Sorting.Hub.Infrastructure.ObjectStorage;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// MinIO 对象存储服务回归测试。
/// </summary>
public sealed class MinioObjectStorageServiceTests {
    /// <summary>
    /// 验证场景：单对象上传预签名会话会返回 PUT 地址与 Content-Type 头。
    /// </summary>
    [Fact]
    public async Task CreateUploadSessionAsync_ShouldReturnPutSessionWithContentTypeHeader() {
        var service = CreateService();

        var session = await service.CreateUploadSessionAsync(new CreateObjectStorageUploadSessionRequest {
            BucketName = "sorting-hub-parcel-images",
            ObjectKey = "images/2026/06/15/parcel-001/top-cam.jpg",
            ContentType = "image/jpeg",
            ObjectSizeBytes = 1024
        }, CancellationToken.None);

        Assert.Equal("PUT", session.HttpMethod);
        Assert.Contains("X-Amz-Signature", session.Url, StringComparison.Ordinal);
        Assert.Equal("image/jpeg", session.Headers["Content-Type"]);
        Assert.True(session.ExpiresAtLocal > DateTime.Now.AddMinutes(-1));
    }

    /// <summary>
    /// 验证场景：对象读取预签名会话会返回 GET 地址且不附带额外请求头。
    /// </summary>
    [Fact]
    public async Task CreateReadSessionAsync_ShouldReturnGetSessionWithoutHeaders() {
        var service = CreateService();

        var session = await service.CreateReadSessionAsync(new CreateObjectStorageReadSessionRequest {
            BucketName = "sorting-hub-parcel-images",
            ObjectKey = "images/2026/06/15/parcel-001/top-cam.jpg",
            DownloadFileName = "top-cam.jpg"
        }, CancellationToken.None);

        Assert.Equal("GET", session.HttpMethod);
        Assert.Contains("X-Amz-Signature", session.Url, StringComparison.Ordinal);
        Assert.Empty(session.Headers);
    }

    /// <summary>
    /// 验证场景：Multipart 分片预签名地址会携带 uploadId 与 partNumber 查询参数。
    /// </summary>
    [Fact]
    public async Task CreateMultipartUploadPartSessionAsync_ShouldIncludeMultipartQueryParameters() {
        var service = CreateService();

        var session = await service.CreateMultipartUploadPartSessionAsync(new CreateObjectStorageMultipartUploadPartRequest {
            BucketName = "sorting-hub-files",
            ObjectKey = "files/2026/06/15/parcel-001/manual.pdf",
            UploadId = "upload-session-001",
            PartNumber = 3
        }, CancellationToken.None);

        Assert.Equal("PUT", session.HttpMethod);
        Assert.Contains("uploadId=upload-session-001", session.Url, StringComparison.Ordinal);
        Assert.Contains("partNumber=3", session.Url, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：DI 可解析对象存储抽象。
    /// </summary>
    [Fact]
    public void AddMinioObjectStorage_ShouldRegisterObjectStorageService() {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        services.AddMinioObjectStorage(configuration);

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        var objectStorageService = serviceProvider.GetRequiredService<IObjectStorageService>();

        Assert.IsType<MinioObjectStorageService>(objectStorageService);
    }

    /// <summary>
    /// 创建测试服务实例。
    /// </summary>
    /// <returns>对象存储服务。</returns>
    private static MinioObjectStorageService CreateService() {
        var configuration = CreateConfiguration();
        var options = MinioObjectStorageClientOptions.FromConfiguration(configuration);
        var minioClient = (MinioClient)new MinioClient()
            .WithEndpoint(options.Endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithSSL(options.UseSsl)
            .WithRegion(options.Region)
            .Build();
        var multipartInvoker = new MinioMultipartOperationInvoker(minioClient, options);
        return new MinioObjectStorageService(minioClient, options, multipartInvoker);
    }

    /// <summary>
    /// 创建测试配置。
    /// </summary>
    /// <returns>配置根。</returns>
    private static IConfiguration CreateConfiguration() {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["ObjectStorage:Minio:Endpoint"] = "minio.zeye.local:9000",
                ["ObjectStorage:Minio:UseSsl"] = "false",
                ["ObjectStorage:Minio:AccessKey"] = "${MINIO_ACCESS_KEY}",
                ["ObjectStorage:Minio:SecretKey"] = "${MINIO_SECRET_KEY}",
                ["ObjectStorage:Minio:Region"] = "us-east-1",
                ["ObjectStorage:Minio:PresignedUploadExpireSeconds"] = "900",
                ["ObjectStorage:Minio:PresignedReadExpireSeconds"] = "300",
                ["ObjectStorage:Minio:MultipartPartExpireSeconds"] = "900"
            })
            .Build();
    }
}
