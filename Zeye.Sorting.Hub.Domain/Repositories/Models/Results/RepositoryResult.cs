using Zeye.Sorting.Hub.Domain.Enums;

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

/// <summary>
/// 仓储层稳定错误码定义。
/// </summary>
public static class RepositoryErrorCodes {
    /// <summary>
    /// Parcel 主键冲突错误码。
    /// </summary>
    public const string ParcelIdConflict = "Parcel.Id.Conflict";
}

/// <summary>
/// 危险批量动作执行结果。
/// </summary>
public readonly record struct DangerousBatchActionResult {
    /// <summary>
    /// 动作名称（用于审计检索）。
    /// </summary>
    public required string ActionName { get; init; }

    /// <summary>
    /// 隔离器决策结果。
    /// </summary>
    public required ActionIsolationDecision Decision { get; init; }

    /// <summary>
    /// 计划处理数量（受单次上限保护）。
    /// </summary>
    public required int PlannedCount { get; init; }

    /// <summary>
    /// 实际执行数量（dry-run 或阻断时为 0）。
    /// </summary>
    public required int ExecutedCount { get; init; }

    /// <summary>
    /// 是否为 dry-run。
    /// </summary>
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// 是否被隔离守卫阻断。
    /// </summary>
    public required bool IsBlockedByGuard { get; init; }

    /// <summary>
    /// 补偿说明（用于明确回滚边界）。
    /// </summary>
    public required string CompensationBoundary { get; init; }
}
