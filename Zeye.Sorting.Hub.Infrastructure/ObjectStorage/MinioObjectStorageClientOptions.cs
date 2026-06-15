using Microsoft.Extensions.Configuration;

namespace Zeye.Sorting.Hub.Infrastructure.ObjectStorage;

/// <summary>
/// MinIO 对象存储运行期配置。
/// </summary>
internal sealed class MinioObjectStorageClientOptions {
    /// <summary>
    /// MinIO 配置节路径。
    /// </summary>
    internal const string SectionPath = "ObjectStorage:Minio";

    /// <summary>
    /// 默认 Region。
    /// </summary>
    internal const string DefaultRegion = "us-east-1";

    /// <summary>
    /// Multipart 默认分片大小（10 MB）。
    /// </summary>
    internal const int DefaultMultipartPartSizeBytes = 10 * 1024 * 1024;

    /// <summary>
    /// Multipart 最小分片大小（5 MB）。
    /// </summary>
    internal const int MinMultipartPartSizeBytes = 5 * 1024 * 1024;

    /// <summary>
    /// Multipart 允许的最大分片数量。
    /// </summary>
    internal const int MaxMultipartPartCount = 10_000;

    /// <summary>
    /// MinIO 访问端点。
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// 是否启用 HTTPS。
    /// </summary>
    public bool UseSsl { get; init; }

    /// <summary>
    /// 运行期 AccessKey。
    /// </summary>
    public required string AccessKey { get; init; }

    /// <summary>
    /// 运行期 SecretKey。
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>
    /// Region。
    /// </summary>
    public required string Region { get; init; }

    /// <summary>
    /// 单对象上传预签名有效期（秒）。
    /// </summary>
    public int PresignedUploadExpireSeconds { get; init; }

    /// <summary>
    /// 对象读取预签名有效期（秒）。
    /// </summary>
    public int PresignedReadExpireSeconds { get; init; }

    /// <summary>
    /// Multipart 分片预签名有效期（秒）。
    /// </summary>
    public int MultipartPartExpireSeconds { get; init; }

    /// <summary>
    /// 从配置中构建 MinIO 运行期配置。
    /// </summary>
    /// <param name="configuration">配置根。</param>
    /// <returns>运行期配置。</returns>
    internal static MinioObjectStorageClientOptions FromConfiguration(IConfiguration configuration) {
        var section = configuration.GetSection(SectionPath);
        var accessKey = ResolveRuntimeCredential(section["AccessKey"], "${MINIO_ACCESS_KEY}");
        var secretKey = ResolveRuntimeCredential(section["SecretKey"], "${MINIO_SECRET_KEY}");
        var region = NormalizeText(section["Region"], DefaultRegion);

        return new MinioObjectStorageClientOptions {
            Endpoint = NormalizeText(section["Endpoint"], "minio.internal.local:9000"),
            UseSsl = ParseBoolean(section["UseSsl"], fallback: false),
            AccessKey = accessKey,
            SecretKey = secretKey,
            Region = region,
            PresignedUploadExpireSeconds = ParseInt32(section["PresignedUploadExpireSeconds"], fallback: 900),
            PresignedReadExpireSeconds = ParseInt32(section["PresignedReadExpireSeconds"], fallback: 300),
            MultipartPartExpireSeconds = ParseInt32(section["MultipartPartExpireSeconds"], fallback: 900)
        };
    }

    /// <summary>
    /// 归一化文本配置。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <param name="fallback">回退值。</param>
    /// <returns>归一化后的文本。</returns>
    private static string NormalizeText(string? value, string fallback) {
        var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    /// <summary>
    /// 解析运行期凭据。
    /// </summary>
    /// <param name="configuredValue">配置值。</param>
    /// <param name="fallback">回退值。</param>
    /// <returns>环境变量解析后的凭据；未解析时返回原始配置值。</returns>
    private static string ResolveRuntimeCredential(string? configuredValue, string fallback) {
        var normalized = NormalizeText(configuredValue, fallback);
        if (!TryExtractPlaceholderName(normalized, out var placeholderName)) {
            return normalized;
        }

        var runtimeValue = Environment.GetEnvironmentVariable(placeholderName);
        return string.IsNullOrWhiteSpace(runtimeValue) ? normalized : runtimeValue.Trim();
    }

    /// <summary>
    /// 尝试提取占位符变量名。
    /// </summary>
    /// <param name="value">配置值。</param>
    /// <param name="placeholderName">占位符变量名。</param>
    /// <returns>成功时返回 <see langword="true"/>。</returns>
    private static bool TryExtractPlaceholderName(string value, out string placeholderName) {
        placeholderName = string.Empty;
        if (!value.StartsWith("${", StringComparison.Ordinal) || !value.EndsWith('}')) {
            return false;
        }

        if (value.Length <= 3) {
            return false;
        }

        placeholderName = value[2..^1].Trim();
        return !string.IsNullOrWhiteSpace(placeholderName);
    }

    /// <summary>
    /// 解析布尔配置。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <param name="fallback">回退值。</param>
    /// <returns>解析结果。</returns>
    private static bool ParseBoolean(string? value, bool fallback) {
        return bool.TryParse(value, out var parsedValue) ? parsedValue : fallback;
    }

    /// <summary>
    /// 解析整数配置。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <param name="fallback">回退值。</param>
    /// <returns>解析结果。</returns>
    private static int ParseInt32(string? value, int fallback) {
        return int.TryParse(value, out var parsedValue) ? parsedValue : fallback;
    }
}
