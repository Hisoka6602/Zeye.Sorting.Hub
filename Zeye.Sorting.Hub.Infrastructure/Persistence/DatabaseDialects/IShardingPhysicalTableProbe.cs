using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// 分表物理对象探测抽象：负责物理表存在性与关键索引缺失探测（仅探测，不执行 DDL）。
    /// </summary>
    public interface IShardingPhysicalTableProbe {
        /// <summary>
        /// 判断指定 schema 下的物理表是否存在。
        /// </summary>
        /// <param name="dbContext">用于执行探测 SQL 的 DbContext。</param>
        /// <param name="schemaName">schema 名称；为空时使用当前数据库默认 schema 语义。</param>
        /// <param name="physicalTableName">物理表名。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>存在返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        Task<bool> ExistsAsync(
            DbContext dbContext,
            string? schemaName,
            string physicalTableName,
            CancellationToken cancellationToken);

        /// <summary>
        /// 在指定物理表上探测“缺失索引名”集合（仅探测，不执行任何 DDL）。
        /// </summary>
        /// <param name="dbContext">用于执行探测 SQL 的 DbContext。</param>
        /// <param name="schemaName">schema 名称；为空时使用当前数据库默认 schema 语义。</param>
        /// <param name="physicalTableName">物理表名。</param>
        /// <param name="indexNames">期望存在的索引名集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>缺失索引名集合。</returns>
        Task<IReadOnlyList<string>> FindMissingIndexesAsync(
            DbContext dbContext,
            string? schemaName,
            string physicalTableName,
            IReadOnlyList<string> indexNames,
            CancellationToken cancellationToken);
    }
}
