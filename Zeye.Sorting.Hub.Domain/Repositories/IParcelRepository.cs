using Zeye.Sorting.Hub.Domain.Aggregates.Parcels;
using Zeye.Sorting.Hub.Domain.Enums;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Filters;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Paging;
using Zeye.Sorting.Hub.Domain.Repositories.Models.ReadModels;
using Zeye.Sorting.Hub.Domain.Repositories.Models.Results;

namespace Zeye.Sorting.Hub.Domain.Repositories {

    /// <summary>
    /// Parcel 聚合仓储契约（第一阶段可落地实现）。
    /// </summary>
    public interface IParcelRepository {
        /// <summary>
        /// 按扫码时间邻近查询的单侧最大条数上限（避免单次查询开销过大）。
        /// Application 层与 Infrastructure 层均以此为唯一来源，禁止各自硬编码魔法数字。
        /// </summary>
        const int MaxAdjacentCountPerSide = 200;

        /// <summary>
        /// 根据主键获取包裹完整聚合详情（包含值对象与集合）。
        /// </summary>
        Task<Parcel?> GetByIdAsync(long id, CancellationToken cancellationToken);

        /// <summary>
        /// 按过滤条件执行分页查询（返回摘要读模型）。
        /// </summary>
        Task<PageResult<ParcelSummaryReadModel>> GetPagedAsync(
            ParcelQueryFilter filter,
            PageRequest pageRequest,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按集包号与扫码时间范围分页查询包裹摘要。
        /// </summary>
        Task<PageResult<ParcelSummaryReadModel>> GetByBagCodeAsync(
            string bagCode,
            DateTime scannedTimeStart,
            DateTime scannedTimeEnd,
            PageRequest pageRequest,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按工作台与扫码时间范围分页查询包裹摘要。
        /// </summary>
        Task<PageResult<ParcelSummaryReadModel>> GetByWorkstationNameAsync(
            string workstationName,
            DateTime scannedTimeStart,
            DateTime scannedTimeEnd,
            PageRequest pageRequest,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按包裹状态与扫码时间范围分页查询包裹摘要。
        /// </summary>
        Task<PageResult<ParcelSummaryReadModel>> GetByStatusAsync(
            ParcelStatus status,
            DateTime scannedTimeStart,
            DateTime scannedTimeEnd,
            PageRequest pageRequest,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按实际/目标格口与扫码时间范围分页查询包裹摘要。
        /// </summary>
        /// <param name="actualChuteId">实际格口 Id（可选）。</param>
        /// <param name="targetChuteId">目标格口 Id（可选）。</param>
        /// <param name="scannedTimeStart">扫码开始时间（含边界）。</param>
        /// <param name="scannedTimeEnd">扫码结束时间（含边界）。</param>
        /// <param name="pageRequest">分页参数。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <exception cref="ArgumentException">当 <paramref name="actualChuteId"/> 与 <paramref name="targetChuteId"/> 同时为空时抛出（至少提供一个格口 Id）。</exception>
        Task<PageResult<ParcelSummaryReadModel>> GetByChuteAsync(
            long? actualChuteId,
            long? targetChuteId,
            DateTime scannedTimeStart,
            DateTime scannedTimeEnd,
            PageRequest pageRequest,
            CancellationToken cancellationToken);

        /// <summary>
        /// 按扫描时间查询前后邻近记录（时间顺序）。
        /// </summary>
        Task<IReadOnlyList<ParcelSummaryReadModel>> GetAdjacentByScannedTimeAsync(
            DateTime scannedTime,
            int beforeCount,
            int afterCount,
            CancellationToken cancellationToken);

        /// <summary>
        /// 新增包裹聚合。
        /// </summary>
        Task<RepositoryResult> AddAsync(Parcel parcel, CancellationToken cancellationToken);

        /// <summary>
        /// 更新包裹聚合。
        /// </summary>
        Task<RepositoryResult> UpdateAsync(Parcel parcel, CancellationToken cancellationToken);

        /// <summary>
        /// 删除包裹聚合。
        /// </summary>
        Task<RepositoryResult> RemoveAsync(Parcel parcel, CancellationToken cancellationToken);

        /// <summary>
        /// 按创建时间清理过期包裹（危险动作：受隔离器开关、dry-run 与审计约束）。
        /// </summary>
        Task<RepositoryResult<DangerousBatchActionResult>> RemoveExpiredAsync(DateTime createdBefore, CancellationToken cancellationToken);

        /// <summary>
        /// 批量新增包裹聚合。
        /// </summary>
        Task<RepositoryResult> AddRangeAsync(IReadOnlyCollection<Parcel> parcels, CancellationToken cancellationToken);
    }
}
