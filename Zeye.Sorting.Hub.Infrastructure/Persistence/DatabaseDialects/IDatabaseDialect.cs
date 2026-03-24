using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data.Common;

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

        /// <summary>
        /// 闭环自治阶段：根据风险与时段生成数据库方言差异化维护动作（统计信息/索引维护）。
        /// </summary>
        /// <param name="schemaName">候选 schema 名；为空表示无 schema。</param>
        /// <param name="tableName">候选表名（已过标识符安全校验）。</param>
        /// <param name="inPeakWindow">是否处于业务高峰时段。</param>
        /// <param name="highRisk">是否为高风险动作场景。</param>
        IReadOnlyList<string> BuildAutonomousMaintenanceSql(string? schemaName, string tableName, bool inPeakWindow, bool highRisk);

        /// <summary>
        /// 从连接字符串提取目标数据库名。
        /// </summary>
        /// <param name="connectionString">原始连接字符串。</param>
        /// <returns>目标数据库名。</returns>
        string ExtractDatabaseName(string connectionString);

        /// <summary>
        /// 基于业务连接字符串构建“服务器级管理连接”（用于探测/建库）。
        /// </summary>
        /// <param name="connectionString">原始连接字符串。</param>
        /// <returns>服务器级连接。</returns>
        DbConnection CreateAdministrationConnection(string connectionString);

        /// <summary>
        /// 探测目标数据库是否存在。
        /// </summary>
        /// <param name="administrationConnection">服务器级连接。</param>
        /// <param name="databaseName">目标数据库名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>存在返回 true，否则 false。</returns>
        Task<bool> DatabaseExistsAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken);

        /// <summary>
        /// 创建目标数据库。
        /// </summary>
        /// <param name="administrationConnection">服务器级连接。</param>
        /// <param name="databaseName">目标数据库名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>异步任务。</returns>
        Task CreateDatabaseAsync(DbConnection administrationConnection, string databaseName, CancellationToken cancellationToken);
    }
}
