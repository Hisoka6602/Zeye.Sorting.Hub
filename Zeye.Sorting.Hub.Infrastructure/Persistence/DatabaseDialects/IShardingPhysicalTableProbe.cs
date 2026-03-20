using Microsoft.EntityFrameworkCore;

namespace Zeye.Sorting.Hub.Infrastructure.Persistence.DatabaseDialects {

    /// <summary>
    /// 分表物理表存在性探测抽象：仅负责判断目标物理表是否存在。
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
    }
}
