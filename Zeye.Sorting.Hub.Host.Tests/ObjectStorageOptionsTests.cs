using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Zeye.Sorting.Hub.Domain.Enums.ObjectStorage;
using Zeye.Sorting.Hub.Host.Options;

namespace Zeye.Sorting.Hub.Host.Tests;

/// <summary>
/// 对象存储配置回归测试。
/// </summary>
public sealed class ObjectStorageOptionsTests {
    /// <summary>
    /// 验证场景：默认配置满足 MinIO PR-B 规划值。
    /// </summary>
    [Fact]
    public void ObjectStorageOptions_ShouldExposeExpectedDefaults() {
        var options = new ObjectStorageOptions();

        Assert.Equal(ObjectStorageProvider.Minio, options.Provider);
        Assert.True(options.Minio.IsEnabled);
        Assert.Equal("minio.internal.local:9000", options.Minio.Endpoint);
        Assert.False(options.Minio.UseSsl);
        Assert.Equal("${MINIO_ACCESS_KEY}", options.Minio.AccessKey);
        Assert.Equal("${MINIO_SECRET_KEY}", options.Minio.SecretKey);
        Assert.Equal("sorting-hub-parcel-images", options.Minio.ParcelImagesBucket);
        Assert.Equal("sorting-hub-files", options.Minio.GenericFilesBucket);
        Assert.Equal(900, options.Minio.PresignedUploadExpireSeconds);
        Assert.Equal(300, options.Minio.PresignedReadExpireSeconds);
        Assert.Equal(900, options.Minio.MultipartPartExpireSeconds);
        Assert.False(options.Minio.Bootstrap.EnsureBucketsExist);
        Assert.True(options.Minio.Bootstrap.DryRun);
        Assert.True(options.Minio.Bootstrap.EnableGuard);
        Assert.False(options.Minio.Bootstrap.AllowDangerousActionExecution);
    }

    /// <summary>
    /// 验证场景：合法配置可通过启动期校验并成功绑定。
    /// </summary>
    [Fact]
    public void AddObjectStorageOptions_ShouldBindValidConfiguration() {
        var configuration = CreateConfiguration(new Dictionary<string, string?> {
            [$"{ObjectStorageOptions.SectionName}:Provider"] = "Minio",
            [$"{ObjectStorageOptions.SectionName}:Minio:Endpoint"] = "minio.zeye.local:9000",
            [$"{ObjectStorageOptions.SectionName}:Minio:UseSsl"] = "true",
            [$"{ObjectStorageOptions.SectionName}:Minio:AccessKey"] = "${MINIO_ACCESS_KEY}",
            [$"{ObjectStorageOptions.SectionName}:Minio:SecretKey"] = "${MINIO_SECRET_KEY}",
            [$"{ObjectStorageOptions.SectionName}:Minio:ParcelImagesBucket"] = "sorting-hub-images",
            [$"{ObjectStorageOptions.SectionName}:Minio:GenericFilesBucket"] = "sorting-hub-files",
            [$"{ObjectStorageOptions.SectionName}:Minio:PresignedUploadExpireSeconds"] = "1200",
            [$"{ObjectStorageOptions.SectionName}:Minio:PresignedReadExpireSeconds"] = "600",
            [$"{ObjectStorageOptions.SectionName}:Minio:MultipartPartExpireSeconds"] = "1800",
            [$"{ObjectStorageOptions.SectionName}:Minio:Bootstrap:EnsureBucketsExist"] = "false",
            [$"{ObjectStorageOptions.SectionName}:Minio:Bootstrap:DryRun"] = "true",
            [$"{ObjectStorageOptions.SectionName}:Minio:Bootstrap:EnableGuard"] = "true",
            [$"{ObjectStorageOptions.SectionName}:Minio:Bootstrap:AllowDangerousActionExecution"] = "false"
        });

        var options = BuildValidatedOptions(configuration);

        Assert.Equal(ObjectStorageProvider.Minio, options.Provider);
        Assert.Equal("minio.zeye.local:9000", options.Minio.Endpoint);
        Assert.True(options.Minio.UseSsl);
        Assert.Equal(1200, options.Minio.PresignedUploadExpireSeconds);
        Assert.Equal(600, options.Minio.PresignedReadExpireSeconds);
        Assert.Equal(1800, options.Minio.MultipartPartExpireSeconds);
    }

    /// <summary>
    /// 验证场景：真实凭据文本会在启动期校验阶段被拒绝。
    /// </summary>
    [Fact]
    public void AddObjectStorageOptions_ShouldRejectRealCredentialText() {
        var configuration = CreateConfiguration(new Dictionary<string, string?> {
            [$"{ObjectStorageOptions.SectionName}:Provider"] = "Minio",
            [$"{ObjectStorageOptions.SectionName}:Minio:AccessKey"] = "minio-access-key",
            [$"{ObjectStorageOptions.SectionName}:Minio:SecretKey"] = "${MINIO_SECRET_KEY}"
        });

        var exception = Assert.Throws<OptionsValidationException>(() => BuildValidatedOptions(configuration));

        Assert.Contains("AccessKey", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证场景：危险启动期自检配置会在启动期校验阶段被拒绝。
    /// </summary>
    [Fact]
    public void AddObjectStorageOptions_ShouldRejectUnsafeBootstrapConfiguration() {
        var configuration = CreateConfiguration(new Dictionary<string, string?> {
            [$"{ObjectStorageOptions.SectionName}:Provider"] = "Minio",
            [$"{ObjectStorageOptions.SectionName}:Minio:AccessKey"] = "${MINIO_ACCESS_KEY}",
            [$"{ObjectStorageOptions.SectionName}:Minio:SecretKey"] = "${MINIO_SECRET_KEY}",
            [$"{ObjectStorageOptions.SectionName}:Minio:Bootstrap:EnsureBucketsExist"] = "true",
            [$"{ObjectStorageOptions.SectionName}:Minio:Bootstrap:DryRun"] = "false",
            [$"{ObjectStorageOptions.SectionName}:Minio:Bootstrap:EnableGuard"] = "true",
            [$"{ObjectStorageOptions.SectionName}:Minio:Bootstrap:AllowDangerousActionExecution"] = "false"
        });

        var exception = Assert.Throws<OptionsValidationException>(() => BuildValidatedOptions(configuration));

        Assert.Contains("Bootstrap", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// 创建测试配置。
    /// </summary>
    /// <param name="values">配置键值对。</param>
    /// <returns>配置根。</returns>
    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    /// <summary>
    /// 构建并读取通过 ValidateOnStart 注册的对象存储配置。
    /// </summary>
    /// <param name="configuration">配置根。</param>
    /// <returns>校验后的配置值。</returns>
    private static ObjectStorageOptions BuildValidatedOptions(IConfiguration configuration) {
        var services = new ServiceCollection();
        services.AddObjectStorageOptions(configuration);

        using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions {
            ValidateScopes = true
        });
        return serviceProvider.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
    }
}
