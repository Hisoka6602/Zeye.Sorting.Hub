using System.Collections.ObjectModel;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using NLog;
using Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

namespace Zeye.Sorting.Hub.Infrastructure.ObjectStorage;

/// <summary>
/// 基于 MinIO 的对象存储服务实现。
/// </summary>
internal sealed class MinioObjectStorageService : IObjectStorageService {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly Logger NLogLogger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 空请求头集合。
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new ReadOnlyDictionary<string, string>(
        new Dictionary<string, string>(capacity: 0, comparer: StringComparer.Ordinal));

    /// <summary>
    /// MinIO 客户端。
    /// </summary>
    private readonly MinioClient minioClient;

    /// <summary>
    /// MinIO 运行期配置。
    /// </summary>
    private readonly MinioObjectStorageClientOptions options;

    /// <summary>
    /// Multipart 调用器。
    /// </summary>
    private readonly MinioMultipartOperationInvoker multipartOperationInvoker;

    /// <summary>
    /// 初始化对象存储服务。
    /// </summary>
    /// <param name="minioClient">MinIO 客户端。</param>
    /// <param name="options">运行期配置。</param>
    /// <param name="multipartOperationInvoker">Multipart 调用器。</param>
    internal MinioObjectStorageService(
        MinioClient minioClient,
        MinioObjectStorageClientOptions options,
        MinioMultipartOperationInvoker multipartOperationInvoker) {
        this.minioClient = minioClient;
        this.options = options;
        this.multipartOperationInvoker = multipartOperationInvoker;
    }

    /// <inheritdoc />
    public async Task<ObjectStoragePresignedSession> CreateUploadSessionAsync(
        CreateObjectStorageUploadSessionRequest request,
        CancellationToken cancellationToken) {
        var bucketName = NormalizeRequiredText(request.BucketName, nameof(request.BucketName), "Bucket 名称");
        var objectKey = NormalizeRequiredText(request.ObjectKey, nameof(request.ObjectKey), "对象键");
        ValidateOptionalObjectSize(request.ObjectSizeBytes, nameof(request.ObjectSizeBytes));
        var headers = CreateHeaders(request.ContentType);

        try {
            var url = await minioClient.PresignedPutObjectAsync(new PresignedPutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithHeaders(headers)
                .WithExpiry(options.PresignedUploadExpireSeconds)).ConfigureAwait(false);

            return CreatePresignedSession(
                bucketName,
                objectKey,
                url,
                httpMethod: "PUT",
                expiresAfterSeconds: options.PresignedUploadExpireSeconds,
                AsReadOnlyHeaders(headers));
        }
        catch (Exception exception) {
            NLogLogger.Error(exception, "创建 MinIO 单对象上传预签名会话失败，Bucket: {BucketName}, ObjectKey: {ObjectKey}", bucketName, objectKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ObjectStoragePresignedSession> CreateReadSessionAsync(
        CreateObjectStorageReadSessionRequest request,
        CancellationToken cancellationToken) {
        var bucketName = NormalizeRequiredText(request.BucketName, nameof(request.BucketName), "Bucket 名称");
        var objectKey = NormalizeRequiredText(request.ObjectKey, nameof(request.ObjectKey), "对象键");

        try {
            var url = await minioClient.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithExpiry(options.PresignedReadExpireSeconds)).ConfigureAwait(false);

            return CreatePresignedSession(
                bucketName,
                objectKey,
                url,
                httpMethod: "GET",
                expiresAfterSeconds: options.PresignedReadExpireSeconds,
                headers: EmptyHeaders);
        }
        catch (Exception exception) {
            NLogLogger.Error(exception, "创建 MinIO 对象读取预签名会话失败，Bucket: {BucketName}, ObjectKey: {ObjectKey}", bucketName, objectKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ObjectStorageMultipartUploadSession> CreateMultipartUploadSessionAsync(
        CreateObjectStorageMultipartUploadSessionRequest request,
        CancellationToken cancellationToken) {
        var bucketName = NormalizeRequiredText(request.BucketName, nameof(request.BucketName), "Bucket 名称");
        var objectKey = NormalizeRequiredText(request.ObjectKey, nameof(request.ObjectKey), "对象键");
        ValidateOptionalObjectSize(request.ObjectSizeBytes, nameof(request.ObjectSizeBytes));
        var headers = CreateHeaders(request.ContentType);
        var partSizeBytes = ResolveMultipartPartSize(request.ObjectSizeBytes, request.PartSizeBytes);

        try {
            var uploadId = await multipartOperationInvoker.CreateMultipartUploadAsync(
                bucketName,
                objectKey,
                request.ContentType,
                headers,
                cancellationToken).ConfigureAwait(false);

            return new ObjectStorageMultipartUploadSession {
                BucketName = bucketName,
                ObjectKey = objectKey,
                UploadId = uploadId,
                PartSizeBytes = partSizeBytes,
                EstimatedPartCount = EstimatePartCount(request.ObjectSizeBytes, partSizeBytes),
                ExpiresAtLocal = DateTime.Now.AddSeconds(options.MultipartPartExpireSeconds)
            };
        }
        catch (Exception exception) {
            NLogLogger.Error(exception, "创建 MinIO Multipart 上传会话失败，Bucket: {BucketName}, ObjectKey: {ObjectKey}", bucketName, objectKey);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ObjectStoragePresignedSession> CreateMultipartUploadPartSessionAsync(
        CreateObjectStorageMultipartUploadPartRequest request,
        CancellationToken cancellationToken) {
        var bucketName = NormalizeRequiredText(request.BucketName, nameof(request.BucketName), "Bucket 名称");
        var objectKey = NormalizeRequiredText(request.ObjectKey, nameof(request.ObjectKey), "对象键");
        var uploadId = NormalizeRequiredText(request.UploadId, nameof(request.UploadId), "UploadId");
        ValidatePartNumber(request.PartNumber);

        try {
            var url = await multipartOperationInvoker.CreateMultipartUploadPartPresignedUrlAsync(
                bucketName,
                objectKey,
                uploadId,
                request.PartNumber,
                cancellationToken).ConfigureAwait(false);

            return CreatePresignedSession(
                bucketName,
                objectKey,
                url,
                httpMethod: "PUT",
                expiresAfterSeconds: options.MultipartPartExpireSeconds,
                headers: EmptyHeaders);
        }
        catch (Exception exception) {
            NLogLogger.Error(
                exception,
                "创建 MinIO Multipart 分片预签名会话失败，Bucket: {BucketName}, ObjectKey: {ObjectKey}, UploadId: {UploadId}, PartNumber: {PartNumber}",
                bucketName,
                objectKey,
                uploadId,
                request.PartNumber);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task CompleteMultipartUploadAsync(
        CompleteObjectStorageMultipartUploadRequest request,
        CancellationToken cancellationToken) {
        var bucketName = NormalizeRequiredText(request.BucketName, nameof(request.BucketName), "Bucket 名称");
        var objectKey = NormalizeRequiredText(request.ObjectKey, nameof(request.ObjectKey), "对象键");
        var uploadId = NormalizeRequiredText(request.UploadId, nameof(request.UploadId), "UploadId");
        var partEtags = BuildPartEtags(request.Parts);

        try {
            await multipartOperationInvoker.CompleteMultipartUploadAsync(
                bucketName,
                objectKey,
                uploadId,
                partEtags,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) {
            NLogLogger.Error(exception, "完成 MinIO Multipart 上传失败，Bucket: {BucketName}, ObjectKey: {ObjectKey}, UploadId: {UploadId}", bucketName, objectKey, uploadId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AbortMultipartUploadAsync(
        AbortObjectStorageMultipartUploadRequest request,
        CancellationToken cancellationToken) {
        var bucketName = NormalizeRequiredText(request.BucketName, nameof(request.BucketName), "Bucket 名称");
        var objectKey = NormalizeRequiredText(request.ObjectKey, nameof(request.ObjectKey), "对象键");
        var uploadId = NormalizeRequiredText(request.UploadId, nameof(request.UploadId), "UploadId");

        try {
            await multipartOperationInvoker.AbortMultipartUploadAsync(
                bucketName,
                objectKey,
                uploadId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) {
            NLogLogger.Error(exception, "中止 MinIO Multipart 上传失败，Bucket: {BucketName}, ObjectKey: {ObjectKey}, UploadId: {UploadId}", bucketName, objectKey, uploadId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        ObjectStorageObjectDescriptor descriptor,
        CancellationToken cancellationToken) {
        var bucketName = NormalizeRequiredText(descriptor.BucketName, nameof(descriptor.BucketName), "Bucket 名称");
        var objectKey = NormalizeRequiredText(descriptor.ObjectKey, nameof(descriptor.ObjectKey), "对象键");

        try {
            _ = await minioClient.StatObjectAsync(new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey), cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (ObjectNotFoundException exception) {
            NLogLogger.Warn(exception, "MinIO 对象不存在，Bucket: {BucketName}, ObjectKey: {ObjectKey}", bucketName, objectKey);
            return false;
        }
        catch (BucketNotFoundException exception) {
            NLogLogger.Warn(exception, "MinIO Bucket 不存在，Bucket: {BucketName}, ObjectKey: {ObjectKey}", bucketName, objectKey);
            return false;
        }
        catch (Exception exception) {
            NLogLogger.Error(exception, "探测 MinIO 对象存在性失败，Bucket: {BucketName}, ObjectKey: {ObjectKey}", bucketName, objectKey);
            throw;
        }
    }

    /// <summary>
    /// 构建预签名会话返回模型。
    /// </summary>
    /// <param name="bucketName">Bucket 名称。</param>
    /// <param name="objectKey">对象键。</param>
    /// <param name="url">预签名地址。</param>
    /// <param name="httpMethod">HTTP 方法。</param>
    /// <param name="expiresAfterSeconds">有效期秒数。</param>
    /// <param name="headers">请求头。</param>
    /// <returns>预签名会话。</returns>
    private static ObjectStoragePresignedSession CreatePresignedSession(
        string bucketName,
        string objectKey,
        string url,
        string httpMethod,
        int expiresAfterSeconds,
        IReadOnlyDictionary<string, string> headers) {
        return new ObjectStoragePresignedSession {
            BucketName = bucketName,
            ObjectKey = objectKey,
            Url = url,
            HttpMethod = httpMethod,
            ExpiresAtLocal = DateTime.Now.AddSeconds(expiresAfterSeconds),
            Headers = headers
        };
    }

    /// <summary>
    /// 将请求头转换为只读集合。
    /// </summary>
    /// <param name="headers">请求头。</param>
    /// <returns>只读请求头集合。</returns>
    private static IReadOnlyDictionary<string, string> AsReadOnlyHeaders(IDictionary<string, string> headers) {
        if (headers.Count == 0) {
            return EmptyHeaders;
        }

        return new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(headers, StringComparer.Ordinal));
    }

    /// <summary>
    /// 创建请求头集合。
    /// </summary>
    /// <param name="contentType">内容类型。</param>
    /// <returns>请求头集合。</returns>
    private static IDictionary<string, string> CreateHeaders(string? contentType) {
        if (string.IsNullOrWhiteSpace(contentType)) {
            return new Dictionary<string, string>(capacity: 0, comparer: StringComparer.Ordinal);
        }

        return new Dictionary<string, string>(StringComparer.Ordinal) {
            ["Content-Type"] = contentType.Trim()
        };
    }

    /// <summary>
    /// 归一化必填文本。
    /// </summary>
    /// <param name="value">原始值。</param>
    /// <param name="argumentName">参数名。</param>
    /// <param name="displayName">显示名。</param>
    /// <returns>归一化后的文本。</returns>
    private static string NormalizeRequiredText(string? value, string argumentName, string displayName) {
        if (string.IsNullOrWhiteSpace(value)) {
            NLogLogger.Error("{DisplayName}不能为空。", displayName);
            throw new ArgumentException($"{displayName}不能为空。", argumentName);
        }

        return value.Trim();
    }

    /// <summary>
    /// 校验可选对象大小。
    /// </summary>
    /// <param name="objectSizeBytes">对象大小。</param>
    /// <param name="argumentName">参数名。</param>
    private static void ValidateOptionalObjectSize(long? objectSizeBytes, string argumentName) {
        if (objectSizeBytes.HasValue && objectSizeBytes.Value < 0) {
            NLogLogger.Error("对象大小不能为负数。");
            throw new ArgumentOutOfRangeException(argumentName, "对象大小不能为负数。");
        }
    }

    /// <summary>
    /// 校验分片序号。
    /// </summary>
    /// <param name="partNumber">分片序号。</param>
    private static void ValidatePartNumber(int partNumber) {
        if (partNumber <= 0) {
            NLogLogger.Error("分片序号必须大于 0。");
            throw new ArgumentOutOfRangeException(nameof(partNumber), "分片序号必须大于 0。");
        }
    }

    /// <summary>
    /// 解析 Multipart 分片大小。
    /// </summary>
    /// <param name="objectSizeBytes">对象总大小。</param>
    /// <param name="requestedPartSizeBytes">请求分片大小。</param>
    /// <returns>实际分片大小。</returns>
    private static int ResolveMultipartPartSize(long? objectSizeBytes, int? requestedPartSizeBytes) {
        var partSizeBytes = requestedPartSizeBytes ?? MinioObjectStorageClientOptions.DefaultMultipartPartSizeBytes;
        if (partSizeBytes <= 0) {
            NLogLogger.Error("分片大小必须大于 0。");
            throw new ArgumentOutOfRangeException(nameof(requestedPartSizeBytes), "分片大小必须大于 0。");
        }

        partSizeBytes = Math.Max(partSizeBytes, MinioObjectStorageClientOptions.MinMultipartPartSizeBytes);
        if (!objectSizeBytes.HasValue || objectSizeBytes.Value == 0) {
            return partSizeBytes;
        }

        var requiredPartSize = (long)Math.Ceiling(objectSizeBytes.Value / (double)MinioObjectStorageClientOptions.MaxMultipartPartCount);
        requiredPartSize = Math.Max(requiredPartSize, MinioObjectStorageClientOptions.MinMultipartPartSizeBytes);
        if (requiredPartSize > int.MaxValue) {
            NLogLogger.Error("对象过大，无法为 Multipart 计算合法分片大小，ObjectSizeBytes: {ObjectSizeBytes}", objectSizeBytes.Value);
            throw new InvalidOperationException("对象过大，无法计算合法的 Multipart 分片大小。");
        }

        return Math.Max(partSizeBytes, (int)requiredPartSize);
    }

    /// <summary>
    /// 估算分片数量。
    /// </summary>
    /// <param name="objectSizeBytes">对象总大小。</param>
    /// <param name="partSizeBytes">分片大小。</param>
    /// <returns>估算分片数量。</returns>
    private static int? EstimatePartCount(long? objectSizeBytes, int partSizeBytes) {
        if (!objectSizeBytes.HasValue || objectSizeBytes.Value <= 0) {
            return null;
        }

        return (int)Math.Ceiling(objectSizeBytes.Value / (double)partSizeBytes);
    }

    /// <summary>
    /// 构建完成上传所需的分片 ETag 映射。
    /// </summary>
    /// <param name="partEtags">分片集合。</param>
    /// <returns>分片 ETag 映射。</returns>
    private static IReadOnlyDictionary<int, string> BuildPartEtags(IReadOnlyCollection<ObjectStorageMultipartPartETag> partEtags) {
        if (partEtags.Count == 0) {
            NLogLogger.Error("完成 Multipart 上传时至少需要一个分片 ETag。");
            throw new ArgumentException("完成 Multipart 上传时至少需要一个分片 ETag。", nameof(partEtags));
        }

        var dictionary = new SortedDictionary<int, string>();
        foreach (var partEtag in partEtags) {
            ValidatePartNumber(partEtag.PartNumber);
            var eTag = NormalizeRequiredText(partEtag.ETag, nameof(partEtag.ETag), "分片 ETag");
            if (!dictionary.TryAdd(partEtag.PartNumber, eTag)) {
                NLogLogger.Error("检测到重复的 Multipart 分片序号，PartNumber: {PartNumber}", partEtag.PartNumber);
                throw new ArgumentException($"检测到重复的 Multipart 分片序号：{partEtag.PartNumber}。", nameof(partEtags));
            }
        }

        return new ReadOnlyDictionary<int, string>(dictionary);
    }
}
