using NLog;

namespace Zeye.Sorting.Hub.Application.Utilities;

/// <summary>
/// 基础参数边界守卫工具（仅供应用层服务使用）。
/// 统一封装常用的范围检查、Warn 日志记录与 <see cref="ArgumentOutOfRangeException"/> 抛出，
/// 消除各服务中重复的参数校验模板代码。
/// </summary>
internal static class Guard {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 校验长整型值是否大于零；不大于零时记录警告日志并抛出
    /// <see cref="ArgumentOutOfRangeException"/>。
    /// 适用于 Id 类参数（必须为正数）。
    /// </summary>
    /// <param name="value">待校验的值。</param>
    /// <param name="paramName">参数名称（用于异常与日志输出）。</param>
    /// <param name="errorMessage">异常消息（描述具体约束）。</param>
    /// <param name="logContext">日志上下文（操作名称，如"删除包裹"）。</param>
    internal static void ThrowIfZeroOrNegative(long value, string paramName, string errorMessage, string logContext) {
        if (value <= 0) {
            Logger.Warn("{LogContext}参数非法，{ParamName}={Value}", logContext, paramName, value);
            throw new ArgumentOutOfRangeException(paramName, errorMessage);
        }
    }

    /// <summary>
    /// 校验整型值是否大于零；不大于零时记录警告日志并抛出
    /// <see cref="ArgumentOutOfRangeException"/>。
    /// 适用于页码、页大小等必须为正数的整型参数。
    /// </summary>
    /// <param name="value">待校验的值。</param>
    /// <param name="paramName">参数名称（用于异常与日志输出）。</param>
    /// <param name="errorMessage">异常消息（描述具体约束）。</param>
    /// <param name="logContext">日志上下文（操作名称，如"分页查询 Parcel 列表"）。</param>
    internal static void ThrowIfZeroOrNegative(int value, string paramName, string errorMessage, string logContext) {
        if (value <= 0) {
            Logger.Warn("{LogContext}参数非法，{ParamName}={Value}", logContext, paramName, value);
            throw new ArgumentOutOfRangeException(paramName, errorMessage);
        }
    }

    /// <summary>
    /// 校验整型值是否不小于零；小于零时记录警告日志并抛出
    /// <see cref="ArgumentOutOfRangeException"/>。
    /// 适用于可选数量类参数（允许为 0，但不允许为负数）。
    /// </summary>
    /// <param name="value">待校验的值。</param>
    /// <param name="paramName">参数名称（用于异常与日志输出）。</param>
    /// <param name="errorMessage">异常消息（描述具体约束）。</param>
    /// <param name="logContext">日志上下文（操作名称，如"查询 Parcel 邻近记录"）。</param>
    internal static void ThrowIfNegative(int value, string paramName, string errorMessage, string logContext) {
        if (value < 0) {
            Logger.Warn("{LogContext}参数非法，{ParamName}={Value}", logContext, paramName, value);
            throw new ArgumentOutOfRangeException(paramName, errorMessage);
        }
    }
}
