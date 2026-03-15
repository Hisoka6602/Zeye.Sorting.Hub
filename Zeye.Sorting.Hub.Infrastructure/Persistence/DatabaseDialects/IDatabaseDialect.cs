using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    public interface IDatabaseDialect {

        /// <summary>
        /// 数据库类型名称（用于日志/诊断）
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// 初始化阶段可选 SQL（非关键，失败必须可降级）
        /// </summary>
        IReadOnlyList<string> GetOptionalBootstrapSql();

        /// <summary>
        /// 基于慢查询分析结果生成自动调谐动作 SQL（非关键，失败必须可降级）
        /// </summary>
        /// <param name="schemaName">候选 schema 名；为空表示无 schema。</param>
        /// <param name="tableName">候选表名（已过标识符安全校验）。</param>
        /// <param name="whereColumns">候选筛选列（已过标识符安全校验）。</param>
        IReadOnlyList<string> BuildAutomaticTuningSql(string? schemaName, string tableName, IReadOnlyList<string> whereColumns);

        /// <summary>
        /// 是否可忽略自动调谐动作异常（如“索引已存在”）
        /// </summary>
        bool ShouldIgnoreAutoTuningException(Exception exception);
    }
}
