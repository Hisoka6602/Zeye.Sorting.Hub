namespace Zeye.Sorting.Hub.Application.Services.Idempotency;

/// <summary>
/// 幂等守卫异常。
/// </summary>
public sealed class IdempotencyGuardException : InvalidOperationException {
    /// <summary>
    /// 幂等请求处理中错误码。
    /// </summary>
    public const string RequestInProgressErrorCode = "Idempotency.Request.InProgress";

    /// <summary>
    /// 幂等状态落库失败错误码。
    /// </summary>
    public const string StatePersistenceFailedErrorCode = "Idempotency.Record.StatePersistenceFailed";

    /// <summary>
    /// 错误码。
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// 初始化幂等守卫异常。
    /// </summary>
    /// <param name="errorCode">稳定错误码。</param>
    /// <param name="message">异常消息。</param>
    /// <param name="innerException">内部异常。</param>
    public IdempotencyGuardException(string errorCode, string message, Exception? innerException = null)
        : base(message, innerException) {
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? StatePersistenceFailedErrorCode : errorCode;
    }
}
