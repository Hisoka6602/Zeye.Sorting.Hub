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

}
