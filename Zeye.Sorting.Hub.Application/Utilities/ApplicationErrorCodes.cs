namespace Zeye.Sorting.Hub.Application.Utilities;

/// <summary>
/// 应用层稳定错误码定义。
/// </summary>
public static class ApplicationErrorCodes {
    /// <summary>
    /// 通用参数校验失败错误码。
    /// </summary>
    public const string ValidationFailed = "Application.ValidationFailed";

    /// <summary>
    /// 资源不存在错误码。
    /// </summary>
    public const string NotFound = "Application.NotFound";

    /// <summary>
    /// 业务冲突错误码。
    /// </summary>
    public const string Conflict = "Application.Conflict";

    /// <summary>
    /// 幂等拒绝错误码。
    /// </summary>
    public const string IdempotencyRejected = "Application.IdempotencyRejected";

    /// <summary>
    /// 不支持的业务操作错误码。
    /// </summary>
    public const string UnsupportedOperation = "Application.UnsupportedOperation";

    /// <summary>
    /// 通用内部失败错误码。
    /// </summary>
    public const string UnexpectedFailure = "Application.UnexpectedFailure";
}
