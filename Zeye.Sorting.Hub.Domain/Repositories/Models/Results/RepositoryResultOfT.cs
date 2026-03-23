namespace Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

/// <summary>
/// 仓储操作结果（带返回值）。
/// </summary>
/// <typeparam name="T">返回值类型。</typeparam>
public readonly record struct RepositoryResult<T> {
    /// <summary>
    /// 仓储错误码（成功时为空）。
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// 是否成功。
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// 返回值（失败时为默认值）。
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// 错误信息（成功时为空）。
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// 创建表示操作成功的泛型结果对象，并返回指定值。
    /// </summary>
    public static RepositoryResult<T> Success(T value) => new() { IsSuccess = true, Value = value };

    /// <summary>
    /// 创建表示操作失败的泛型结果对象，并附带错误消息。
    /// </summary>
    public static RepositoryResult<T> Fail(string errorMessage) => new() {
        IsSuccess = false,
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "仓储操作失败" : errorMessage
    };

    /// <summary>
    /// 创建表示操作失败的泛型结果对象，并附带错误消息与稳定错误码。
    /// </summary>
    public static RepositoryResult<T> Fail(string errorMessage, string errorCode) => new() {
        IsSuccess = false,
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "仓储操作失败" : errorMessage,
        ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode
    };
}
