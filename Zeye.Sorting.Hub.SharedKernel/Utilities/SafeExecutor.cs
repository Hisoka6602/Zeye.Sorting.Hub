using NLog;

namespace Zeye.Sorting.Hub.SharedKernel.Utilities;

/// <summary>
/// 安全执行器 — 确保任何方法异常都不会导致程序崩溃。
/// 所有异常均通过 NLog 记录，不再依赖 Microsoft.Extensions.Logging 注入。
/// </summary>
public class SafeExecutor {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 安全执行同步方法，异常被捕获并记录日志后返回 <c>false</c>。
    /// </summary>
    /// <param name="action">待执行操作。</param>
    /// <param name="operationName">操作名称（用于日志记录）。</param>
    /// <returns><c>true</c> 表示执行成功；<c>false</c> 表示执行发生异常。</returns>
    public bool Execute(Action action, string operationName) {
        try {
            action();
            return true;
        }
        catch (Exception ex) {
            Logger.Error(ex, "执行操作失败: {OperationName}", operationName);
            return false;
        }
    }

    /// <summary>
    /// 安全执行异步方法，异常被捕获并记录日志后返回 <c>false</c>。
    /// </summary>
    /// <param name="action">待执行异步操作。</param>
    /// <param name="operationName">操作名称（用于日志记录）。</param>
    /// <returns><c>true</c> 表示执行成功；<c>false</c> 表示执行发生异常。</returns>
    public async Task<bool> ExecuteAsync(Func<Task> action, string operationName) {
        try {
            await action();
            return true;
        }
        catch (Exception ex) {
            Logger.Error(ex, "执行操作失败: {OperationName}", operationName);
            return false;
        }
    }

    /// <summary>
    /// 安全执行带返回值的异步方法，异常被捕获并记录日志后返回默认值。
    /// </summary>
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="func">待执行异步函数。</param>
    /// <param name="operationName">操作名称（用于日志记录）。</param>
    /// <returns>成功时返回 <c>(true, result)</c>；失败时返回 <c>(false, default)</c>。</returns>
    public async Task<(bool Success, T? Result)> ExecuteAsync<T>(Func<Task<T>> func, string operationName) {
        try {
            var result = await func();
            return (true, result);
        }
        catch (Exception ex) {
            Logger.Error(ex, "执行操作失败: {OperationName}", operationName);
            return (false, default);
        }
    }
}
