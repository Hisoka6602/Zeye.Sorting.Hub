using Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.AutoTuning;

/// <summary>统一读取和执行阶段的异常分类，展开包装异常并支持四种数据库。</summary>
internal static class SlowQueryFailureClassifier {
    /// <summary>按有限深度展开异常，避免包装后的超时和取消漏计。</summary>
    private static IEnumerable<Exception> Causes(Exception? error, int depth = 0) {
        if (error is null || depth >= 16) yield break;
        yield return error;
        if (error is AggregateException aggregate) {
            foreach (var child in aggregate.InnerExceptions)
                foreach (var cause in Causes(child, depth + 1)) yield return cause;
        }
        else foreach (var cause in Causes(error.InnerException, depth + 1)) yield return cause;
    }
    /// <summary>取消与普通错误分开统计。</summary>
    internal static bool IsCanceled(Exception? error) => Causes(error).Any(cause => cause is OperationCanceledException);
    /// <summary>数字相同的错误码可能语义不同；兼容真实提供器及测试中的显式提供器名称。</summary>
    private static bool IsMySql(Exception error, string provider) => provider.Contains("MySql", StringComparison.OrdinalIgnoreCase)
        || (error.GetType().FullName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) ?? false);
    /// <summary>支持框架超时、SQL Server 和 MySQL 命令超时及 Oracle 资源等待超时。</summary>
    internal static bool IsTimeout(Exception? error, string provider = "") => Causes(error).Any(cause => cause is TimeoutException ||
        DatabaseProviderOperations.TryGetProviderErrorNumber(cause, out var number) && (number is -2 or 3024 or 30006 || number is -1 or 1205 && IsMySql(cause, provider)));
    /// <summary>支持 SQL Server、MySQL 和 Oracle 的死锁错误码，SQLite 锁忙不误标成死锁。</summary>
    internal static bool IsDeadlock(Exception? error, string provider = "") => Causes(error).Any(cause =>
        DatabaseProviderOperations.TryGetProviderErrorNumber(cause, out var number) && (number is 1213 or 60 || number == 1205 && !IsMySql(cause, provider)));
}
