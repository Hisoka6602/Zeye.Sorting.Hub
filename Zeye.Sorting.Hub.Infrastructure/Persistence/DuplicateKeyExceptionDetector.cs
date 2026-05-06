using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence;

/// <summary>
/// 重复键异常检测器。
/// </summary>
internal static class DuplicateKeyExceptionDetector {
    /// <summary>
    /// 判断是否为唯一键冲突。
    /// </summary>
    /// <param name="exception">数据库更新异常。</param>
    /// <returns>是否为唯一键冲突。</returns>
    public static bool IsDuplicateKeyException(DbUpdateException exception) {
        if (exception.InnerException is MySqlException mySqlException) {
            return mySqlException.Number == 1062;
        }

        if (exception.InnerException is SqlException sqlException) {
            return sqlException.Number == 2627 || sqlException.Number == 2601;
        }

        return false;
    }

    /// <summary>
    /// 判断异常消息是否包含重复键语义。
    /// </summary>
    /// <param name="message">异常消息。</param>
    /// <returns>是否包含重复键语义。</returns>
    public static bool ContainsDuplicateKeyMessage(string? message) {
        if (string.IsNullOrWhiteSpace(message)) {
            return false;
        }

        return message.Contains("same key", StringComparison.OrdinalIgnoreCase)
               || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
               || message.Contains("已存在", StringComparison.OrdinalIgnoreCase);
    }
}
