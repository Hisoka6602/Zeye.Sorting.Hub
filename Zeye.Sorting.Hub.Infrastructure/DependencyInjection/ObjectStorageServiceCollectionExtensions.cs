using Minio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;
using Zeye.Sorting.Hub.Infrastructure.ObjectStorage;

namespace Zeye.Sorting.Hub.Infrastructure.DependencyInjection;

/// <summary>
/// 对象存储服务注册扩展。
/// </summary>
public static class ObjectStorageServiceCollectionExtensions {
    /// <summary>
    /// 注册 MinIO 对象存储实现。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configuration">配置根。</param>
    /// <returns>服务集合。</returns>
    public static IServiceCollection AddMinioObjectStorage(this IServiceCollection services, IConfiguration configuration) {
        var options = MinioObjectStorageClientOptions.FromConfiguration(configuration);

        services.AddSingleton(options);
        services.AddSingleton(static serviceProvider => {
            var runtimeOptions = serviceProvider.GetRequiredService<MinioObjectStorageClientOptions>();
            return (MinioClient)new MinioClient()
                .WithEndpoint(runtimeOptions.Endpoint)
                .WithCredentials(runtimeOptions.AccessKey, runtimeOptions.SecretKey)
                .WithSSL(runtimeOptions.UseSsl)
                .WithRegion(runtimeOptions.Region)
                .Build();
        });
        services.AddSingleton(static serviceProvider => new MinioMultipartOperationInvoker(
            serviceProvider.GetRequiredService<MinioClient>(),
            serviceProvider.GetRequiredService<MinioObjectStorageClientOptions>()));
        services.AddSingleton<IObjectStorageService>(static serviceProvider => new MinioObjectStorageService(
            serviceProvider.GetRequiredService<MinioClient>(),
            serviceProvider.GetRequiredService<MinioObjectStorageClientOptions>(),
            serviceProvider.GetRequiredService<MinioMultipartOperationInvoker>()));
        return services;
    }
}
