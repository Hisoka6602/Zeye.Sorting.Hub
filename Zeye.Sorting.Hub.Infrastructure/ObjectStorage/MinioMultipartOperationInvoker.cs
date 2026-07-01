using System.Globalization;
using System.Reflection;
using System.Net.Http;
using Minio;
using Minio.DataModel.Args;

namespace Zeye.Sorting.Hub.Infrastructure.ObjectStorage;

/// <summary>
/// MinIO Multipart 相关调用器。
/// </summary>
internal sealed class MinioMultipartOperationInvoker {
    /// <summary>
    /// 创建 Multipart 上传的反射方法。
    /// </summary>
    private static readonly MethodInfo NewMultipartUploadMethod = ResolveRequiredInstanceMethod(
        typeof(MinioClient),
        "NewMultipartUploadAsync",
        "Minio.DataModel.Args.NewMultipartUploadPutArgs",
        typeof(CancellationToken));

    /// <summary>
    /// 完成 Multipart 上传的反射方法。
    /// </summary>
    private static readonly MethodInfo CompleteMultipartUploadMethod = ResolveRequiredInstanceMethod(
        typeof(MinioClient),
        "CompleteMultipartUploadAsync",
        "Minio.DataModel.Args.CompleteMultipartUploadArgs",
        typeof(CancellationToken));

    /// <summary>
    /// 中止 Multipart 上传的反射方法。
    /// </summary>
    private static readonly MethodInfo RemoveUploadMethod = ResolveRequiredInstanceMethod(
        typeof(MinioClient),
        "RemoveUploadAsync",
        typeof(RemoveUploadArgs),
        typeof(CancellationToken));

    /// <summary>
    /// 创建对象请求构建器的反射方法。
    /// </summary>
    private static readonly MethodInfo CreateObjectRequestMethod = ResolveRequiredMethod(
        typeof(RequestExtensions),
        "CreateRequest",
        BindingFlags.Static | BindingFlags.NonPublic,
        typeof(IMinioClient),
        typeof(HttpMethod),
        typeof(string),
        typeof(string),
        typeof(IDictionary<string, string>),
        typeof(string),
        typeof(ReadOnlyMemory<byte>),
        typeof(string),
        typeof(bool));

    /// <summary>
    /// Multipart 新建参数类型。
    /// </summary>
    private static readonly Type NewMultipartUploadPutArgsType = ResolveRequiredType("Minio.DataModel.Args.NewMultipartUploadPutArgs");

    /// <summary>
    /// Multipart 完成参数类型。
    /// </summary>
    private static readonly Type CompleteMultipartUploadArgsType = ResolveRequiredType("Minio.DataModel.Args.CompleteMultipartUploadArgs");

    /// <summary>
    /// V4 签名器类型。
    /// </summary>
    private static readonly Type V4AuthenticatorType = ResolveRequiredType("Minio.V4Authenticator");

    /// <summary>
    /// MinIO 请求构建器类型。
    /// </summary>
    private static readonly Type HttpRequestMessageBuilderType = ResolveRequiredType("Minio.HttpRequestMessageBuilder");

    /// <summary>
    /// V4 签名器构造方法。
    /// </summary>
    private static readonly ConstructorInfo V4AuthenticatorConstructor = ResolveRequiredConstructor(
        V4AuthenticatorType,
        typeof(bool),
        typeof(string),
        typeof(string),
        typeof(string),
        typeof(string));

    /// <summary>
    /// V4 预签名方法。
    /// </summary>
    private static readonly MethodInfo PresignUrlMethod = ResolveRequiredMethod(
        V4AuthenticatorType,
        "PresignURL",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        HttpRequestMessageBuilderType,
        typeof(int),
        typeof(string),
        typeof(string),
        typeof(DateTime?));

    /// <summary>
    /// MinIO 客户端。
    /// </summary>
    private readonly MinioClient minioClient;

    /// <summary>
    /// MinIO 运行期配置。
    /// </summary>
    private readonly MinioObjectStorageClientOptions options;

    /// <summary>
    /// 初始化 Multipart 调用器。
    /// </summary>
    /// <param name="minioClient">MinIO 客户端。</param>
    /// <param name="options">运行期配置。</param>
    internal MinioMultipartOperationInvoker(
        MinioClient minioClient,
        MinioObjectStorageClientOptions options) {
        this.minioClient = minioClient;
        this.options = options;
    }

    /// <summary>
    /// 创建 Multipart 上传会话。
    /// </summary>
    /// <param name="bucketName">Bucket 名称。</param>
    /// <param name="objectKey">对象键。</param>
    /// <param name="contentType">内容类型。</param>
    /// <param name="headers">请求头。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>UploadId。</returns>
    internal Task<string> CreateMultipartUploadAsync(
        string bucketName,
        string objectKey,
        string? contentType,
        IDictionary<string, string> headers,
        CancellationToken cancellationToken) {
        var args = Activator.CreateInstance(NewMultipartUploadPutArgsType, nonPublic: true)
            ?? throw new InvalidOperationException("无法创建 MinIO Multipart 初始化参数。");

        // 步骤 1：填充 Bucket/ObjectKey/Headers。
        InvokeFluent(args, "WithBucket", bucketName);
        InvokeFluent(args, "WithObject", objectKey);
        if (headers.Count > 0) {
            InvokeFluent(args, "WithHeaders", headers);
        }

        // 步骤 2：内容类型仅在显式提供时写入，避免污染默认头。
        if (!string.IsNullOrWhiteSpace(contentType)) {
            InvokeFluent(args, "WithContentType", contentType.Trim());
        }

        // 步骤 3：调用 SDK 内部 Multipart 初始化逻辑，获取 UploadId。
        return InvokeTaskAsync<string>(NewMultipartUploadMethod, minioClient, args, cancellationToken);
    }

    /// <summary>
    /// 创建 Multipart 分片上传预签名地址。
    /// </summary>
    /// <param name="bucketName">Bucket 名称。</param>
    /// <param name="objectKey">对象键。</param>
    /// <param name="uploadId">UploadId。</param>
    /// <param name="partNumber">分片序号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>预签名地址。</returns>
    internal async Task<string> CreateMultipartUploadPartPresignedUrlAsync(
        string bucketName,
        string objectKey,
        string uploadId,
        int partNumber,
        CancellationToken cancellationToken) {
        // 步骤 1：构建普通 PUT 请求。
        var requestBuilder = await CreateObjectRequestAsync(
            HttpMethod.Put,
            bucketName,
            objectKey,
            headers: null,
            contentType: "application/octet-stream",
            cancellationToken).ConfigureAwait(false);

        // 步骤 2：补充 Multipart 子资源查询参数。
        AppendQueryString(requestBuilder, new Dictionary<string, string>(StringComparer.Ordinal) {
            ["uploadId"] = uploadId,
            ["partNumber"] = partNumber.ToString(CultureInfo.InvariantCulture)
        });

        // 步骤 3：使用 SDK 内部签名器生成包含 Multipart 查询参数的预签名 URL。
        return CreatePresignedUrl(requestBuilder, options.MultipartPartExpireSeconds);
    }

    /// <summary>
    /// 完成 Multipart 上传。
    /// </summary>
    /// <param name="bucketName">Bucket 名称。</param>
    /// <param name="objectKey">对象键。</param>
    /// <param name="uploadId">UploadId。</param>
    /// <param name="partEtags">已完成分片集合。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    internal Task CompleteMultipartUploadAsync(
        string bucketName,
        string objectKey,
        string uploadId,
        IReadOnlyDictionary<int, string> partEtags,
        CancellationToken cancellationToken) {
        var args = Activator.CreateInstance(CompleteMultipartUploadArgsType, nonPublic: true)
            ?? throw new InvalidOperationException("无法创建 MinIO Multipart 完成参数。");

        InvokeFluent(args, "WithBucket", bucketName);
        InvokeFluent(args, "WithObject", objectKey);
        InvokeFluent(args, "WithUploadId", uploadId);
        InvokeFluent(args, "WithETags", partEtags);
        return InvokeTaskAsync(CompleteMultipartUploadMethod, minioClient, args, cancellationToken);
    }

    /// <summary>
    /// 中止 Multipart 上传。
    /// </summary>
    /// <param name="bucketName">Bucket 名称。</param>
    /// <param name="objectKey">对象键。</param>
    /// <param name="uploadId">UploadId。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    internal Task AbortMultipartUploadAsync(
        string bucketName,
        string objectKey,
        string uploadId,
        CancellationToken cancellationToken) {
        var args = new RemoveUploadArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithUploadId(uploadId);
        return InvokeTaskAsync(RemoveUploadMethod, minioClient, args, cancellationToken);
    }

    /// <summary>
    /// 创建对象请求构建器。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="bucketName">Bucket 名称。</param>
    /// <param name="objectKey">对象键。</param>
    /// <param name="headers">请求头。</param>
    /// <param name="contentType">内容类型。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>请求构建器。</returns>
    private async Task<object> CreateObjectRequestAsync(
        HttpMethod method,
        string bucketName,
        string objectKey,
        IDictionary<string, string>? headers,
        string contentType,
        CancellationToken cancellationToken) {
        var requestTask = (Task?)CreateObjectRequestMethod.Invoke(
            obj: null,
            parameters: [minioClient, method, bucketName, objectKey, headers, contentType, ReadOnlyMemory<byte>.Empty, null, false]);
        if (requestTask is null) {
            throw new InvalidOperationException("MinIO 请求构建失败。");
        }

        await requestTask.ConfigureAwait(false);
        var resultProperty = requestTask.GetType().GetProperty("Result");
        return resultProperty?.GetValue(requestTask)
            ?? throw new InvalidOperationException("MinIO 请求构建结果为空。");
    }

    /// <summary>
    /// 生成预签名地址。
    /// </summary>
    /// <param name="requestBuilder">请求构建器。</param>
    /// <param name="expirySeconds">有效期。</param>
    /// <returns>预签名地址。</returns>
    private string CreatePresignedUrl(object requestBuilder, int expirySeconds) {
        var authenticator = V4AuthenticatorConstructor.Invoke([
            options.UseSsl,
            options.AccessKey,
            options.SecretKey,
            options.Region,
            string.Empty
        ]);

        return (string?)PresignUrlMethod.Invoke(authenticator, [requestBuilder, expirySeconds, options.Region, string.Empty, null])
            ?? throw new InvalidOperationException("MinIO 预签名地址生成失败。");
    }

    /// <summary>
    /// 追加查询字符串。
    /// </summary>
    /// <param name="requestBuilder">请求构建器。</param>
    /// <param name="parameters">查询参数。</param>
    private static void AppendQueryString(object requestBuilder, IReadOnlyDictionary<string, string> parameters) {
        var requestUriProperty = requestBuilder.GetType().GetProperty("RequestUri", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("MinIO 请求构建器缺少 RequestUri 属性。");
        var requestUri = (Uri?)requestUriProperty.GetValue(requestBuilder)
            ?? throw new InvalidOperationException("MinIO 请求构建器 RequestUri 为空。");
        var uriBuilder = new UriBuilder(requestUri);
        var query = string.Join("&", parameters.Select(static pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
        uriBuilder.Query = string.IsNullOrWhiteSpace(uriBuilder.Query)
            ? query
            : $"{uriBuilder.Query.TrimStart('?')}&{query}";
        requestUriProperty.SetValue(requestBuilder, uriBuilder.Uri);
    }

    /// <summary>
    /// 调用 Fluent 方法。
    /// </summary>
    /// <param name="target">目标对象。</param>
    /// <param name="methodName">方法名。</param>
    /// <param name="argument">参数。</param>
    private static void InvokeFluent(object target, string methodName, params object[] arguments) {
        var argumentTypes = arguments.Select(static argument => argument.GetType()).ToArray();
        var method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: argumentTypes,
            modifiers: null);
        if (method is null) {
            throw new InvalidOperationException($"MinIO 参数对象缺少方法：{methodName}。");
        }

        _ = method.Invoke(target, arguments);
    }

    /// <summary>
    /// 调用返回 <see cref="Task{TResult}"/> 的反射方法。
    /// </summary>
    /// <typeparam name="TResult">结果类型。</typeparam>
    /// <param name="method">方法。</param>
    /// <param name="target">目标对象。</param>
    /// <param name="arguments">参数。</param>
    /// <returns>方法返回结果。</returns>
    private static async Task<TResult> InvokeTaskAsync<TResult>(MethodInfo method, object target, params object?[] arguments) {
        var task = (Task<TResult>?)method.Invoke(target, arguments);
        if (task is null) {
            throw new InvalidOperationException($"MinIO 反射调用失败：{method.Name}。");
        }

        return await task.ConfigureAwait(false);
    }

    /// <summary>
    /// 调用返回 <see cref="Task"/> 的反射方法。
    /// </summary>
    /// <param name="method">方法。</param>
    /// <param name="target">目标对象。</param>
    /// <param name="arguments">参数。</param>
    private static async Task InvokeTaskAsync(MethodInfo method, object target, params object?[] arguments) {
        var task = (Task?)method.Invoke(target, arguments);
        if (task is null) {
            throw new InvalidOperationException($"MinIO 反射调用失败：{method.Name}。");
        }

        await task.ConfigureAwait(false);
    }

    /// <summary>
    /// 解析必需的类型。
    /// </summary>
    /// <param name="fullName">完整类型名。</param>
    /// <returns>类型。</returns>
    private static Type ResolveRequiredType(string fullName) {
        return typeof(MinioClient).Assembly.GetType(fullName)
            ?? throw new InvalidOperationException($"未找到 MinIO 类型：{fullName}。");
    }

    /// <summary>
    /// 解析必需的构造方法。
    /// </summary>
    /// <param name="type">目标类型。</param>
    /// <param name="parameterTypes">参数类型。</param>
    /// <returns>构造方法。</returns>
    private static ConstructorInfo ResolveRequiredConstructor(Type type, params Type[] parameterTypes) {
        return type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, binder: null, parameterTypes, modifiers: null)
            ?? throw new InvalidOperationException($"未找到 MinIO 构造方法：{type.FullName}。");
    }

    /// <summary>
    /// 解析必需的方法。
    /// </summary>
    /// <param name="type">目标类型。</param>
    /// <param name="methodName">方法名。</param>
    /// <param name="bindingFlags">绑定标记。</param>
    /// <param name="parameterTypes">参数类型。</param>
    /// <returns>方法信息。</returns>
    private static MethodInfo ResolveRequiredMethod(Type type, string methodName, BindingFlags bindingFlags, params Type[] parameterTypes) {
        return type.GetMethod(methodName, bindingFlags, binder: null, parameterTypes, modifiers: null)
            ?? throw new InvalidOperationException($"未找到 MinIO 方法：{type.FullName}.{methodName}。");
    }

    /// <summary>
    /// 解析必需的实例方法。
    /// </summary>
    /// <param name="type">目标类型。</param>
    /// <param name="methodName">方法名。</param>
    /// <param name="firstParameterTypeFullName">首参数类型名。</param>
    /// <param name="otherParameterTypes">其他参数类型。</param>
    /// <returns>方法信息。</returns>
    private static MethodInfo ResolveRequiredInstanceMethod(
        Type type,
        string methodName,
        string firstParameterTypeFullName,
        params Type[] otherParameterTypes) {
        var method = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .SingleOrDefault(method => {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal)) {
                    return false;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != otherParameterTypes.Length + 1) {
                    return false;
                }

                if (!string.Equals(parameters[0].ParameterType.FullName, firstParameterTypeFullName, StringComparison.Ordinal)) {
                    return false;
                }

                for (var index = 0; index < otherParameterTypes.Length; index++) {
                    if (parameters[index + 1].ParameterType != otherParameterTypes[index]) {
                        return false;
                    }
                }

                return true;
            });
        return method ?? throw new InvalidOperationException($"未找到 MinIO 实例方法：{type.FullName}.{methodName}。");
    }

    /// <summary>
    /// 解析必需的实例方法。
    /// </summary>
    /// <param name="type">目标类型。</param>
    /// <param name="methodName">方法名。</param>
    /// <param name="parameterTypes">参数类型。</param>
    /// <returns>方法信息。</returns>
    private static MethodInfo ResolveRequiredInstanceMethod(Type type, string methodName, params Type[] parameterTypes) {
        return ResolveRequiredMethod(type, methodName, BindingFlags.Instance | BindingFlags.NonPublic, parameterTypes);
    }
}
