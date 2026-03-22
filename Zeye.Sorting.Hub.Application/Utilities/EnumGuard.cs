using NLog;

namespace Zeye.Sorting.Hub.Application.Utilities;

/// <summary>
/// 枚举值合法性校验工具（仅供应用层服务使用）。
/// 统一封装 <see cref="Enum.IsDefined"/> 判断、Warn 日志记录与
/// <see cref="ArgumentOutOfRangeException"/> 抛出，消除各服务中重复的枚举验证模板代码。
/// </summary>
internal static class EnumGuard {
    /// <summary>
    /// NLog 日志器。
    /// </summary>
    private static readonly ILogger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// 验证整型值是否为有效的枚举成员；无效时记录警告日志并抛出
    /// <see cref="ArgumentOutOfRangeException"/>。
    /// </summary>
    /// <typeparam name="TEnum">目标枚举类型（必须为 struct 枚举）。</typeparam>
    /// <param name="value">待验证的整型枚举值。</param>
    /// <param name="paramName">参数名称（用于异常与日志输出）。</param>
    /// <param name="errorMessage">异常消息（描述具体无效原因）。</param>
    /// <param name="logContext">日志上下文（操作名称，如"新增包裹"）。</param>
    internal static void ThrowIfUndefined<TEnum>(int value, string paramName, string errorMessage, string logContext)
        where TEnum : struct, Enum {
        if (!Enum.IsDefined(typeof(TEnum), value)) {
            Logger.Warn("{LogContext}参数非法，{ParamName}={Value}", logContext, paramName, value);
            throw new ArgumentOutOfRangeException(paramName, errorMessage);
        }
    }

    /// <summary>
    /// 验证可空整型值是否为有效的枚举成员（<c>null</c> 视为合法，跳过验证）；
    /// 有值且无效时记录警告日志并抛出 <see cref="ArgumentOutOfRangeException"/>。
    /// </summary>
    /// <typeparam name="TEnum">目标枚举类型（必须为 struct 枚举）。</typeparam>
    /// <param name="value">待验证的可空整型枚举值。</param>
    /// <param name="paramName">参数名称（用于异常与日志输出）。</param>
    /// <param name="errorMessage">异常消息（描述具体无效原因）。</param>
    /// <param name="logContext">日志上下文（操作名称，如"更新包裹状态"）。</param>
    internal static void ThrowIfUndefined<TEnum>(int? value, string paramName, string errorMessage, string logContext)
        where TEnum : struct, Enum {
        if (value.HasValue) {
            ThrowIfUndefined<TEnum>(value.Value, paramName, errorMessage, logContext);
        }
    }
}
