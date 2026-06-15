namespace Zeye.Sorting.Hub.Application.Abstractions.ObjectStorage;

/// <summary>
/// 对象存储应用协作抽象。
/// </summary>
public interface IObjectStorageService {
    /// <summary>
    /// 创建单对象上传预签名会话。
    /// </summary>
    /// <param name="request">上传请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>预签名会话。</returns>
    Task<ObjectStoragePresignedSession> CreateUploadSessionAsync(
        CreateObjectStorageUploadSessionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 创建对象读取预签名会话。
    /// </summary>
    /// <param name="request">读取请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>预签名会话。</returns>
    Task<ObjectStoragePresignedSession> CreateReadSessionAsync(
        CreateObjectStorageReadSessionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 创建 Multipart 上传会话。
    /// </summary>
    /// <param name="request">Multipart 上传请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Multipart 上传会话。</returns>
    Task<ObjectStorageMultipartUploadSession> CreateMultipartUploadSessionAsync(
        CreateObjectStorageMultipartUploadSessionRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 创建 Multipart 分片上传预签名会话。
    /// </summary>
    /// <param name="request">分片上传请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>预签名会话。</returns>
    Task<ObjectStoragePresignedSession> CreateMultipartUploadPartSessionAsync(
        CreateObjectStorageMultipartUploadPartRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 完成 Multipart 上传。
    /// </summary>
    /// <param name="request">完成请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task CompleteMultipartUploadAsync(
        CompleteObjectStorageMultipartUploadRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 中止 Multipart 上传。
    /// </summary>
    /// <param name="request">中止请求。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task AbortMultipartUploadAsync(
        AbortObjectStorageMultipartUploadRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 探测对象是否存在。
    /// </summary>
    /// <param name="descriptor">对象描述。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>存在时返回 <see langword="true"/>。</returns>
    Task<bool> ExistsAsync(
        ObjectStorageObjectDescriptor descriptor,
        CancellationToken cancellationToken);
}
