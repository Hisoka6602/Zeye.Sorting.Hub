using Zeye.Sorting.Hub.Domain.Enums.ObjectStorage;

namespace Zeye.Sorting.Hub.Host.Options;

/// <summary>
/// 对象存储配置注册扩展。
/// </summary>
public static class ObjectStorageOptionsServiceCollectionExtensions {
    /// <summary>
    /// 注册对象存储配置并在启动阶段执行校验。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置根。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddObjectStorageOptions(
        this IServiceCollection services,
        IConfiguration configuration) {
        services.AddOptions<ObjectStorageOptions>()
            .Bind(configuration.GetSection(ObjectStorageOptions.SectionName))
            .Validate(
                static options => options.Provider == ObjectStorageProvider.Minio,
                "ObjectStorage:Provider 当前仅允许填写 Minio")
            .Validate(
                static options => MinioOptions.IsValidEndpoint(options.Minio.Endpoint),
                "ObjectStorage:Minio:Endpoint 不能为空，且不得包含 http:// 或 https:// 前缀")
            .Validate(
                static options => MinioOptions.IsPlaceholderCredential(options.Minio.AccessKey),
                "ObjectStorage:Minio:AccessKey 只能填写 ${变量名} 形式占位符")
            .Validate(
                static options => MinioOptions.IsPlaceholderCredential(options.Minio.SecretKey),
                "ObjectStorage:Minio:SecretKey 只能填写 ${变量名} 形式占位符")
            .Validate(
                static options => MinioOptions.IsValidBucketName(options.Minio.ParcelImagesBucket),
                "ObjectStorage:Minio:ParcelImagesBucket 必须为 3~63 位小写 Bucket 名称")
            .Validate(
                static options => MinioOptions.IsValidBucketName(options.Minio.GenericFilesBucket),
                "ObjectStorage:Minio:GenericFilesBucket 必须为 3~63 位小写 Bucket 名称")
            .Validate(
                static options => MinioOptions.IsValidExpireSeconds(options.Minio.PresignedUploadExpireSeconds),
                $"ObjectStorage:Minio:PresignedUploadExpireSeconds 必须在 {MinioOptions.MinExpireSeconds}~{MinioOptions.MaxExpireSeconds} 之间")
            .Validate(
                static options => MinioOptions.IsValidExpireSeconds(options.Minio.PresignedReadExpireSeconds),
                $"ObjectStorage:Minio:PresignedReadExpireSeconds 必须在 {MinioOptions.MinExpireSeconds}~{MinioOptions.MaxExpireSeconds} 之间")
            .Validate(
                static options => MinioOptions.IsValidExpireSeconds(options.Minio.MultipartPartExpireSeconds),
                $"ObjectStorage:Minio:MultipartPartExpireSeconds 必须在 {MinioOptions.MinExpireSeconds}~{MinioOptions.MaxExpireSeconds} 之间")
            .Validate(
                static options => MinioBootstrapOptions.IsSafeConfiguration(options.Minio.Bootstrap),
                "ObjectStorage:Minio:Bootstrap 必须保持守卫开启、DryRun=true，且禁止真实危险动作")
            .ValidateOnStart();

        return services;
    }
}
