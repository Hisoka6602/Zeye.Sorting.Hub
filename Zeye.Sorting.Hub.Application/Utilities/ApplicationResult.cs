namespace Zeye.Sorting.Hub.Application.Utilities;

/// <summary>
/// 应用层统一结果模型。
/// </summary>
public sealed record ApplicationResult {
    /// <summary>
    /// 默认失败标题。
    /// </summary>
    public const string DefaultProblemTitle = "业务处理失败";

    /// <summary>
    /// 默认失败消息。
    /// </summary>
    public const string DefaultErrorMessage = "业务处理失败";

    /// <summary>
    /// 400 状态码常量。
    /// </summary>
    public const int BadRequestStatusCode = 400;

    /// <summary>
    /// 404 状态码常量。
    /// </summary>
    public const int NotFoundStatusCode = 404;

    /// <summary>
    /// 409 状态码常量。
    /// </summary>
    public const int ConflictStatusCode = 409;

    /// <summary>
    /// 500 状态码常量。
    /// </summary>
    public const int InternalServerErrorStatusCode = 500;

    /// <summary>
    /// 200 状态码常量。
    /// </summary>
    public const int SuccessStatusCode = 200;

    /// <summary>
    /// 稳定错误码。
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// 错误消息。
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// ProblemDetails 标题。
    /// </summary>
    public string? ProblemTitle { get; }

    /// <summary>
    /// HTTP 状态码。
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// 私有构造函数，仅供工厂方法使用，禁止外部直接实例化。
    /// </summary>
    /// <param name="isSuccess">是否成功。</param>
    /// <param name="statusCode">HTTP 状态码。</param>
    /// <param name="problemTitle">问题标题。</param>
    /// <param name="errorMessage">错误消息。</param>
    /// <param name="errorCode">稳定错误码。</param>
    private ApplicationResult(bool isSuccess, int statusCode, string? problemTitle, string? errorMessage, string? errorCode) {
        IsSuccess = isSuccess;
        StatusCode = statusCode;
        ProblemTitle = problemTitle;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
    }

    /// <summary>
    /// 创建成功结果。
    /// </summary>
    /// <returns>成功结果。</returns>
    public static ApplicationResult Success() => new(
        isSuccess: true,
        statusCode: SuccessStatusCode,
        problemTitle: null,
        errorMessage: null,
        errorCode: null);

    /// <summary>
    /// 创建参数校验失败结果。
    /// </summary>
    /// <param name="errorMessage">错误消息。</param>
    /// <param name="errorCode">稳定错误码。</param>
    /// <returns>失败结果。</returns>
    public static ApplicationResult ValidationFailed(string errorMessage, string? errorCode = null) {
        return CreateFailure(
            BadRequestStatusCode,
            "请求参数无效",
            errorMessage,
            string.IsNullOrWhiteSpace(errorCode) ? ApplicationErrorCodes.ValidationFailed : errorCode);
    }

    /// <summary>
    /// 创建资源不存在结果。
    /// </summary>
    /// <param name="errorMessage">错误消息。</param>
    /// <param name="errorCode">稳定错误码。</param>
    /// <returns>失败结果。</returns>
    public static ApplicationResult NotFound(string errorMessage, string? errorCode = null) {
        return CreateFailure(
            NotFoundStatusCode,
            "资源不存在",
            errorMessage,
            string.IsNullOrWhiteSpace(errorCode) ? ApplicationErrorCodes.NotFound : errorCode);
    }

    /// <summary>
    /// 创建业务冲突结果。
    /// </summary>
    /// <param name="errorMessage">错误消息。</param>
    /// <param name="errorCode">稳定错误码。</param>
    /// <returns>失败结果。</returns>
    public static ApplicationResult Conflict(string errorMessage, string? errorCode = null) {
        return CreateFailure(
            ConflictStatusCode,
            "请求冲突",
            errorMessage,
            string.IsNullOrWhiteSpace(errorCode) ? ApplicationErrorCodes.Conflict : errorCode);
    }

    /// <summary>
    /// 创建不支持操作结果。
    /// </summary>
    /// <param name="errorMessage">错误消息。</param>
    /// <param name="errorCode">稳定错误码。</param>
    /// <returns>失败结果。</returns>
    public static ApplicationResult Unsupported(string errorMessage, string? errorCode = null) {
        return CreateFailure(
            BadRequestStatusCode,
            "当前操作不受支持",
            errorMessage,
            string.IsNullOrWhiteSpace(errorCode) ? ApplicationErrorCodes.UnsupportedOperation : errorCode);
    }

    /// <summary>
    /// 创建通用失败结果。
    /// </summary>
    /// <param name="statusCode">HTTP 状态码。</param>
    /// <param name="problemTitle">问题标题。</param>
    /// <param name="errorMessage">错误消息。</param>
    /// <param name="errorCode">稳定错误码。</param>
    /// <returns>失败结果。</returns>
    public static ApplicationResult Fail(int statusCode, string problemTitle, string errorMessage, string? errorCode = null) {
        return CreateFailure(statusCode, problemTitle, errorMessage, errorCode);
    }

    /// <summary>
    /// 创建失败结果对象。
    /// </summary>
    /// <param name="statusCode">状态码。</param>
    /// <param name="problemTitle">问题标题。</param>
    /// <param name="errorMessage">错误消息。</param>
    /// <param name="errorCode">稳定错误码。</param>
    /// <returns>失败结果。</returns>
    private static ApplicationResult CreateFailure(int statusCode, string problemTitle, string errorMessage, string? errorCode) {
        return new ApplicationResult(
            isSuccess: false,
            statusCode: NormalizeFailureStatusCode(statusCode),
            problemTitle: NormalizeProblemTitle(problemTitle),
            errorMessage: NormalizeErrorMessage(errorMessage),
            errorCode: NormalizeErrorCode(errorCode));
    }

    /// <summary>
    /// 归一化失败状态码，防止写入非错误状态。
    /// </summary>
    /// <param name="statusCode">原始状态码。</param>
    /// <returns>可用于失败结果的状态码。</returns>
    private static int NormalizeFailureStatusCode(int statusCode) {
        return statusCode is >= BadRequestStatusCode and <= 599
            ? statusCode
            : InternalServerErrorStatusCode;
    }

    /// <summary>
    /// 归一化问题标题。
    /// </summary>
    /// <param name="problemTitle">原始问题标题。</param>
    /// <returns>规范化后的问题标题。</returns>
    private static string NormalizeProblemTitle(string problemTitle) {
        return string.IsNullOrWhiteSpace(problemTitle)
            ? DefaultProblemTitle
            : problemTitle.Trim();
    }

    /// <summary>
    /// 归一化错误消息。
    /// </summary>
    /// <param name="errorMessage">原始错误消息。</param>
    /// <returns>规范化后的错误消息。</returns>
    private static string NormalizeErrorMessage(string errorMessage) {
        return string.IsNullOrWhiteSpace(errorMessage)
            ? DefaultErrorMessage
            : errorMessage.Trim();
    }

    /// <summary>
    /// 归一化稳定错误码。
    /// </summary>
    /// <param name="errorCode">原始稳定错误码。</param>
    /// <returns>规范化后的错误码。</returns>
    private static string? NormalizeErrorCode(string? errorCode) {
        return string.IsNullOrWhiteSpace(errorCode)
            ? null
            : errorCode.Trim();
    }
}
