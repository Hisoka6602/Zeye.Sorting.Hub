namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

/// <summary>
/// 仓储操作结果（用于隔离异常，避免影响上层调用链）。
/// </summary>
public readonly record struct RepositoryResult {
    /// <summary>
    /// 仓储错误码（成功时为空）。
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 错误信息（成功时为空）。
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建表示操作成功的结果对象。
    /// </summary>
    public static RepositoryResult Success() => new() { IsSuccess = true };

    /// <summary>
    /// 创建表示操作失败的结果对象，并附带错误消息。
    /// </summary>
    public static RepositoryResult Fail(string errorMessage) => new() {
        IsSuccess = false,
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "仓储操作失败" : errorMessage
    };

    /// <summary>
    /// 创建表示操作失败的结果对象，并附带错误消息与稳定错误码。
    /// </summary>
    public static RepositoryResult Fail(string errorMessage, string errorCode) => new() {
        IsSuccess = false,
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "仓储操作失败" : errorMessage,
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode
    };
}
