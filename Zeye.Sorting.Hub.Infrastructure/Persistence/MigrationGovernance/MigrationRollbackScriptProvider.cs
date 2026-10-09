using System;
using System.Linq;
using System.Text;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.MigrationGovernance;

/// <summary>
/// 迁移回滚参考脚本生成器。
/// </summary>
public static class MigrationRollbackScriptProvider {
    /// <summary>
    /// 生成手工回滚参考脚本。
    /// </summary>
    /// <param name="plan">迁移计划。</param>
    /// <returns>回滚参考脚本文本。</returns>
    public static string BuildManualRollbackScript(MigrationPlan plan) {
        ArgumentNullException.ThrowIfNull(plan);

        var builder = new StringBuilder();
        builder.AppendLine("-- 迁移回滚参考脚本（仅供人工评审与归档，不允许自动执行）");
        builder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"-- 生成时间（本地）：{plan.GeneratedAtLocal:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"-- Provider：{plan.ProviderName}");
        builder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"-- Environment：{plan.EnvironmentName}");
        builder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"-- ForwardScript：{plan.ArchivedForwardScriptPath ?? "未归档"}");
        builder.AppendLine("-- 说明：不可逆操作（如 DROP/TRUNCATE/DELETE）不得自动回滚，必须走人工审计与恢复流程。 ");
        builder.AppendLine();
        builder.AppendLine("-- 已应用迁移：");
        foreach (var migration in plan.AppliedMigrations) {
            builder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"--   - {migration}");
        }

        builder.AppendLine();
        builder.AppendLine("-- 待执行迁移：");
        foreach (var migration in plan.PendingMigrations) {
            builder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"--   - {migration}");
        }

        if (plan.DangerousOperations.Count > 0) {
            builder.AppendLine();
            builder.AppendLine("-- 危险 SQL 命中：");
            foreach (var operation in plan.DangerousOperations) {
                builder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"--   - {operation}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("-- 建议回滚步骤：");
        builder.AppendLine("-- 1. 先停止写流量并确认审计链路完整。");
        builder.AppendLine("-- 2. 结合正向脚本、DBA 审核意见与备份快照编写人工回滚脚本。");
        builder.AppendLine("-- 3. 回滚完成后重新执行迁移一致性校验与健康检查。");
        if (plan.PendingMigrations.Count > 0) {
            builder.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"-- 4. 本次候选迁移范围：{string.Join(", ", plan.PendingMigrations)}");
        }

        return builder.ToString();
    }
}
