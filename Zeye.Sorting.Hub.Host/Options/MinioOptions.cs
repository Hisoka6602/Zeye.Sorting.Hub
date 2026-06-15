namespace Zeye.Sorting.Hub.Host.Options;

/// <summary>
/// MinIO 配置。
/// </summary>
public sealed class MinioOptions {
    /// <summary>
    /// 预签名有效期最小秒数。
    /// </summary>
    public const int MinExpireSeconds = 60;

    /// <summary>
    /// 预签名有效期最大秒数。
    /// </summary>
    public const int MaxExpireSeconds = 604800;

    /// <summary>
    /// Endpoint 最大长度。
    /// </summary>
    public const int MaxEndpointLength = 256;

    /// <summary>
    /// 是否启用 MinIO。
    /// 可填写范围：true / false。
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// MinIO 访问端点。
    /// 可填写范围：主机名或主机名:端口，禁止包含协议前缀。
    /// </summary>
    public string Endpoint { get; set; } = "minio.internal.local:9000";

    /// <summary>
    /// 是否启用 HTTPS。
    /// 可填写范围：true / false。
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// AccessKey 占位符。
    /// 可填写范围：仅允许 ${变量名} 形式占位符。
    /// </summary>
    public string AccessKey { get; set; } = "${MINIO_ACCESS_KEY}";

    /// <summary>
    /// SecretKey 占位符。
    /// 可填写范围：仅允许 ${变量名} 形式占位符。
    /// </summary>
    public string SecretKey { get; set; } = "${MINIO_SECRET_KEY}";

    /// <summary>
    /// Parcel 图片 Bucket 名称。
    /// 可填写范围：3~63 位小写字母、数字、点或中划线。
    /// </summary>
    public string ParcelImagesBucket { get; set; } = "sorting-hub-parcel-images";

    /// <summary>
    /// 通用文件 Bucket 名称。
    /// 可填写范围：3~63 位小写字母、数字、点或中划线。
    /// </summary>
    public string GenericFilesBucket { get; set; } = "sorting-hub-files";

    /// <summary>
    /// 单对象上传预签名有效期（秒）。
    /// 可填写范围：60~604800。
    /// </summary>
    public int PresignedUploadExpireSeconds { get; set; } = 900;

    /// <summary>
    /// 对象读取预签名有效期（秒）。
    /// 可填写范围：60~604800。
    /// </summary>
    public int PresignedReadExpireSeconds { get; set; } = 300;

    /// <summary>
    /// Multipart 分片预签名有效期（秒）。
    /// 可填写范围：60~604800。
    /// </summary>
    public int MultipartPartExpireSeconds { get; set; } = 900;

    /// <summary>
    /// 启动期自检配置。
    /// </summary>
    public MinioBootstrapOptions Bootstrap { get; set; } = new();

    /// <summary>
    /// 校验 Endpoint 是否合法。
    /// </summary>
    /// <param name="endpoint">待校验 Endpoint。</param>
    /// <returns>合法时返回 <see langword="true"/>。</returns>
    public static bool IsValidEndpoint(string? endpoint) {
        if (string.IsNullOrWhiteSpace(endpoint)) {
            return false;
        }

        var value = endpoint.Trim();
        return value.Length <= MaxEndpointLength
            && !value.Contains("://", StringComparison.Ordinal);
    }

    /// <summary>
    /// 校验证书字段是否为占位符。
    /// </summary>
    /// <param name="credential">待校验凭据。</param>
    /// <returns>占位符合法时返回 <see langword="true"/>。</returns>
    public static bool IsPlaceholderCredential(string? credential) {
        if (string.IsNullOrWhiteSpace(credential)) {
            return false;
        }

        var value = credential.Trim();
        if (!value.StartsWith("${", StringComparison.Ordinal) || !value.EndsWith('}')) {
            return false;
        }

        if (value.Length <= 3) {
            return false;
        }

        for (var index = 2; index < value.Length - 1; index++) {
            if (!IsPlaceholderCharacter(value[index])) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 校验 Bucket 名称是否合法。
    /// </summary>
    /// <param name="bucketName">待校验 Bucket 名称。</param>
    /// <returns>合法时返回 <see langword="true"/>。</returns>
    public static bool IsValidBucketName(string? bucketName) {
        if (string.IsNullOrWhiteSpace(bucketName)) {
            return false;
        }

        var value = bucketName.Trim();
        if (value.Length is < 3 or > 63) {
            return false;
        }

        if (value.Contains("..", StringComparison.Ordinal)
            || value.Contains(".-", StringComparison.Ordinal)
            || value.Contains("-.", StringComparison.Ordinal)) {
            return false;
        }

        for (var index = 0; index < value.Length; index++) {
            if (!IsValidBucketCharacter(value[index])) {
                return false;
            }
        }

        var startsWithSupportedCharacter = IsValidBucketBoundaryCharacter(value[0]);
        var endsWithSupportedCharacter = IsValidBucketBoundaryCharacter(value[^1]);
        return startsWithSupportedCharacter && endsWithSupportedCharacter;
    }

    /// <summary>
    /// 校验预签名有效期是否处于允许范围。
    /// </summary>
    /// <param name="seconds">待校验秒数。</param>
    /// <returns>处于允许范围时返回 <see langword="true"/>。</returns>
    public static bool IsValidExpireSeconds(int seconds) {
        return seconds is >= MinExpireSeconds and <= MaxExpireSeconds;
    }

    /// <summary>
    /// 校验占位符变量字符是否合法。
    /// </summary>
    /// <param name="character">待校验字符。</param>
    /// <returns>合法时返回 <see langword="true"/>。</returns>
    private static bool IsPlaceholderCharacter(char character) {
        return char.IsAsciiLetterOrDigit(character) || character == '_';
    }

    /// <summary>
    /// 校验 Bucket 名称字符是否合法。
    /// </summary>
    /// <param name="character">待校验字符。</param>
    /// <returns>合法时返回 <see langword="true"/>。</returns>
    private static bool IsValidBucketCharacter(char character) {
        return character is >= 'a' and <= 'z'
            || character is >= '0' and <= '9'
            || character is '.'
            || character is '-';
    }

    /// <summary>
    /// 校验 Bucket 首尾边界字符是否合法。
    /// </summary>
    /// <param name="character">待校验字符。</param>
    /// <returns>合法时返回 <see langword="true"/>。</returns>
    private static bool IsValidBucketBoundaryCharacter(char character) {
        return character is >= 'a' and <= 'z'
            || character is >= '0' and <= '9';
    }
}
