using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// 分表物理表批量探测抽象：用于单次请求返回缺失物理表集合，降低逐表探测往返开销。
    /// </summary>
    public interface IBatchShardingPhysicalTableProbe : IShardingPhysicalTableProbe {
        /// <summary>
        /// 批量查找缺失的物理表名。
        /// </summary>
        /// <param name="dbContext">用于执行探测 SQL 的 DbContext。</param>
        /// <param name="schemaName">schema 名称；为空时按方言默认语义处理。</param>
        /// <param name="physicalTableNames">待探测物理表名集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>缺失物理表名集合。</returns>
        Task<IReadOnlyList<string>> FindMissingTablesAsync(
            DbContext dbContext,
            string? schemaName,
            IReadOnlyList<string> physicalTableNames,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按逻辑表名前缀列出已存在的物理分表名（仅探测，不执行 DDL）。
        /// </summary>
        /// <param name="dbContext">用于执行探测 SQL 的 DbContext。</param>
        /// <param name="schemaName">schema 名称；为空时按方言默认语义处理。</param>
        /// <param name="baseTableName">逻辑基础表名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>已存在物理表名集合。</returns>
        Task<IReadOnlyList<string>> ListPhysicalTablesByBaseNameAsync(
            DbContext dbContext,
            string? schemaName,
            string baseTableName,
            CancellationToken cancellationToken);
    }
}
