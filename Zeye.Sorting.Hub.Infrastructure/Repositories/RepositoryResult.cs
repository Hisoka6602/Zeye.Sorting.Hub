using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Repositories {
    /// <summary>
    /// 仓储操作结果（用于隔离异常，避免影响上层调用链）
    /// </summary>
    public readonly record struct RepositoryResult {
        /// <summary>
        /// 是否成功
        /// </summary>
        public required bool IsSuccess { get; init; }

        /// <summary>
        /// 错误信息（成功时为空）
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// 方法：Success。
        /// </summary>
        public static RepositoryResult Success() => new() { IsSuccess = true };

        /// <summary>
        /// 方法：Fail。
        /// </summary>
        public static RepositoryResult Fail(string errorMessage) => new() {
            IsSuccess = false,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "仓储操作失败" : errorMessage
        };
    }

    /// <summary>
    /// 仓储操作结果（带返回值）
    /// </summary>
    public readonly record struct RepositoryResult<T> {
        /// <summary>
        /// 是否成功
        /// </summary>
        public required bool IsSuccess { get; init; }

        /// <summary>
        /// 返回值（失败时为默认值）
        /// </summary>
        public T? Value { get; init; }

        /// <summary>
        /// 错误信息（成功时为空）
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// 方法：Success。
        /// </summary>
        public static RepositoryResult<T> Success(T value) => new() { IsSuccess = true, Value = value };

        /// <summary>
        /// 方法：Fail。
        /// </summary>
        public static RepositoryResult<T> Fail(string errorMessage) => new() {
            IsSuccess = false,
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "仓储操作失败" : errorMessage
        };
    }
}
